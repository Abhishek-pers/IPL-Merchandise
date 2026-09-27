import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { Layout } from './components/Common';
import { SessionProvider } from './context/SessionContext';
import { CartPage } from './pages/CartPage';
import { OrderDetailsPage, OrdersPage } from './pages/OrderPages';
import { ProductDetailsPage } from './pages/ProductDetailsPage';
import { ProductListPage } from './pages/ProductListPage';
import './styles.css';

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: '/', element: <ProductListPage /> },
      { path: '/products/:productId', element: <ProductDetailsPage /> },
      { path: '/cart', element: <CartPage /> },
      { path: '/orders', element: <OrdersPage /> },
      { path: '/orders/:orderId', element: <OrderDetailsPage /> },
      { path: '*', element: <p>Page not found.</p> },
    ],
  },
]);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <SessionProvider>
      <RouterProvider router={router} />
    </SessionProvider>
  </StrictMode>,
);
