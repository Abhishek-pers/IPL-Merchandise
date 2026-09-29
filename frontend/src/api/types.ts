// Mirrors the API's DTOs (backend/src/IplStore.Application/**/**Dtos.cs / *Contracts.cs).

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
}

export interface ProductSummary {
  id: string;
  sku: string;
  name: string;
  price: number;
  currency: string;
  inStock: boolean;
  stockQuantity: number;
  franchiseCode: string;
  franchiseName: string;
  franchiseColor: string;
  categoryCode: string;
  categoryName: string;
  imageUrl: string | null;
}

export interface ProductDetails extends ProductSummary {
  description: string;
  stockQuantity: number;
  attributes: Record<string, unknown>;
}

export interface Franchise {
  id: string;
  code: string;
  name: string;
  city: string;
  primaryColor: string;
}

export interface Category {
  id: string;
  code: string;
  name: string;
}

export interface Customer {
  id: string;
  fullName: string;
  email: string;
}

export interface PriceSummary {
  subtotal: number;
  tax: number;
  shipping: number;
  total: number;
  currency: string;
}

export interface CartLine {
  productId: string;
  sku: string;
  productName: string;
  franchiseCode: string;
  franchiseName: string;
  categoryName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  isAvailable: boolean;
  availableStock: number;
}

export interface Cart {
  cartId: string | null;
  lines: CartLine[];
  totalQuantity: number;
  price: PriceSummary;
  updatedAt: string | null;
}

export type OrderStatus = 'Placed' | 'Paid' | 'Shipped' | 'Delivered' | 'Cancelled';

export interface OrderSummary {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  placedAt: string;
  itemCount: number;
  total: number;
  currency: string;
}

export interface OrderLine {
  productId: string;
  sku: string;
  productName: string;
  franchiseName: string;
  categoryName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderDetails {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  placedAt: string;
  itemCount: number;
  price: PriceSummary;
  lines: OrderLine[];
}

export type ProductSort = 'Name' | 'PriceLowToHigh' | 'PriceHighToLow' | 'Newest';

/** Search parameters as one object: adding a filter never changes a function signature. */
export interface ProductSearch {
  search?: string;
  franchise?: string[];
  category?: string[];
  minPrice?: number;
  maxPrice?: number;
  inStockOnly?: boolean;
  sort?: ProductSort;
  page?: number;
  pageSize?: number;
}
