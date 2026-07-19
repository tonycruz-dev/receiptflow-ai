import { ChevronLeft, ChevronRight } from 'lucide-react';
import { NavLink } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import {
  accountNavigation,
  primaryNavigation,
} from '@/components/layout/navigation-items';
import { Wordmark } from '@/components/layout/wordmark';

interface DesktopSidebarProps {
  collapsed: boolean;
  onCollapsedChange: (collapsed: boolean) => void;
}

export function DesktopSidebar({
  collapsed,
  onCollapsedChange,
}: DesktopSidebarProps) {
  return (
    <aside
      className={cn(
        'hidden min-h-screen shrink-0 flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground transition-[width] lg:sticky lg:top-0 lg:flex lg:h-screen',
        collapsed ? 'w-20' : 'w-64',
      )}
    >
      <div className="flex h-18 items-center border-b border-sidebar-border px-5">
        <Wordmark compact={collapsed} inverse />
      </div>
      <nav
        aria-label="Primary navigation"
        className="flex flex-1 flex-col gap-1 p-3"
      >
        {primaryNavigation.map((item) => (
          <NavLink
            key={item.href}
            to={item.href}
            end={item.end ?? false}
            title={collapsed ? item.label : undefined}
            className={({ isActive }) =>
              cn(
                'flex h-11 items-center gap-3 rounded-lg px-3 text-sm font-medium text-sidebar-foreground/75 hover:bg-sidebar-accent hover:text-white',
                isActive && 'bg-sidebar-accent text-white shadow-sm',
                collapsed && 'justify-center px-0',
              )
            }
          >
            <item.icon aria-hidden="true" className="size-5 shrink-0" />
            {collapsed ? (
              <span className="sr-only">{item.label}</span>
            ) : (
              item.label
            )}
          </NavLink>
        ))}
        <div className="mt-auto border-t border-sidebar-border pt-3">
          {accountNavigation.map((item) => (
            <NavLink
              key={item.href}
              to={item.href}
              title={collapsed ? item.label : undefined}
              className={({ isActive }) =>
                cn(
                  'flex h-11 items-center gap-3 rounded-lg px-3 text-sm font-medium text-sidebar-foreground/75 hover:bg-sidebar-accent hover:text-white',
                  isActive && 'bg-sidebar-accent text-white',
                  collapsed && 'justify-center px-0',
                )
              }
            >
              <item.icon aria-hidden="true" className="size-5 shrink-0" />
              {collapsed ? (
                <span className="sr-only">{item.label}</span>
              ) : (
                item.label
              )}
            </NavLink>
          ))}
        </div>
      </nav>
      <div className="border-t border-sidebar-border p-3">
        <Button
          variant="ghost"
          className="w-full text-sidebar-foreground hover:bg-sidebar-accent hover:text-white"
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          onClick={() => {
            onCollapsedChange(!collapsed);
          }}
        >
          {collapsed ? (
            <ChevronRight aria-hidden="true" />
          ) : (
            <>
              <ChevronLeft aria-hidden="true" />
              Collapse
            </>
          )}
        </Button>
      </div>
    </aside>
  );
}
