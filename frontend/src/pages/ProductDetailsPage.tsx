import { useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../api/httpClient';
import { storeApi } from '../api/storeApi';
import { ErrorBanner, Loading, Money, ProductTile } from '../components/Common';
import { useSession } from '../context/SessionContext';
import { useAsync } from '../hooks';

/** Requirement 2: product details + add to cart. */
export function ProductDetailsPage() {
  const { productId = '' } = useParams();
  const { updateCart } = useSession();
  const product = useAsync(() => storeApi.getProduct(productId), [productId]);
  const [quantity, setQuantity] = useState(1);
  const [message, setMessage] = useState<string>();
  const [error, setError] = useState<Error>();
  const [busy, setBusy] = useState(false);
  // One key per "add" intent. Kept after a failure, so clicking again after a network error
  // repeats the SAME request (the server adds at most once). Cleared on success.
  const pendingAdd = useRef<{ productId: string; quantity: number; key: string }>();

  async function addToCart() {
    setBusy(true);
    setMessage(undefined);
    setError(undefined);
    const current = pendingAdd.current;
    if (!current || current.productId !== productId || current.quantity !== quantity) {
      pendingAdd.current = { productId, quantity, key: crypto.randomUUID() };
    }
    try {
      const cart = await storeApi.addToCart(productId, quantity, pendingAdd.current!.key);
      pendingAdd.current = undefined;
      updateCart(cart);
      setMessage(`Added ${quantity} to your cart.`);
    } catch (e) {
      setError(e instanceof ApiError ? e : new Error('Could not add to cart.'));
    } finally {
      setBusy(false);
    }
  }

  if (product.loading && !product.data) return <Loading />;
  if (product.error || !product.data) return <ErrorBanner error={product.error ?? new Error('Product not found.')} />;

  const p = product.data;
  return (
    <section className="details">
      <Link to="/">← Back to products</Link>
      <div className="details-layout">
        <ProductTile product={p} />
        <div>
          <h1>{p.name}</h1>
          <p className="muted">
            {p.franchiseName} · {p.categoryName} · SKU {p.sku}
          </p>
          <p className="price-large">
            <Money amount={p.price} currency={p.currency} />
          </p>
          <p>{p.description}</p>

          {Object.keys(p.attributes).length > 0 && (
            <table className="attributes">
              <tbody>
                {Object.entries(p.attributes).map(([key, value]) => (
                  <tr key={key}>
                    <th>{key.replace(/_/g, ' ')}</th>
                    <td>{Array.isArray(value) ? value.join(', ') : String(value)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <p className={p.inStock ? 'in-stock' : 'badge'}>{p.inStock ? `${p.stockQuantity} in stock` : 'Sold out'}</p>

          <div className="add-to-cart">
            <input
              type="number"
              min={1}
              max={Math.max(1, p.stockQuantity)}
              value={quantity}
              onChange={(e) => setQuantity(Math.max(1, Number(e.target.value)))}
              aria-label="Quantity"
            />
            <button type="button" onClick={addToCart} disabled={!p.inStock || busy}>
              {busy ? 'Adding…' : 'Add to cart'}
            </button>
          </div>
          {message && (
            <div className="banner banner-ok">
              {message} <Link to="/cart">View cart</Link>
            </div>
          )}
          <ErrorBanner error={error} />
        </div>
      </div>
    </section>
  );
}
