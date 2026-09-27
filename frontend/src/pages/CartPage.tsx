import { useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { storeApi } from '../api/storeApi';
import type { Cart } from '../api/types';
import { ErrorBanner, Loading, Money, PriceTable } from '../components/Common';
import { useSession } from '../context/SessionContext';
import { useAsync } from '../hooks';

/** Requirement 4: the cart, quantity changes and idempotent checkout. */
export function CartPage() {
  const { customerId, updateCart } = useSession();
  const navigate = useNavigate();
  const loaded = useAsync(() => storeApi.getCart(), [customerId]);
  const [cart, setCart] = useState<Cart>();
  const [error, setError] = useState<Error>();
  const [busy, setBusy] = useState(false);

  // One key per checkout ATTEMPT. Kept across retries (so a retry after a timeout can't create a
  // second order), discarded after success or when the cart changes.
  const checkoutKey = useRef<string>();

  const current = cart ?? loaded.data;

  async function mutate(action: () => Promise<Cart>) {
    setBusy(true);
    setError(undefined);
    try {
      const updated = await action();
      checkoutKey.current = undefined;
      setCart(updated);
      updateCart(updated);
    } catch (e) {
      setError(e as Error);
    } finally {
      setBusy(false);
    }
  }

  async function checkout() {
    const idempotencyKey = (checkoutKey.current ??= crypto.randomUUID());
    setBusy(true);
    setError(undefined);
    try {
      const { order } = await storeApi.placeOrder(idempotencyKey);
      checkoutKey.current = undefined;
      updateCart({ ...current!, lines: [], totalQuantity: 0 });
      navigate(`/orders/${order.id}`, { state: { justPlaced: true } });
    } catch (e) {
      setError(e as Error);
    } finally {
      setBusy(false);
    }
  }

  if (loaded.loading && !current) return <Loading />;
  if (!current) return <ErrorBanner error={loaded.error} onRetry={loaded.reload} />;

  if (current.lines.length === 0) {
    return (
      <section>
        <h1>Your cart</h1>
        <p>
          Your cart is empty. <Link to="/">Browse products</Link>
        </p>
      </section>
    );
  }

  return (
    <section>
      <h1>Your cart</h1>
      <ErrorBanner error={error} />
      <table className="table">
        <thead>
          <tr>
            <th>Product</th>
            <th>Price</th>
            <th>Quantity</th>
            <th>Total</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {current.lines.map((line) => (
            <tr key={line.productId}>
              <td>
                <Link to={`/products/${line.productId}`}>{line.productName}</Link>
                {!line.isAvailable && <div className="badge">Only {line.availableStock} left</div>}
              </td>
              <td>
                <Money amount={line.unitPrice} currency={current.price.currency} />
              </td>
              <td className="qty">
                <button type="button" disabled={busy} onClick={() => mutate(() => storeApi.setCartQuantity(line.productId, line.quantity - 1))}>
                  −
                </button>
                <span>{line.quantity}</span>
                <button type="button" disabled={busy} onClick={() => mutate(() => storeApi.setCartQuantity(line.productId, line.quantity + 1))}>
                  +
                </button>
              </td>
              <td>
                <Money amount={line.lineTotal} currency={current.price.currency} />
              </td>
              <td>
                <button type="button" className="link-button" disabled={busy} onClick={() => mutate(() => storeApi.removeFromCart(line.productId))}>
                  Remove
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="summary">
        <PriceTable price={current.price} />
        <button type="button" className="primary" onClick={checkout} disabled={busy}>
          {busy ? 'Placing order…' : 'Place order'}
        </button>
      </div>
    </section>
  );
}
