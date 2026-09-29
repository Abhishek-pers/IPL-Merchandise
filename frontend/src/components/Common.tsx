import { Link, NavLink, Outlet } from 'react-router-dom';
import { formatMoney } from '../hooks';
import { useSession } from '../context/SessionContext';
import type { PriceSummary, ProductSummary } from '../api/types';

export function Layout() {
  const { customers, customerId, selectCustomer, cartCount } = useSession();

  return (
    <div className="app">
      <header className="header">
        <Link to="/" className="brand">
          IPL Fan Store
        </Link>
        <nav className="nav">
          <NavLink to="/" end>
            Products
          </NavLink>
          <NavLink to="/cart">Cart ({cartCount})</NavLink>
          <NavLink to="/orders">My orders</NavLink>
        </nav>
        <label className="customer-picker">
          Shopping as{' '}
          <select value={customerId ?? ''} onChange={(e) => selectCustomer(e.target.value)} aria-label="Customer">
            {customers.map((c) => (
              <option key={c.id} value={c.id}>
                {c.fullName}
              </option>
            ))}
          </select>
        </label>
      </header>
      <main className="main">
        <Outlet />
      </main>
    </div>
  );
}

export function Money({ amount, currency }: { amount: number; currency: string }) {
  return <span className="money">{formatMoney(amount, currency)}</span>;
}

export function ErrorBanner({ error, onRetry }: { error?: Error; onRetry?: () => void }) {
  if (!error) return null;
  return (
    <div className="banner banner-error" role="alert">
      {error.message}
      {onRetry && (
        <button type="button" className="link-button" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  );
}

export function Loading() {
  return <p className="muted">Loading…</p>;
}

/** Coloured tile standing in for a product photo (keeps the demo free of image assets). */
export function ProductTile({ product }: { product: Pick<ProductSummary, 'franchiseCode' | 'franchiseColor' | 'categoryName'> }) {
  return (
    <div className="tile" style={{ background: product.franchiseColor }}>
      <span className="tile-code">{product.franchiseCode}</span>
      <span className="tile-type">{product.categoryName}</span>
    </div>
  );
}

export function ProductCard({ product }: { product: ProductSummary }) {
  return (
    <Link to={`/products/${product.id}`} className="card">
      <ProductTile product={product} />
      <div className="card-body">
        <div className="card-title">{product.name}</div>
        <div className="muted small">{product.franchiseName}</div>
        <div className="card-footer">
          <Money amount={product.price} currency={product.currency} />
          {product.stockQuantity === 0 ? (
            <span className="stock-label sold-out">Sold out</span>
          ) : product.stockQuantity < 15 ? (
            <span className="stock-label low-stock">Only {product.stockQuantity} left</span>
          ) : (
            <span className="stock-label in-stock">In stock</span>
          )}
        </div>
      </div>
    </Link>
  );
}

export function Pager({ page, totalPages, onChange }: { page: number; totalPages: number; onChange: (page: number) => void }) {
  if (totalPages <= 1) return null;
  return (
    <div className="pager">
      <button type="button" disabled={page <= 1} onClick={() => onChange(page - 1)}>
        ‹ Previous
      </button>
      <span>
        Page {page} of {totalPages}
      </span>
      <button type="button" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>
        Next ›
      </button>
    </div>
  );
}

export function PriceTable({ price }: { price: PriceSummary }) {
  return (
    <table className="price-table">
      <tbody>
        <tr>
          <td>Subtotal</td>
          <td>
            <Money amount={price.subtotal} currency={price.currency} />
          </td>
        </tr>
        <tr>
          <td>GST</td>
          <td>
            <Money amount={price.tax} currency={price.currency} />
          </td>
        </tr>
        <tr>
          <td>Shipping</td>
          <td>{price.shipping === 0 ? 'Free' : <Money amount={price.shipping} currency={price.currency} />}</td>
        </tr>
        <tr className="total">
          <td>Total</td>
          <td>
            <Money amount={price.total} currency={price.currency} />
          </td>
        </tr>
      </tbody>
    </table>
  );
}
