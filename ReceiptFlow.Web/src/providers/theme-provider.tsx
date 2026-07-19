import {
  createContext,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react';

export type Theme = 'light' | 'dark' | 'system';

export interface ThemeContextValue {
  theme: Theme;
  setTheme: (theme: Theme) => void;
}

export const ThemeContext = createContext<ThemeContextValue | null>(null);

function getStoredTheme(): Theme {
  const stored = globalThis.localStorage.getItem('receiptflow-theme');
  return stored === 'light' || stored === 'dark' || stored === 'system'
    ? stored
    : 'system';
}

export function ThemeProvider({ children }: PropsWithChildren) {
  const [theme, setTheme] = useState<Theme>(getStoredTheme);

  useEffect(() => {
    const root = document.documentElement;
    const media = window.matchMedia('(prefers-color-scheme: dark)');

    const applyTheme = () => {
      const resolvedTheme =
        theme === 'system' && media.matches ? 'dark' : theme;
      root.classList.toggle('dark', resolvedTheme === 'dark');
    };

    applyTheme();
    globalThis.localStorage.setItem('receiptflow-theme', theme);
    media.addEventListener('change', applyTheme);
    return () => {
      media.removeEventListener('change', applyTheme);
    };
  }, [theme]);

  const value = useMemo(() => ({ theme, setTheme }), [theme]);
  return (
    <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
  );
}
