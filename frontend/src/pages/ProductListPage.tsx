import { useSearchParams } from 'react-router-dom';
import { storeApi } from '../api/storeApi';
import type { ProductSearch, ProductSort } from '../api/types';
import { ErrorBanner, Loading, Pager, ProductCard } from '../components/Common';
import { appConfig } from '../config';
import { useAsync } from '../hooks';

const SORTS: { value: ProductSort; label: string }[] = [
  { value: 'Name', label: 'Name' },
  { value: 'PriceLowToHigh', label: 'Price: low to high' },
  { value: 'PriceHighToLow', label: 'Price: high to low' },
  { value: 'Newest', label: 'Newest' },
];

/** Requirement 1 + 3: product list with search by name, type and franchise. Filters live in the URL. */
export function ProductListPage() {
  const [params, setParams] = useSearchParams();
  const search: ProductSearch = {
    search: params.get('search') ?? undefined,
    franchise: params.getAll('franchise'),
    category: params.getAll('category'),
    inStockOnly: params.get('inStockOnly') === 'true' || undefined,
    sort: (params.get('sort') as ProductSort | null) ?? 'Name',
    page: Number(params.get('page') ?? 1),
    pageSize: appConfig.pageSize,
  };

  const franchises = useAsync(() => storeApi.listFranchises(), []);
  const categories = useAsync(() => storeApi.listCategories(), []);
  const products = useAsync(() => storeApi.searchProducts(search), [params.toString()]);

  function update(key: string, value: string | undefined) {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    if (key !== 'page') next.delete('page');
    setParams(next);
  }

  return (
    <section>
      <h1>Official IPL merchandise</h1>

      {/* Team buttons: one click filters by a franchise; clicking it again clears the filter. */}
      <nav className="team-shortcuts" aria-label="Browse by franchise">
        <button
          type="button"
          className={!search.franchise?.length ? 'team-shortcut active' : 'team-shortcut'}
          aria-pressed={!search.franchise?.length}
          onClick={() => update('franchise', undefined)}
        >
          All teams
        </button>
        {franchises.data?.map((franchise) => {
          const selected = search.franchise?.includes(franchise.code) ?? false;
          return (
            <button
              key={franchise.id}
              type="button"
              className={selected ? 'team-shortcut active' : 'team-shortcut'}
              aria-pressed={selected}
              onClick={() => update('franchise', selected ? undefined : franchise.code)}
            >
              <span
                className="team-swatch"
                style={{ backgroundColor: franchise.primaryColor }}
                aria-hidden="true"
              />
              {franchise.name}
            </button>
          );
        })}
      </nav>

      {/* Filter bar. Every control writes to the URL, which triggers a new search. */}
      <form
        className="filters"
        onSubmit={(e) => {
          e.preventDefault();
          const text = String(new FormData(e.currentTarget).get('search') ?? '').trim();
          update('search', text || undefined);
        }}
      >
        {/* Free-text search: applied on submit (Enter or the button), not on every keystroke. */}
        <input
          name="search"
          type="search"
          placeholder="Search jerseys, caps, teams…"
          defaultValue={search.search}
          aria-label="Search"
        />
        <button type="submit">Search</button>

        <select
          value={search.franchise?.[0] ?? ''}
          onChange={(e) => update('franchise', e.target.value || undefined)}
          aria-label="Franchise"
        >
          <option value="">All franchises</option>
          {franchises.data?.map((f) => (
            <option key={f.code} value={f.code}>
              {f.name}
            </option>
          ))}
        </select>

        <select
          value={search.category?.[0] ?? ''}
          onChange={(e) => update('category', e.target.value || undefined)}
          aria-label="Product type"
        >
          <option value="">All types</option>
          {categories.data?.map((c) => (
            <option key={c.code} value={c.code}>
              {c.name}
            </option>
          ))}
        </select>

        <select value={search.sort} onChange={(e) => update('sort', e.target.value)} aria-label="Sort">
          {SORTS.map((s) => (
            <option key={s.value} value={s.value}>
              {s.label}
            </option>
          ))}
        </select>

        <label className="checkbox">
          <input
            type="checkbox"
            checked={Boolean(search.inStockOnly)}
            onChange={(e) => update('inStockOnly', e.target.checked ? 'true' : undefined)}
          />
          In stock only
        </label>
      </form>

      {/* Results: error with retry, spinner on first load, then the product grid and pager. */}
      <ErrorBanner error={products.error} onRetry={products.reload} />
      {products.loading && !products.data && <Loading />}

      {products.data && (
        <>
          <p className="muted">{products.data.totalCount} products</p>
          <div className="grid">
            {products.data.items.map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
          {products.data.items.length === 0 && <p>No products match your search.</p>}
          <Pager page={products.data.page} totalPages={products.data.totalPages} onChange={(p) => update('page', String(p))} />
        </>
      )}
    </section>
  );
}
