import {
  Bot,
  LayoutDashboard,
  ReceiptText,
  Package,
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
  { label: 'Products', href: '/products', icon: Package },
  { label: 'Upload', href: '/upload', icon: Upload },
  { label: 'Search', href: '/search', icon: Search },
  { label: 'Assistant', href: '/assistant', icon: Bot },
];

export const accountNavigation: NavigationItem[] = [
  { label: 'Profile', href: '/profile', icon: UserRound },
];

export const pageTitles: Record<string, string> = {
  '/': 'Dashboard',
  '/receipts': 'Receipts',
  '/upload': 'Upload',
  '/receipts/new': 'Upload receipt',
  '/products': 'Products',
  '/products/manuals/new': 'Upload product manual',
  '/search': 'Receipt search',
  '/assistant': 'AI receipt assistant',
  '/profile': 'Profile',
};
