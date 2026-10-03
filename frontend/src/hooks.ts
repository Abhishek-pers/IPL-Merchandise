import { useCallback, useEffect, useState } from 'react';

export interface AsyncState<T> {
  data?: T;
  error?: Error;
  loading: boolean;
  reload: () => void;
}

/** Runs an async loader whenever `deps` change; ignores results of stale requests. */
export function useAsync<T>(load: () => Promise<T>, deps: readonly unknown[]): AsyncState<T> {
  const [state, setState] = useState<Omit<AsyncState<T>, 'reload'>>({ loading: true });
  const [version, setVersion] = useState(0);
  const reload = useCallback(() => setVersion((v) => v + 1), []);

  useEffect(() => {
    let cancelled = false;
    setState((s) => ({ ...s, loading: true, error: undefined }));
    load()
      .then((data) => !cancelled && setState({ data, loading: false }))
      .catch((error: Error) => !cancelled && setState({ error, loading: false }));
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, version]);

  return { ...state, reload };
}

/** One cached formatter per currency: creating Intl.NumberFormat on every render is slow. */
const formatters = new Map<string, Intl.NumberFormat>();

/** Formats an amount the Indian way, e.g. 2499 -> "₹2,499.00". */
export function formatMoney(amount: number, currency = 'INR'): string {
  let formatter = formatters.get(currency);
  if (!formatter) {
    formatter = new Intl.NumberFormat('en-IN', { style: 'currency', currency });
    formatters.set(currency, formatter);
  }
  return formatter.format(amount);
}
