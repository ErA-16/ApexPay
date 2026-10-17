import { ApiError, friendlyMessage } from "./types.ts";
import { clearSession, loadSession } from "../auth/session.ts";

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;

if (!configuredBaseUrl) {
  throw new Error("VITE_API_BASE_URL is not set. Add it to your .env file (see .env.example).");
}

const baseUrl = configuredBaseUrl;

const REQUEST_TIMEOUT_MS = 30000;

export function newIdempotencyKey(): string {
  return crypto.randomUUID();
}

interface RequestOptions {
  idempotencyKey?: string;
  signal?: AbortSignal;
}

export async function api<T>(path: string, method: string, body?: unknown, options?: RequestOptions): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
  };
  const session = loadSession();
  if (session) {
    headers["Authorization"] = `Bearer ${session.accessToken}`;
  }
  if (options?.idempotencyKey) {
    headers["Idempotency-Key"] = options.idempotencyKey;
  }

  const controller = new AbortController();
  let timedOut = false;
  const timeoutId = window.setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, REQUEST_TIMEOUT_MS);

  if (options?.signal) {
    options.signal.addEventListener("abort", () => controller.abort());
  }

  try {
    const response = await fetch(`${baseUrl}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: controller.signal,
    });
    if (response.status === 401) {
      clearSession();
    }
    if (response.status === 204) {
      return undefined as T;
    }
    const text = await response.text();
    if (!response.ok) {
      throw new ApiError(response.status, friendlyMessage(response.status, text));
    }
    if (!text) {
      return undefined as T;
    }
    try {
      return JSON.parse(text) as T;
    } catch {
      return text as unknown as T;
    }
  } catch (err) {
    if (timedOut) {
      throw new Error("The request timed out. Please try again.");
    }
    throw err;
  } finally {
    window.clearTimeout(timeoutId);
  }
}
