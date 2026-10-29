import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import {
  ConsolidatedGroupOrderItem,
  GroupOrderData,
} from '@app/models/group-orders/group-order.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { OwnerGroupOrderPageComponent } from './owner-group-order-page.component';

describe('OwnerGroupOrderPageComponent', () => {
  let fixture: ComponentFixture<OwnerGroupOrderPageComponent>;
  let groupOrdersService: jasmine.SpyObj<GroupOrdersService>;

  const demoOrder: GroupOrderData = {
    groupReservationId: 'demo-group-reservation',
    snapshot: {
      participants: [
        {
          id: 'guest-1',
          type: 'Guest',
          name: 'Andrii',
          items: [
            {
              dish: { id: 'dish-1', name: 'Caesar Salad', price: 180 },
              quantity: 2,
            },
          ],
          total: 360,
          isSubmitted: true,
        },
        {
          id: 'guest-2',
          type: 'Guest',
          name: 'Olena',
          items: [
            {
              dish: { id: 'dish-1', name: 'Caesar Salad', price: 180 },
              quantity: 1,
            },
          ],
          total: 180,
          isSubmitted: false,
        },
      ],
      total: 540,
    },
  };

  beforeEach(async () => {
    groupOrdersService = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
      'getGroupOrder$',
      'submit$',
      'submitDemo$',
    ]);
    groupOrdersService.getGroupOrder$.and.returnValue(of(demoOrder));
    groupOrdersService.submitDemo$.and.returnValue(
      of([{ dishId: 'dish-1', quantity: 2 } satisfies ConsolidatedGroupOrderItem])
    );

    await TestBed.configureTestingModule({
      imports: [OwnerGroupOrderPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: () => 'demo-reservation' },
              queryParamMap: { has: () => true },
            },
          },
        },
        { provide: GroupOrdersService, useValue: groupOrdersService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(OwnerGroupOrderPageComponent);
    fixture.detectChanges();
  });

  it('shows guest selections and excludes guests who have not submitted', () => {
    const pageText = fixture.nativeElement.textContent as string;

    expect(pageText).toContain('Andrii');
    expect(pageText).toContain('Olena');
    expect(pageText).toContain('Caesar Salad');
    expect(pageText).toContain('$360.00');
    expect(pageText).toContain('Only submitted guest baskets are included');
    expect(groupOrdersService.getGroupOrder$).toHaveBeenCalledWith('demo-reservation', true);
    expect(fixture.componentInstance.combinedTotal()).toBe(360);
  });

  it('confirms and submits the combined demo order', () => {
    const sendButton = findButton('Send group order');
    sendButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Send this group order?');
    findButton('Confirm order').click();
    fixture.detectChanges();

    expect(groupOrdersService.submitDemo$).toHaveBeenCalledWith(demoOrder.snapshot);
    expect(fixture.componentInstance.orderSubmitted()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Group order sent!');
    expect(findButton('Order sent').disabled).toBeTrue();
  });

  function findButton(label: string): HTMLButtonElement {
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button')
    ) as HTMLButtonElement[];
    const button = buttons.find((candidate) => candidate.textContent?.includes(label));

    if (!button) {
      throw new Error(`Could not find button "${label}".`);
    }

    return button;
  }
});
