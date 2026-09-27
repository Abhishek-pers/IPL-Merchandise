import { useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { storeApi } from '../api/storeApi';
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

  if (order.loading && !order.data) return <Loading />;
  if (!order.data) return <ErrorBanner error={order.error} onRetry={order.reload} />;

  const o = order.data;
  return (
    <section>
      <Link to="/orders">← All orders</Link>
      {justPlaced && <div className="banner banner-ok">Thank you! Your order has been placed.</div>}
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
      </div>
    </section>
  );
}
