import { NavLink } from 'react-router-dom';
import { primaryNavigation } from '@/components/layout/navigation-items';
import { cn } from '@/lib/utils';

const mobileItems = primaryNavigation.filter((item) => item.label !== 'Search');

export function MobileNavigation() {
  return (
    <nav
      aria-label="Mobile navigation"
      className="fixed inset-x-0 bottom-0 z-40 border-t bg-card/95 px-2 pb-[max(0.5rem,env(safe-area-inset-bottom))] shadow-[0_-4px_18px_rgb(15_23_42/0.06)] backdrop-blur-sm lg:hidden"
    >
      <ul className="mx-auto grid max-w-lg grid-cols-4">
        {mobileItems.map((item) => (
          <li key={item.href}>
            <NavLink
              to={item.href}
              end={item.end ?? false}
              className={({ isActive }) =>
                cn(
                  'flex min-h-14 flex-col items-center justify-center gap-1 rounded-lg px-1 text-[0.7rem] font-medium text-muted-foreground',
                  isActive && 'text-primary',
                )
              }
            >
              <item.icon aria-hidden="true" className="size-5" />
              <span>{item.label}</span>
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
