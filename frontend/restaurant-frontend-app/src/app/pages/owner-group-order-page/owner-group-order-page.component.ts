import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, catchError, exhaustMap, tap, timer } from 'rxjs';

import {
  ConsolidatedGroupOrderItem,
  GroupOrderData,
  GroupOrderParticipant,
  GroupOrderSnapshot,
} from '@app/models/group-orders/group-order.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';

interface ConsolidatedDishView {
  id: string;
  name: string;
  price: number;
  quantity: number;
}

@Component({
  standalone: true,
  selector: 'app-owner-group-order-page',
  templateUrl: './owner-group-order-page.component.html',
  styleUrl: './owner-group-order-page.component.scss',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class OwnerGroupOrderPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly groupOrdersService = inject(GroupOrdersService);

  readonly reservationId = this.route.snapshot.paramMap.get('reservationId') ?? '';
  readonly isDemo = this.route.snapshot.queryParamMap.has('demo');
  readonly snapshot = signal<GroupOrderSnapshot | null>(null);
  readonly groupReservationId = signal<string | null>(null);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly orderSubmitted = signal(false);
  readonly showSubmitConfirmation = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly submitErrorMessage = signal<string | null>(null);
  readonly lastSyncedAt = signal<Date | null>(null);
  readonly submittedOrder = signal<ConsolidatedGroupOrderItem[]>([]);

  readonly participants = computed(() => this.snapshot()?.participants ?? []);
  readonly guestCount = computed(
    () => this.participants().filter((participant) => participant.type === 'Guest').length
  );
  readonly submittedGuestCount = computed(
    () =>
      this.participants().filter(
        (participant) => participant.type === 'Guest' && participant.isSubmitted
      ).length
  );
  readonly combinedDishes = computed(() => this.createCombinedDishList(this.participants()));
  readonly combinedTotal = computed(() =>
    this.combinedDishes().reduce((sum, item) => sum + item.price * item.quantity, 0)
  );
  readonly submittedDishCount = computed(() =>
    this.submittedOrder().reduce((count, item) => count + item.quantity, 0)
  );
  readonly canSubmit = computed(
    () => this.submittedGuestCount() > 0 && !this.orderSubmitted() && !this.submitting()
  );

  constructor() {
    if (this.isDemo) {
      this.loadSnapshot();
      return;
    }

    timer(0, 15_000)
      .pipe(
        exhaustMap(() => {
          if (this.orderSubmitted()) {
            return EMPTY;
          }

          return this.groupOrdersService.getGroupOrder$(this.reservationId, false).pipe(
            tap((groupOrder) => {
              this.acceptGroupOrder(groupOrder);
              this.lastSyncedAt.set(new Date());
              this.errorMessage.set(null);
              this.loading.set(false);
            }),
            catchError((error: unknown) => {
              this.errorMessage.set(this.getErrorMessage(error));
              this.loading.set(false);
              return EMPTY;
            })
          );
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();
  }

  retryLoad(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.groupOrdersService
      .getGroupOrder$(this.reservationId, this.isDemo)
      .pipe(
        tap((groupOrder) => {
          this.acceptGroupOrder(groupOrder);
          this.lastSyncedAt.set(new Date());
          this.loading.set(false);
        }),
        catchError((error: unknown) => {
          this.errorMessage.set(this.getErrorMessage(error));
          this.loading.set(false);
          return EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();
  }

  openSubmitConfirmation(): void {
    if (this.canSubmit()) {
      this.showSubmitConfirmation.set(true);
      this.submitErrorMessage.set(null);
    }
  }

  closeSubmitConfirmation(): void {
    if (!this.submitting()) {
      this.showSubmitConfirmation.set(false);
    }
  }

  confirmSubmit(): void {
    const snapshot = this.snapshot();
    if (!snapshot || !this.canSubmit()) {
      return;
    }

    this.submitting.set(true);
    this.submitErrorMessage.set(null);
    const groupReservationId = this.groupReservationId();
    if (!this.isDemo && !groupReservationId) {
      this.submitErrorMessage.set(
        'The group reservation could not be identified. Reload the page and try again.'
      );
      this.submitting.set(false);
      return;
    }

    const submit$ = this.isDemo
      ? this.groupOrdersService.submitDemo$(snapshot)
      : this.groupOrdersService.submit$(this.reservationId, groupReservationId!);

    submit$
      .pipe(
        tap((items) => {
          this.submittedOrder.set(items);
          this.orderSubmitted.set(true);
          this.showSubmitConfirmation.set(false);
          this.submitting.set(false);
        }),
        catchError((error: unknown) => {
          this.submitErrorMessage.set(this.getErrorMessage(error));
          this.submitting.set(false);
          return EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();
  }

  participantTotal(participant: GroupOrderParticipant): number {
    return participant.items.reduce((total, item) => total + item.dish.price * item.quantity, 0);
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part.charAt(0).toUpperCase())
      .join('');
  }

  private loadSnapshot(): void {
    this.groupOrdersService
      .getGroupOrder$(this.reservationId, this.isDemo)
      .pipe(
        tap((groupOrder) => {
          this.acceptGroupOrder(groupOrder);
          this.lastSyncedAt.set(new Date());
          this.loading.set(false);
        }),
        catchError((error: unknown) => {
          this.errorMessage.set(this.getErrorMessage(error));
          this.loading.set(false);
          return EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();
  }

  private acceptGroupOrder(groupOrder: GroupOrderData): void {
    this.groupReservationId.set(groupOrder.groupReservationId);
    this.snapshot.set(groupOrder.snapshot);
  }

  private createCombinedDishList(participants: GroupOrderParticipant[]): ConsolidatedDishView[] {
    const dishes = new Map<string, ConsolidatedDishView>();

    for (const participant of participants) {
      if (!participant.isSubmitted || participant.type === 'Owner') {
        continue;
      }

      for (const item of participant.items) {
        const existing = dishes.get(item.dish.id);
        if (existing) {
          existing.quantity += item.quantity;
        } else {
          dishes.set(item.dish.id, {
            id: item.dish.id,
            name: item.dish.name,
            price: item.dish.price,
            quantity: item.quantity,
          });
        }
      }
    }

    return Array.from(dishes.values()).sort((first, second) =>
      first.name.localeCompare(second.name)
    );
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) {
        return 'Could not connect to the server. Check your connection and try again.';
      }

      return error.error?.error ?? error.error?.message ?? 'The request failed. Please try again.';
    }

    return error instanceof Error ? error.message : 'Something went wrong. Please try again.';
  }
}
