import { useState } from 'react';
import { Outlet, useNavigation } from 'react-router-dom';
import { DesktopSidebar } from '@/components/layout/desktop-sidebar';
import { MobileNavigation } from '@/components/layout/mobile-navigation';
import { TopHeader } from '@/components/layout/top-header';
import { RouteLoading } from '@/components/shared/route-loading';

export function AppShell() {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const navigation = useNavigation();

  return (
    <div className="flex min-h-screen bg-background">
      <DesktopSidebar
        collapsed={sidebarCollapsed}
        onCollapsedChange={setSidebarCollapsed}
      />
      <div className="min-w-0 flex-1">
        <TopHeader />
        <main
          id="main-content"
          className="mx-auto w-full max-w-7xl p-4 pb-24 sm:p-6 sm:pb-24 lg:p-8"
        >
          {navigation.state === 'loading' ? <RouteLoading /> : <Outlet />}
        </main>
      </div>
      <MobileNavigation />
    </div>
  );
}
