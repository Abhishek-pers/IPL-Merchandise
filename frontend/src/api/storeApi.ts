import { appConfig } from '../config';
import { createHttpClient, type HttpClient } from './httpClient';
import type {
  Cart,
  Category,
  Customer,
  Franchise,
  OrderDetails,
  OrderSummary,
  PagedResult,
  ProductDetails,
  ProductSearch,
  ProductSummary,
} from './types';

/** One method per API endpoint. Pages depend on this, never on fetch directly. */
export function createStoreApi(http: HttpClient) {
  return {
    searchProducts: (search: ProductSearch) =>
      http.get<PagedResult<ProductSummary>>('/products', { ...search }),
    getProduct: (id: string) => http.get<ProductDetails>(`/products/${id}`),
    listFranchises: () => http.get<Franchise[]>('/franchises'),
    listCategories: () => http.get<Category[]>('/categories'),
    listCustomers: () => http.get<Customer[]>('/customers'),

    getCart: () => http.get<Cart>('/cart'),
    /** Idempotent add: one key per click, reused when retrying, so a lost response never adds twice. */
    addToCart: (productId: string, quantity: number, idempotencyKey: string) =>
      http.post<Cart>('/cart/items', { productId, quantity }, idempotencyKey).then((r) => r.data),
    setCartQuantity: (productId: string, quantity: number) =>
      http.put<Cart>(`/cart/items/${productId}`, { quantity }),
    removeFromCart: (productId: string) => http.delete<Cart>(`/cart/items/${productId}`),

    /** Idempotent checkout: reuse the same key when retrying the same attempt. */
    placeOrder: async (idempotencyKey: string) => {
      const response = await http.post<OrderDetails>('/orders', undefined, idempotencyKey);
      return { order: response.data, replayed: response.headers.get('Idempotent-Replayed') === 'true' };
    },
    listOrders: (page = 1) => http.get<PagedResult<OrderSummary>>('/orders', { page, pageSize: 10 }),
    getOrder: (id: string) => http.get<OrderDetails>(`/orders/${id}`),
  };
}

export type StoreApi = ReturnType<typeof createStoreApi>;

const CUSTOMER_STORAGE_KEY = 'iplstore.customerId';

export function readStoredCustomerId(): string | null {
  try {
    return localStorage.getItem(CUSTOMER_STORAGE_KEY);
  } catch {
    return null;
  }
}

export function storeCustomerId(id: string): void {
  try {
    localStorage.setItem(CUSTOMER_STORAGE_KEY, id);
  } catch {
    // Private mode / storage disabled: the choice simply isn't remembered.
  }
}

/** Default singleton wired to the real backend. */
export const storeApi = createStoreApi(
  createHttpClient({
    baseUrl: appConfig.apiBaseUrl,
    retry: appConfig.retry,
    getCustomerId: readStoredCustomerId,
  }),
);
