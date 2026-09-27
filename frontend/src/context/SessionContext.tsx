import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { readStoredCustomerId, storeApi, storeCustomerId } from '../api/storeApi';
import type { Cart, Customer } from '../api/types';

/**
 * Who is shopping + their cart badge. Stand-in for real authentication: the chosen demo
 * customer id is sent as X-Customer-Id on every request.
 */
interface Session {
  customers: Customer[];
  customerId: string | null;
  selectCustomer: (id: string) => void;
  cartCount: number;
  /** Pages call this with the cart returned by any cart mutation. */
  updateCart: (cart: Cart) => void;
  refreshCart: () => Promise<void>;
}

const SessionContext = createContext<Session | undefined>(undefined);

export function SessionProvider({ children }: { children: ReactNode }) {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [customerId, setCustomerId] = useState<string | null>(readStoredCustomerId());
  const [cartCount, setCartCount] = useState(0);

  const updateCart = useCallback((cart: Cart) => setCartCount(cart.totalQuantity), []);

  const refreshCart = useCallback(async () => {
    if (!readStoredCustomerId()) return;
    try {
      updateCart(await storeApi.getCart());
    } catch {
      setCartCount(0);
    }
  }, [updateCart]);

  const selectCustomer = useCallback(
    (id: string) => {
      storeCustomerId(id);
      setCustomerId(id);
      void refreshCart();
    },
    [refreshCart],
  );

  useEffect(() => {
    storeApi
      .listCustomers()
      .then((list) => {
        setCustomers(list);
        const current = readStoredCustomerId();
        if ((!current || !list.some((c) => c.id === current)) && list.length > 0) selectCustomer(list[0].id);
        else void refreshCart();
      })
      .catch(() => setCustomers([]));
  }, [selectCustomer, refreshCart]);

  const value = useMemo(
    () => ({ customers, customerId, selectCustomer, cartCount, updateCart, refreshCart }),
    [customers, customerId, selectCustomer, cartCount, updateCart, refreshCart],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): Session {
  const session = useContext(SessionContext);
  if (!session) throw new Error('useSession must be used inside <SessionProvider>');
  return session;
}
