import { useEffect, useRef } from "react";
import type { ReactNode } from "react";
import { NavLink, useNavigate } from "react-router-dom";
import { clearSession } from "../auth/session.ts";
import { formatMoney } from "../money.ts";

function svgProps(size = 22) {
  return {
    width: size,
    height: size,
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 1.8,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    "aria-hidden": true,
  };
}

export function IconHome({ size }: { size?: number }) {
  return (
    <svg {...svgProps(size)}>
      <path d="M3 10.5 12 3l9 7.5" />
      <path d="M5 9.5V21h14V9.5" />
    </svg>
  );
}

export function IconWallets({ size }: { size?: number }) {
  return (
    <svg {...svgProps(size)}>
      <rect x="3" y="6" width="18" height="13" rx="2.5" />
      <path d="M3 10h18" />
      <circle cx="16.5" cy="14.5" r="1.2" />
    </svg>
  );
}

export function IconSend({ size }: { size?: number }) {
  return (
    <svg {...svgProps(size)}>
      <path d="M21 3 3 10.5l7 2.5 2.5 7L21 3Z" />
      <path d="M10 13 21 3" />
    </svg>
  );
}

export function IconFund({ size }: { size?: number }) {
  return (
    <svg {...svgProps(size)}>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 8v8M8 12h8" />
    </svg>
  );
}

export function IconPay({ size }: { size?: number }) {
  return (
    <svg {...svgProps(size)}>
      <rect x="3" y="5" width="18" height="14" rx="2.5" />
      <path d="M3 10h18" />
    </svg>
  );
}

export function IconRefund({ size }: { size?: number }) {
  return (
    <svg {...svgProps(size)}>
      <path d="M3 12a9 9 0 1 0 3-6.7" />
      <path d="M3 4v5h5" />
    </svg>
  );
}

export function Brand({ tagline }: { tagline?: string }) {
  return (
    <div className="brand">
      <span className="brand-mark" aria-hidden="true">
        A
      </span>
      <div>
        <div className="brand-name">ApexPay</div>
        {tagline && <div className="brand-tagline">{tagline}</div>}
      </div>
    </div>
  );
}

export function ScreenHeader({ title }: { title: string }) {
  const navigate = useNavigate();

  const logout = () => {
    clearSession();
    navigate("/login");
  };

  return (
    <header className="app-header">
      <h1 className="screen-title">{title}</h1>
      <span className="spacer" />
      <button type="button" className="secondary" onClick={logout}>
        Log out
      </button>
    </header>
  );
}

export function AppHeader({ name }: { name: string }) {
  const navigate = useNavigate();
  const initial = name.trim().charAt(0).toUpperCase() || "A";

  const logout = () => {
    clearSession();
    navigate("/login");
  };

  return (
    <header className="app-header">
      <span className="avatar" aria-hidden="true">
        {initial}
      </span>
      <div className="greeting">
        <span className="hi">Welcome,</span>
        <span className="name">{name}</span>
      </div>
      <span className="spacer" />
      <button type="button" className="secondary" onClick={logout}>
        Log out
      </button>
    </header>
  );
}

const navItems = [
  { to: "/", label: "Home", icon: IconHome, end: true },
  { to: "/wallets", label: "Wallets", icon: IconWallets, end: false },
  { to: "/transfer", label: "Send", icon: IconSend, end: false },
  { to: "/checkout", label: "Pay", icon: IconPay, end: false },
];

export function BottomNav() {
  return (
    <nav className="bottom" aria-label="Main">
      {navItems.map((item) => (
        <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => (isActive ? "active" : "")}>
          <span className="icon">
            <item.icon size={20} />
          </span>
          {item.label}
        </NavLink>
      ))}
    </nav>
  );
}

export function TopNav({ title }: { title: string }) {
  const navigate = useNavigate();

  const logout = () => {
    clearSession();
    navigate("/login");
  };

  return (
    <nav className="top">
      <button type="button" className="secondary back" onClick={() => navigate(-1)} aria-label="Go back">
        ←
      </button>
      <span className="muted">{title}</span>
      <span className="spacer" />
      <button type="button" className="secondary" onClick={logout}>
        Log out
      </button>
    </nav>
  );
}

export function Loading({ text = "Loading…" }: { text?: string }) {
  return (
    <div aria-live="polite" aria-busy="true">
      <p className="muted">{text}</p>
      <div className="skeleton skeleton-card" />
      <div className="skeleton skeleton-card" />
    </div>
  );
}

export function Empty({ text }: { text: string }) {
  return <p className="muted">{text}</p>;
}

export function ErrorBanner({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="banner error" role="alert">
      <div>{message}</div>
      {onRetry && (
        <button type="button" className="link" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  );
}

export function OkBanner({ message }: { message: string }) {
  return (
    <div className="banner ok" role="status">
      {message}
    </div>
  );
}

export function StatusPill({ label, kind }: { label: string; kind: string }) {
  return (
    <span className={`status-pill ${kind.toLowerCase()}`}>
      {kind === "Credit" || kind === "Active" || kind === "Successful" ? "✓ " : ""}
      {label}
    </span>
  );
}

export function ConfirmDialog({
  open,
  title,
  amount,
  details,
  confirmLabel,
  onConfirm,
  onCancel,
  sending = false,
  error,
}: {
  open: boolean;
  title: string;
  amount: string;
  details: { label: string; value: string }[];
  confirmLabel: string;
  onConfirm: () => void;
  onCancel: () => void;
  sending?: boolean;
  error?: string;
}) {
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (open && dialogRef.current) {
      dialogRef.current.focus();
    }
  }, [open]);

  if (!open) return null;

  return (
    <div className="confirm-overlay" role="dialog" aria-modal="true" aria-label={title}>
      <div className="confirm-dialog" ref={dialogRef} tabIndex={-1}>
        <h2>{title}</h2>
        <div className="confirm-amount">{amount}</div>
        {details.map((d) => (
          <div className="confirm-detail" key={d.label}>
            <span className="label">{d.label}: </span>
            {d.value}
          </div>
        ))}
        {error && (
          <div className="banner error" role="alert">
            {error}
          </div>
        )}
        <div className="confirm-actions">
          <button type="button" className="secondary" onClick={onCancel} disabled={sending}>
            Cancel
          </button>
          <button type="button" onClick={onConfirm} disabled={sending}>
            {sending ? "Sending…" : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

export function WalletSelect({
  wallets,
  value,
  onChange,
  label,
  id,
}: {
  wallets: { id: string; name: string; accountNumber: string; balance: number }[];
  value: string;
  onChange: (v: string) => void;
  label: string;
  id: string;
}) {
  return (
    <>
      <label htmlFor={id}>{label}</label>
      <select id={id} value={value} onChange={(e) => onChange(e.target.value)}>
        <option value="">Select a wallet…</option>
        {wallets.map((w) => (
          <option key={w.id} value={w.id}>
            {w.name} ({w.accountNumber}) — {formatMoney(w.balance, "NGN")}
          </option>
        ))}
      </select>
    </>
  );
}

export function Pagination({
  page,
  totalPages,
  onPage,
}: {
  page: number;
  totalPages: number;
  onPage: (p: number) => void;
}) {
  if (totalPages <= 1) return null;
  return (
    <div className="pagination">
      <button type="button" className="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        Previous
      </button>
      <span className="muted">
        Page {page} of {totalPages}
      </span>
      <button type="button" className="secondary" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>
        Next
      </button>
    </div>
  );
}

export function PageHeader({ children }: { children: ReactNode }) {
  return <h1>{children}</h1>;
}
