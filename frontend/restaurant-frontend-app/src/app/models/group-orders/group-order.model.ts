export interface GroupOrderDish {
  id: string;
  name: string;
  price: number;
  imageUrl?: string | null;
}

export interface GroupOrderItem {
  dish: GroupOrderDish;
  quantity: number;
}

export interface GroupOrderParticipant {
  id: string;
  type: 'Owner' | 'Guest';
  name: string;
  items: GroupOrderItem[];
  total: number;
  isSubmitted: boolean;
}

export interface GroupOrderSnapshot {
  participants: GroupOrderParticipant[];
  total: number;
}

export interface GroupOrderData {
  groupReservationId: string;
  snapshot: GroupOrderSnapshot;
}

export interface ConsolidatedGroupOrderItem {
  dishId: string;
  quantity: number;
}

export interface GroupOrderInvite {
  GroupReservationId?: string;
  groupReservationId?: string;
}

export interface GroupOrderInviteToken {
  token: string;
}
