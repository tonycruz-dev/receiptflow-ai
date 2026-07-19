import { Bell, ChevronDown, UserRound } from 'lucide-react';
import { Link, useLocation } from 'react-router-dom';
import { Wordmark } from '@/components/layout/wordmark';
import { pageTitles } from '@/components/layout/navigation-items';
import { ThemeToggle } from '@/components/shared/theme-toggle';
import { Button } from '@/components/ui/button';
import { useCurrentUser } from '@/api/use-current-user';
import { useAuth } from '@/providers/use-auth';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

function getPageTitle(pathname: string) {
  if (/^\/receipts\/[^/]+$/.test(pathname)) return 'Receipt details';
  return pageTitles[pathname] ?? 'ReceiptFlow';
}

export function TopHeader() {
  const { pathname } = useLocation();
  const { logout } = useAuth();
  const currentUser = useCurrentUser();
  const accountLabel =
    currentUser.data?.username ?? currentUser.data?.email ?? 'Signed in';

  return (
    <header className="sticky top-0 z-30 flex h-18 items-center justify-between border-b bg-background/95 px-4 backdrop-blur-sm sm:px-6 lg:px-8">
      <div className="lg:hidden">
        <Wordmark compact />
      </div>
      <p className="hidden text-sm font-semibold sm:block">
        {getPageTitle(pathname)}
      </p>
      <div className="ml-auto flex items-center gap-1 sm:gap-2">
        <ThemeToggle />
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label="Notifications (none unread)"
        >
          <Bell aria-hidden="true" />
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" className="gap-2 px-2 sm:px-3">
              <span className="flex size-7 items-center justify-center rounded-full bg-accent text-accent-foreground">
                <UserRound aria-hidden="true" className="size-4" />
              </span>
              <span className="hidden max-w-40 truncate text-sm sm:inline">
                {currentUser.isLoading ? 'Loading account…' : accountLabel}
              </span>
              <ChevronDown
                aria-hidden="true"
                className="hidden size-3.5 sm:block"
              />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel className="max-w-64 truncate">
              {accountLabel}
            </DropdownMenuLabel>
            <DropdownMenuSeparator className="my-1 h-px bg-border" />
            <DropdownMenuItem asChild>
              <Link to="/profile">View profile</Link>
            </DropdownMenuItem>
            <DropdownMenuItem
              onSelect={() => {
                void logout();
              }}
            >
              Sign out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
