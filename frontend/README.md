# frontend/ – React + TypeScript + Vite

Plain on purpose (the brief de-emphasises aesthetics). Structure:

```
src/
  api/httpClient.ts   fetch wrapper: safe retries (idempotent calls only), ProblemDetails -> ApiError
  api/storeApi.ts     one function per API endpoint
  api/types.ts        DTO types mirroring the backend
  config.ts           every client tunable (API base URL, page size, retry policy)
  context/            current demo customer + cart badge
  pages/              ProductList (search/filters in the URL), ProductDetails, Cart (idempotent checkout), Orders
  components/Common   layout, cards, pager, price table
```

```bash
npm install
npm run dev        # http://localhost:5173, proxies /api to http://localhost:5080
npm test           # vitest (http client retry/idempotency rules)
npm run build      # typecheck + production bundle in dist/
```
