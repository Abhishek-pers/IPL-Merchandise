# 15. UI layout changes: where each one lives

The key fact first: **"My orders" is its own page** (route `/orders`), not a section on the cart page. So "move My orders below or beside the cart" means **embedding** the orders component in the cart page. Below is the wiring, then each change with exact files and lines (all in `frontend/src`).

## How the UI is wired (find things fast)

```
main.tsx            routes:  /  /products/:id  /cart  /orders  /orders/:id
 └─ Layout          (components/Common.tsx)  header + nav + <Outlet/> (page goes here)
     ├─ CartPage        pages/CartPage.tsx      table of lines + .summary (totals + Place order)
     ├─ OrdersPage      pages/OrderPages.tsx    "My orders" table
     └─ OrderDetailsPage pages/OrderPages.tsx   lines + .summary (totals + Pay / Fail / Cancel)
styles.css          every layout class (.nav, .main, .summary, .table, …)
```
- Routes: [main.tsx:12-24](../frontend/src/main.tsx#L12-L24)
- Header and nav: [Common.tsx:6-38](../frontend/src/components/Common.tsx#L6-L38)
- **Rule of thumb:** the order of elements is set in the **`.tsx`**; their arrangement (side by side, spacing, alignment) is set in **`styles.css`**.

---

## 1. Move the "My orders" section above or below another section

**Which part changes:** the parent page's JSX. Because orders is a separate page, you render the `OrdersPage` component **inside** `CartPage` at the position you want.

**Steps:**
1. In [CartPage.tsx](../frontend/src/pages/CartPage.tsx), import it at the top:
   ```tsx
   import { OrdersPage } from './OrderPages';
   ```
2. **Below the cart:** add it after the summary, before `</section>` at [line 138](../frontend/src/pages/CartPage.tsx#L138):
   ```tsx
         </div>          {/* end of .summary */}
         <OrdersPage />  {/* ← add */}
       </section>
   ```
   **Above the cart:** put `<OrdersPage />` right after `<h1>Your cart</h1>` at [line 71](../frontend/src/pages/CartPage.tsx#L71) instead.
3. **The edge case to remember:** an **empty cart returns early** ([lines 58-67](../frontend/src/pages/CartPage.tsx#L58-L67)). Add `<OrdersPage />` there too, or the orders vanish when the cart is empty.
4. Optional: the embedded section shows a second `<h1>My orders</h1>` ([OrderPages.tsx:22](../frontend/src/pages/OrderPages.tsx#L22)). Change it to `<h2>` if they comment on heading levels.

**Check:** `/cart` with items, then empty the cart. Orders show in both cases. Switch shopper in the header and the orders list changes.

## 2. Move "My orders" from below the cart to beside it

**Which part changes:** the parent layout (a wrapper in `CartPage`) plus CSS for two columns that stack on small screens.

**Steps:**
1. In [CartPage.tsx:69-139](../frontend/src/pages/CartPage.tsx#L69-L139), wrap the cart content and the orders in a grid:
   ```tsx
   return (
     <div className="cart-layout">
       <section>
         <h1>Your cart</h1>
         {/* …existing table and .summary unchanged… */}
       </section>
       <aside>
         <OrdersPage />
       </aside>
     </div>
   );
   ```
   Do the same in the **empty-cart early return** ([lines 58-67](../frontend/src/pages/CartPage.tsx#L58-L67)), or the orders column disappears when the cart is empty.
2. In [styles.css](../frontend/src/styles.css), add CSS. It copies the existing `.details-layout` pattern at [lines 56-57](../frontend/src/styles.css#L56-L57):
   ```css
   .cart-layout { display: grid; grid-template-columns: 2fr 1fr; gap: 2rem; align-items: start; }
   @media (max-width: 900px) { .cart-layout { grid-template-columns: 1fr; } }
   ```
   - `2fr 1fr` gives the cart about two-thirds of the width; swap to `1fr 2fr` to make orders wider.
   - **Orders on the left:** put `<aside>` before `<section>` in the JSX, or add `.cart-layout aside { order: -1; }`.
3. Optional: give more room with `.main { max-width: 1100px }` at [line 30](../frontend/src/styles.css#L30) → `1300px`. The orders table has 5 columns and gets cramped in a third of 1100px.

**Check:** side by side on a wide window; narrow the window (or open dev tools device mode) and it stacks.

## 3. Move the "My orders" navigation link

**Which part changes:** the nav component. The links are written in order in `Layout`.

**Steps:**
1. [Common.tsx:15-21](../frontend/src/components/Common.tsx#L15-L21):
   ```tsx
   <nav className="nav">
     <NavLink to="/" end>Products</NavLink>
     <NavLink to="/orders">My orders</NavLink>       {/* ← moved before Cart */}
     <NavLink to="/cart">Cart ({cartCount})</NavLink>
   </nav>
   ```
2. **Move the whole nav to the right of the "Shopping as" picker:** put the `<nav>` block after the `<label className="customer-picker">` block ([lines 22-31](../frontend/src/components/Common.tsx#L22-L31)), or use CSS only: `.customer-picker { order: -1; }`.
3. **Push the links to the far right:** in [styles.css:27](../frontend/src/styles.css#L27) change `.nav { … flex: 1; }` to `.nav { … flex: 1; justify-content: flex-end; }`.

The routes in `main.tsx` don't change. Only the link's position does.

## 4. Rearrange fields inside an order (the orders list or order details)

**Which part changes:** the table markup. Orders are shown as **table rows**, not cards, so you must reorder the **header cells and the data cells identically**, or the columns won't line up.

**My orders list** ([OrderPages.tsx:28-53](../frontend/src/pages/OrderPages.tsx#L28-L53)). Example: show Status first and Total before Items.
1. Headers at [lines 31-35](../frontend/src/pages/OrderPages.tsx#L31-L35): `Status, Order, Placed, Total, Items`
2. Cells at [lines 41-49](../frontend/src/pages/OrderPages.tsx#L41-L49), **in exactly the same order**:
   ```tsx
   <td>{o.status}</td>
   <td><Link to={`/orders/${o.id}`}>{o.orderNumber}</Link></td>
   <td>{dateFormat.format(new Date(o.placedAt))}</td>
   <td><Money amount={o.total} currency={o.currency} /></td>
   <td>{o.itemCount}</td>
   ```

**Order details**: line items at [lines 100-125](../frontend/src/pages/OrderPages.tsx#L100-L125) (same rule). Payment buttons order at [lines 129-149](../frontend/src/pages/OrderPages.tsx#L129-L149): move the `<button>` blocks.

**If they literally ask for "order cards":** replace the `<table>` with a `.grid` of `<div className="card">` per order. The `.grid` and `.card` classes already exist ([styles.css:41-46](../frontend/src/styles.css#L41-L46)), so it's mostly markup.

## 5. Change spacing, alignment or size

**Which part changes:** `styles.css` only. Find the class on the element, then edit its rule.

| Element | Class | Rule | Typical change |
|---|---|---|---|
| Page width and padding | `.main` | [styles.css:30](../frontend/src/styles.css#L30) | `max-width`, `padding` |
| Header and nav spacing | `.header`, `.nav` | [L25](../frontend/src/styles.css#L25), [L27](../frontend/src/styles.css#L27) | `gap`, `justify-content` |
| Cart and order totals block | `.summary` | [L67](../frontend/src/styles.css#L67) | `align-items: flex-end` → `flex-start` (left-align), `gap`, `margin-top` |
| Pay / Fail / Cancel buttons | `.payment-actions` | [L68](../frontend/src/styles.css#L68) | `justify-content`, `gap` |
| Table cell spacing | `.table th, .table td` | [L64](../frontend/src/styles.css#L64) | `padding` |
| Page titles | `h1` | [L19](../frontend/src/styles.css#L19) | `font-size`, `margin` |
| Product grid | `.grid` | [L41](../frontend/src/styles.css#L41) | `minmax(200px, 1fr)` → `250px` (bigger cards), `gap` |
| Primary button size | `button.primary` | [L23](../frontend/src/styles.css#L23) | `padding` |

**Example: totals on the left instead of the right**
```css
.summary { …; align-items: flex-start; }
.payment-actions { …; justify-content: flex-start; }
```

## Related likely request: cart totals above the line items
In [CartPage.tsx](../frontend/src/pages/CartPage.tsx), cut the comment and `<div className="summary">…</div>` block ([lines 131-137](../frontend/src/pages/CartPage.tsx#L131-L137)) and paste it **above** the table comment at [line 74](../frontend/src/pages/CartPage.tsx#L74) (the `<table>` itself is line 75). Same approach for order details ([OrderPages.tsx:126-151](../frontend/src/pages/OrderPages.tsx#L126-L151) above [line 100](../frontend/src/pages/OrderPages.tsx#L100)).

## Verify every UI change the same way
```powershell
cd frontend
npm run dev          # http://localhost:5173 (API on :5080), Vite reloads on save
npm run typecheck    # catches a missing import or a typo in JSX
```
Then check: wide window, a narrow window (it stacks), and one edge case (empty cart, a customer with no orders, a Placed vs Paid order). If they want it live on Azure: commit and push to `main` (CD deploys it), or run `.\scripts\deploy.ps1`.

**What to say before each change:** "Order is in the JSX, arrangement is in the CSS. This is a layout change, so it's the `CartPage` JSX plus a grid rule in `styles.css`." It shows the panel you know where things live before you start typing.
