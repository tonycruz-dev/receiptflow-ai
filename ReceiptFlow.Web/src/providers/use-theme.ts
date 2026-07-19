import { useContext } from 'react';
import { ThemeContext } from '@/providers/theme-provider';

export function useTheme() {
  const value = useContext(ThemeContext);
  if (!value) {
    throw new Error('useTheme must be used within ThemeProvider.');
  }
  return value;
}
