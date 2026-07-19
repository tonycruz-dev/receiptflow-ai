import {
  Bot,
  LayoutDashboard,
  ReceiptText,
  Search,
  Upload,
  UserRound,
  type LucideIcon,
} from 'lucide-react';

export interface NavigationItem {
  label: string;
  href: string;
  icon: LucideIcon;
  end?: boolean;
}

export const primaryNavigation: NavigationItem[] = [
  { label: 'Dashboard', href: '/', icon: LayoutDashboard, end: true },
  { label: 'Receipts', href: '/receipts', icon: ReceiptText },
  { label: 'Upload', href: '/receipts/new', icon: Upload },
  { label: 'Search', href: '/search', icon: Search },
  { label: 'Assistant', href: '/assistant', icon: Bot },
];

export const accountNavigation: NavigationItem[] = [
  { label: 'Profile', href: '/profile', icon: UserRound },
];

export const pageTitles: Record<string, string> = {
  '/': 'Dashboard',
  '/receipts': 'Receipts',
  '/receipts/new': 'Upload receipt',
  '/search': 'Receipt search',
  '/assistant': 'AI receipt assistant',
  '/profile': 'Profile',
};
