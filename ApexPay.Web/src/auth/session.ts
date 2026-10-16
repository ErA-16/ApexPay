export interface Session {
  accessToken: string;
  expiresAt: string;
}

const storageKey = "apexpay.auth";

export const idleLimitMs = 30 * 60 * 1000;
export const idleWarningMs = 60 * 1000;

export function loadSession(): Session | null {
  const raw = sessionStorage.getItem(storageKey);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as Session;
  } catch {
    return null;
  }
}

export function saveSession(session: Session): void {
  sessionStorage.setItem(storageKey, JSON.stringify(session));
}

export function clearSession(): void {
  sessionStorage.removeItem(storageKey);
}

export function isLoggedIn(): boolean {
  return loadSession() !== null;
}

export function startIdleWatcher(onIdle: () => void, onWarning: () => void): () => void {
  let idleTimer: number | undefined;
  let warningTimer: number | undefined;

  const clear = () => {
    window.clearTimeout(idleTimer);
    window.clearTimeout(warningTimer);
  };

  const arm = () => {
    clear();
    warningTimer = window.setTimeout(onWarning, idleLimitMs - idleWarningMs);
    idleTimer = window.setTimeout(onIdle, idleLimitMs);
  };

  const activityEvents = ["pointerdown", "keydown", "scroll", "touchstart"];
  const handleActivity = () => {
    if (!loadSession()) return;
    arm();
  };

  activityEvents.forEach((event) => window.addEventListener(event, handleActivity, { passive: true }));
  arm();

  return () => {
    clear();
    activityEvents.forEach((event) => window.removeEventListener(event, handleActivity));
  };
}
