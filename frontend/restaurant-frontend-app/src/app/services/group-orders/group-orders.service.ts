import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, map, of, switchMap } from 'rxjs';

import {
  ConsolidatedGroupOrderItem,
  GroupOrderData,
  GroupOrderInvite,
  GroupOrderInviteToken,
  GroupOrderSnapshot,
} from '@app/models/group-orders/group-order.model';
import { GuestInviteService } from '../reservation/guest-invite.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

const DEMO_SNAPSHOT: GroupOrderSnapshot = {
  participants: [
    {
      id: 'guest-andrii',
      type: 'Guest',
      name: 'Andrii',
      items: [
        { dish: { id: 'caesar-salad', name: 'Caesar Salad', price: 180 }, quantity: 2 },
        { dish: { id: 'tom-yum', name: 'Tom Yum', price: 220 }, quantity: 1 },
      ],
      total: 580,
      isSubmitted: true,
    },
    {
      id: 'guest-olena',
      type: 'Guest',
      name: 'Olena',
      items: [
        { dish: { id: 'tom-yum', name: 'Tom Yum', price: 220 }, quantity: 1 },
        { dish: { id: 'brownie', name: 'Chocolate Brownie', price: 120 }, quantity: 2 },
      ],
      total: 460,
      isSubmitted: true,
    },
    {
      id: 'guest-maksym',
      type: 'Guest',
      name: 'Maksym',
      items: [{ dish: { id: 'caesar-salad', name: 'Caesar Salad', price: 180 }, quantity: 1 }],
      total: 180,
      isSubmitted: false,
    },
  ],
  total: 1220,
};

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);
  private readonly guestInviteService = inject(GuestInviteService);

  getGroupOrder$(reservationId: string, demoMode: boolean): Observable<GroupOrderData> {
    if (demoMode) {
      return of({
        groupReservationId: 'demo-group-reservation',
        snapshot: this.copyDemoSnapshot(),
      });
    }

    return this.guestInviteService.getInviteToken$(reservationId).pipe(
      switchMap((tokenResponse: GroupOrderInviteToken) =>
        this.guestInviteService.getInvite$(tokenResponse.token)
      ),
      switchMap((invite: GroupOrderInvite) => {
        const groupReservationId = invite.GroupReservationId ?? invite.groupReservationId;
        if (!groupReservationId) {
          throw new Error('The group reservation ID is missing from the invitation response.');
        }

        return this.http
          .get<GroupOrderSnapshot>(
            `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions`
          )
          .pipe(map((snapshot) => ({ groupReservationId, snapshot })));
      })
    );
  }

  submit$(
    reservationId: string,
    groupReservationId: string
  ): Observable<ConsolidatedGroupOrderItem[]> {
    return this.http.post<ConsolidatedGroupOrderItem[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupReservationId)}/submit`,
      { reservationId }
    );
  }

  submitDemo$(snapshot: GroupOrderSnapshot): Observable<ConsolidatedGroupOrderItem[]> {
    const consolidatedItems = new Map<string, number>();

    for (const participant of snapshot.participants) {
      if (!participant.isSubmitted || participant.type === 'Owner') {
        continue;
      }

      for (const item of participant.items) {
        consolidatedItems.set(
          item.dish.id,
          (consolidatedItems.get(item.dish.id) ?? 0) + item.quantity
        );
      }
    }

    return of(Array.from(consolidatedItems, ([dishId, quantity]) => ({ dishId, quantity })));
  }

  private copyDemoSnapshot(): GroupOrderSnapshot {
    return {
      total: DEMO_SNAPSHOT.total,
      participants: DEMO_SNAPSHOT.participants.map((participant) => ({
        ...participant,
        items: participant.items.map((item) => ({
          ...item,
          dish: { ...item.dish },
        })),
      })),
    };
  }
}
