import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { SchoolInfoProvider } from './context/SchoolInfoContext';
import AppRoutes from './routes/AppRoutes';

export default function App() {
  return (
    <BrowserRouter>
      <SchoolInfoProvider>
        <AuthProvider>
          <AppRoutes />
        </AuthProvider>
      </SchoolInfoProvider>
    </BrowserRouter>
  );
}
