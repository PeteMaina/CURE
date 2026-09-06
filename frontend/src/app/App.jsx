import { Navigate, Route, Routes } from 'react-router-dom';
import AppShell from '../components/AppShell';
import CustomersPage from '../features/customers/CustomersPage';
import HomePage from '../features/home/HomePage';
import InboxPage from '../features/inbox/InboxPage';

export default function App() {
  return (
    <AppShell>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/customers" element={<CustomersPage />} />
        <Route path="/inbox" element={<InboxPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AppShell>
  );
}