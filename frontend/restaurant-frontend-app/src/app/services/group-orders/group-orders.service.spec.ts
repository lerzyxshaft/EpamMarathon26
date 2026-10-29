import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { firstValueFrom, of } from 'rxjs';

import { GroupOrdersService } from './group-orders.service';
import { GuestInviteService } from '../reservation/guest-invite.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

describe('GroupOrdersService', () => {
  let service: GroupOrdersService;
  let httpTestingController: HttpTestingController;
  let guestInviteService: jasmine.SpyObj<GuestInviteService>;

  beforeEach(() => {
    guestInviteService = jasmine.createSpyObj<GuestInviteService>('GuestInviteService', [
      'getInviteToken$',
      'getInvite$',
    ]);
    TestBed.configureTestingModule({
      providers: [
        GroupOrdersService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.example.test' },
        { provide: GuestInviteService, useValue: guestInviteService },
      ],
    });

    service = TestBed.inject(GroupOrdersService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTestingController.verify());

  it('loads the demo group without making API requests', async () => {
    const result = await firstValueFrom(service.getGroupOrder$('demo-reservation', true));

    expect(result.groupReservationId).toBe('demo-group-reservation');
    expect(result.snapshot.participants.length).toBe(3);
    expect(result.snapshot.total).toBe(1220);
    expect(result.snapshot.participants[0].isSubmitted).toBeTrue();
    expect(result.snapshot.participants[2].isSubmitted).toBeFalse();
    httpTestingController.expectNone(() => true);
  });

  it('loads live group sessions through the owner invite token', async () => {
    const snapshot = {
      participants: [],
      total: 0,
    };
    guestInviteService.getInviteToken$.and.returnValue(of({ token: 'invite-token' }));
    guestInviteService.getInvite$.and.returnValue(
      of({
        GroupReservationId: 'group-reservation-1',
        LocationId: 'location-1',
        RestaurantName: 'Green & Tasty',
        LocationAddress: '100 Main Street',
        StartTime: '2026-10-29T18:00:00Z',
        EndTime: '2026-10-29T20:00:00Z',
        MaxGuestCount: 6,
        TableNumber: 4,
        InviterName: 'Owner',
      })
    );

    const responsePromise = firstValueFrom(service.getGroupOrder$('reservation-1', false));

    expect(guestInviteService.getInviteToken$).toHaveBeenCalledWith('reservation-1');
    expect(guestInviteService.getInvite$).toHaveBeenCalledWith('invite-token');
    httpTestingController
      .expectOne('https://api.example.test/guest/groups/group-reservation-1/sessions')
      .flush(snapshot);

    const result = await responsePromise;
    expect(result.groupReservationId).toBe('group-reservation-1');
    expect(result.snapshot).toEqual(snapshot);
  });

  it('consolidates dishes only from submitted guests in demo mode', async () => {
    const groupOrder = await firstValueFrom(service.getGroupOrder$('demo-reservation', true));
    const submittedItems = await firstValueFrom(service.submitDemo$(groupOrder.snapshot));

    expect(submittedItems).toEqual([
      { dishId: 'caesar-salad', quantity: 2 },
      { dishId: 'tom-yum', quantity: 2 },
      { dishId: 'brownie', quantity: 2 },
    ]);
    httpTestingController.expectNone(() => true);
  });
});
