import { lazy, Suspense, useEffect, useState } from "react";
import { BrowserRouter, Navigate, Route, Routes, useNavigate } from "react-router-dom";
import { ProtectedRoute } from "./components/ProtectedRoute.tsx";
import { clearSession, isLoggedIn, startIdleWatcher } from "./auth/session.ts";
import { Loading } from "./components/ui.tsx";

const LoginPage = lazy(() => import("./pages/LoginPage.tsx").then((m) => ({ default: m.LoginPage })));
const SignupPage = lazy(() => import("./pages/SignupPage.tsx").then((m) => ({ default: m.SignupPage })));
const HomePage = lazy(() => import("./pages/HomePage.tsx").then((m) => ({ default: m.HomePage })));
const WalletsPage = lazy(() => import("./pages/WalletsPage.tsx").then((m) => ({ default: m.WalletsPage })));
const WalletDetailPage = lazy(() => import("./pages/WalletDetailPage.tsx").then((m) => ({ default: m.WalletDetailPage })));
const FundPage = lazy(() => import("./pages/FundPage.tsx").then((m) => ({ default: m.FundPage })));
const TransferPage = lazy(() => import("./pages/TransferPage.tsx").then((m) => ({ default: m.TransferPage })));
const CheckoutPage = lazy(() => import("./pages/CheckoutPage.tsx").then((m) => ({ default: m.CheckoutPage })));
const RefundPage = lazy(() => import("./pages/RefundPage.tsx").then((m) => ({ default: m.RefundPage })));

function IdleGuard() {
  const navigate = useNavigate();
  const [warning, setWarning] = useState(false);
  const [idleMessage, setIdleMessage] = useState("");

  useEffect(() => {
    if (!isLoggedIn()) return;
    return startIdleWatcher(
      () => {
        clearSession();
        setWarning(false);
        setIdleMessage("You were logged out after 30 minutes of inactivity.");
        navigate("/login");
      },
      () => setWarning(true),
    );
  }, [navigate]);

  const stay = () => {
    setWarning(false);
    setIdleMessage("");
  };

  return (
    <>
      {warning && (
        <div className="banner warn" role="alert">
          You will be logged out soon from inactivity.
          <button type="button" className="link" onClick={stay}>
            Stay logged in
          </button>
        </div>
      )}
      {idleMessage && (
        <div className="banner warn" role="status">
          {idleMessage}
        </div>
      )}
    </>
  );
}

export function App() {
  return (
    <BrowserRouter>
      <IdleGuard />
      <Suspense
        fallback={
          <div className="page">
            <Loading />
          </div>
        }
      >
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/signup" element={<SignupPage />} />
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <HomePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/wallets"
            element={
              <ProtectedRoute>
                <WalletsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/wallets/:id"
            element={
              <ProtectedRoute>
                <WalletDetailPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/fund"
            element={
              <ProtectedRoute>
                <FundPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/transfer"
            element={
              <ProtectedRoute>
                <TransferPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/checkout"
            element={
              <ProtectedRoute>
                <CheckoutPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/refund"
            element={
              <ProtectedRoute>
                <RefundPage />
              </ProtectedRoute>
            }
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  );
}
