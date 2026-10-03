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

/**
 * The store's REST API as plain functions: one function per backend endpoint.
 *
 * Pages call these functions and never call `fetch` directly, so URLs, headers and
 * retries live in one place. Paths are relative to `appConfig.apiBaseUrl` (`/api/v1`).
 *
 * `http.get` / `put` / `delete` return the response body. `http.post` returns the whole
 * response (body + headers), so the POST functions below unwrap `.data` themselves,
 * except `placeOrder`, which also needs a response header.
 */
export function createStoreApi(http: HttpClient) {
  return {
    // ---------------------------------------------------------------------------------
    // Catalogue (read only)
    // ---------------------------------------------------------------------------------

    /** GET /products - one page of products matching search text, filters and sort. */
    searchProducts: (search: ProductSearch) =>
      http.get<PagedResult<ProductSummary>>('/products', { ...search }),

    /** GET /products/{id} - full details of one product (description, sizes, stock). */
    getProduct: (id: string) =>
      http.get<ProductDetails>(`/products/${id}`),

    /** GET /franchises - all franchises, for the team buttons and the franchise filter. */
    listFranchises: () =>
      http.get<Franchise[]>('/franchises'),

    /** GET /categories - all product types (Jersey, Cap ...), for the type filter. */
    listCategories: () =>
      http.get<Category[]>('/categories'),

    /** GET /customers - the demo shoppers for the "Shopping as" picker (stand-in for login). */
    listCustomers: () =>
      http.get<Customer[]>('/customers'),

    // ---------------------------------------------------------------------------------
    // Cart (for the shopper in the X-Customer-Id header)
    // ---------------------------------------------------------------------------------

    /** GET /cart - the current cart with live prices and totals. */
    getCart: () =>
      http.get<Cart>('/cart'),

    /**
     * POST /cart/items - adds `quantity` units of a product.
     * Send one idempotency key per click and reuse it when retrying, so a lost response
     * never adds the item twice.
     */
    addToCart: (productId: string, quantity: number, idempotencyKey: string) =>
      http
        .post<Cart>('/cart/items', { productId, quantity }, idempotencyKey)
        .then((response) => response.data),

    /** PUT /cart/items/{productId} - sets an absolute quantity; 0 removes the line. */
    setCartQuantity: (productId: string, quantity: number) =>
      http.put<Cart>(`/cart/items/${productId}`, { quantity }),

    /** DELETE /cart/items/{productId} - removes the product from the cart. */
    removeFromCart: (productId: string) =>
      http.delete<Cart>(`/cart/items/${productId}`),

    // ---------------------------------------------------------------------------------
    // Orders, payment and history
    // ---------------------------------------------------------------------------------

    /**
     * POST /orders - checkout: turns the cart into an order and reserves the stock.
     * Reuse the same idempotency key when retrying the same attempt. `replayed` is true
     * when the server returned an order it had already created for that key.
     */
    placeOrder: async (idempotencyKey: string) => {
      const response = await http.post<OrderDetails>('/orders', undefined, idempotencyKey);
      return {
        order: response.data,
        replayed: response.headers.get('Idempotent-Replayed') === 'true',
      };
    },

    /** GET /orders - the shopper's order history, newest first, 10 per page. */
    listOrders: (page = 1) =>
      http.get<PagedResult<OrderSummary>>('/orders', { page, pageSize: 10 }),

    /** GET /orders/{id} - one order with its lines and price breakdown. */
    getOrder: (id: string) =>
      http.get<OrderDetails>(`/orders/${id}`),

    /**
     * POST /orders/{id}/payment - pays through the dummy gateway.
     * `simulateFailure: true` makes the gateway decline; the order stays Placed and can be
     * retried. Paying an order that is already paid never charges it twice.
     */
    payOrder: (id: string, simulateFailure: boolean) =>
      http
        .post<OrderDetails>(`/orders/${id}/payment`, { simulateFailure })
        .then((response) => response.data),

    /** POST /orders/{id}/cancel - cancels an unpaid order and puts its stock back. */
    cancelOrder: (id: string) =>
      http
        .post<OrderDetails>(`/orders/${id}/cancel`)
        .then((response) => response.data),
  };
}

export type StoreApi = ReturnType<typeof createStoreApi>;

// -------------------------------------------------------------------------------------
// Remembered shopper
// -------------------------------------------------------------------------------------

/** localStorage key holding the id of the demo shopper picked in the header. */
const CUSTOMER_STORAGE_KEY = 'iplstore.customerId';

/** The remembered shopper id, or null if none was chosen or storage is unavailable. */
export function readStoredCustomerId(): string | null {
  try {
    return localStorage.getItem(CUSTOMER_STORAGE_KEY);
  } catch {
    return null;
  }
}

/** Remembers the chosen shopper so a page reload keeps the same cart and orders. */
export function storeCustomerId(id: string): void {
  try {
    localStorage.setItem(CUSTOMER_STORAGE_KEY, id);
  } catch {
    // Private mode / storage disabled: the choice simply isn't remembered.
  }
}

// -------------------------------------------------------------------------------------
// Default instance
// -------------------------------------------------------------------------------------

/**
 * The API instance the pages import. Every request gets the base URL and retry policy
 * from `config.ts`, and the remembered shopper id as the X-Customer-Id header.
 * Tests build their own instance with `createStoreApi(fakeHttpClient)`.
 */
export const storeApi = createStoreApi(
  createHttpClient({
    baseUrl: appConfig.apiBaseUrl,
    retry: appConfig.retry,
    getCustomerId: readStoredCustomerId,
  }),
);
