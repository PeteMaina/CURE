import { useEffect, useState } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import AppShell from '../components/AppShell';
import CustomersPage from '../features/customers/CustomersPage';
import CustomerDetailPage from '../features/customers/CustomerDetailPage';
import HomePage from '../features/home/HomePage';
import InboxPage from '../features/inbox/InboxPage';
import CoreResourcePage from '../features/core/CoreResourcePage';
import LoginPage from '../features/auth/LoginPage';
import OidcCallbackPage from '../features/auth/OidcCallbackPage';
import { oidcManager } from '../services/oidc';

export default function App() {
  const location = useLocation();
  const [authState, setAuthState] = useState(
    process.env.NODE_ENV === 'development' ? 'authenticated' : 'loading',
  );

  useEffect(() => {
    if (process.env.NODE_ENV === 'development') return;
    let active = true;
    if (!oidcManager) {
      setAuthState('anonymous');
      return () => { active = false; };
    }
    oidcManager.getUser()
      .then((user) => {
        if (active) setAuthState(user && !user.expired ? 'authenticated' : 'anonymous');
      })
      .catch(() => {
        if (active) setAuthState('anonymous');
      });
    return () => { active = false; };
  }, []);

  if (location.pathname === '/login') return <LoginPage />;
  if (location.pathname === '/auth/callback') return <OidcCallbackPage />;
  if (process.env.NODE_ENV !== 'development' && authState === 'loading') {
    return <div className="login-page" aria-live="polite">Checking your sign-in...</div>;
  }
  if (process.env.NODE_ENV !== 'development' && authState !== 'authenticated') {
    return <Navigate to="/login" replace />;
  }

  return (
    <AppShell>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/customers" element={<CustomersPage />} />
        <Route path="/customers/:customerId" element={<CustomerDetailPage />} />
        <Route path="/inbox" element={<InboxPage />} />
        <Route path="/organizations" element={<CoreResourcePage resource="organizations" />} />
        <Route path="/contacts" element={<CoreResourcePage resource="contacts" />} />
        <Route path="/leads" element={<CoreResourcePage resource="leads" />} />
        <Route path="/opportunities" element={<CoreResourcePage resource="opportunities" />} />
        <Route path="/activities" element={<CoreResourcePage resource="activities" />} />
        <Route path="/cases" element={<CoreResourcePage resource="cases" />} />
        <Route path="/saved-views" element={<CoreResourcePage resource="saved-views" />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AppShell>
  );
}