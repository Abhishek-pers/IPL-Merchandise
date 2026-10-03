import { useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { storeApi } from '../api/storeApi';
import type { OrderDetails } from '../api/types';
import { ErrorBanner, Loading, Money, Pager, PriceTable } from '../components/Common';
import { useSession } from '../context/SessionContext';
import { useAsync } from '../hooks';

const dateFormat = new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' });

/** Requirement 5: order history. */
export function OrdersPage() {
  const { customerId } = useSession();
  const [page, setPage] = useState(1);
  const orders = useAsync(() => storeApi.listOrders(page), [customerId, page]);

  if (orders.loading && !orders.data) return <Loading />;
  if (!orders.data) return <ErrorBanner error={orders.error} onRetry={orders.reload} />;

  return (
    <section>
      <h1>My orders</h1>
      {orders.data.items.length === 0 ? (
        <p>
          You have not placed any orders yet. <Link to="/">Start shopping</Link>
        </p>
      ) : (
        <table className="table">
          <thead>
            <tr>
              <th>Order</th>
              <th>Placed</th>
              <th>Items</th>
              <th>Status</th>
              <th>Total</th>
            </tr>
          </thead>
          <tbody>
            {orders.data.items.map((o) => (
              <tr key={o.id}>
                <td>
                  <Link to={`/orders/${o.id}`}>{o.orderNumber}</Link>
                </td>
                <td>{dateFormat.format(new Date(o.placedAt))}</td>
                <td>{o.itemCount}</td>
                <td>{o.status}</td>
                <td>
                  <Money amount={o.total} currency={o.currency} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <Pager page={orders.data.page} totalPages={orders.data.totalPages} onChange={setPage} />
    </section>
  );
}

export function OrderDetailsPage() {
  const { orderId = '' } = useParams();
  const location = useLocation();
  const justPlaced = (location.state as { justPlaced?: boolean } | null)?.justPlaced;
  const order = useAsync(() => storeApi.getOrder(orderId), [orderId]);
  const [updated, setUpdated] = useState<OrderDetails>();
  const [error, setError] = useState<Error>();
  const [notice, setNotice] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function act(action: () => Promise<OrderDetails>, successNotice: string) {
    setBusy(true);
    setError(undefined);
    setNotice(undefined);
    try {
      setUpdated(await action());
      setNotice(successNotice);
    } catch (e) {
      setError(e as Error);
    } finally {
      setBusy(false);
    }
  }

  if (order.loading && !order.data) return <Loading />;
  if (!order.data) return <ErrorBanner error={order.error} onRetry={order.reload} />;

  const o = updated ?? order.data;
  return (
    <section>
      <Link to="/orders">← All orders</Link>
      {justPlaced && !notice && o.status === 'Placed' && (
        <div className="banner banner-ok">Your order has been placed and the stock is reserved. Complete the payment below.</div>
      )}
      {notice && <div className="banner banner-ok">{notice}</div>}
      <ErrorBanner error={error} />
      <h1>Order {o.orderNumber}</h1>
      <p className="muted">
        {dateFormat.format(new Date(o.placedAt))} · {o.status} · {o.itemCount} item(s)
      </p>
      <table className="table">
        <thead>
          <tr>
            <th>Product</th>
            <th>Franchise</th>
            <th>Unit price</th>
            <th>Qty</th>
            <th>Total</th>
          </tr>
        </thead>
        <tbody>
          {o.lines.map((l) => (
            <tr key={l.productId}>
              <td>{l.productName}</td>
              <td>{l.franchiseName}</td>
              <td>
                <Money amount={l.unitPrice} currency={o.price.currency} />
              </td>
              <td>{l.quantity}</td>
              <td>
                <Money amount={l.lineTotal} currency={o.price.currency} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="summary">
        <PriceTable price={o.price} />
        {o.status === 'Placed' && (
          <div className="payment-actions">
            <button
              type="button"
              className="primary"
              disabled={busy}
              onClick={() => act(() => storeApi.payOrder(o.id, false), 'Payment successful. Thank you for your order!')}
            >
              {busy ? 'Processing…' : 'Pay now'}
            </button>
            <button type="button" disabled={busy} onClick={() => act(() => storeApi.payOrder(o.id, true), '')}>
              Simulate failed payment
            </button>
            <button
              type="button"
              className="link-button"
              disabled={busy}
              onClick={() => act(() => storeApi.cancelOrder(o.id), 'Order cancelled. The items are back in stock.')}
            >
              Cancel order
            </button>
          </div>
        )}
      </div>
    </section>
  );
}
