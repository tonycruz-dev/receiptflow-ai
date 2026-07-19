import type { ReceiptCardData } from '@/components/shared/receipt-card';

/** Local visual-development fixtures. Replace this module with API hooks later. */
export const recentReceiptFixtures: ReceiptCardData[] = [
  {
    id: 'rcpt-1042',
    merchant: 'North & Pine Stationery',
    date: '2026-07-18',
    total: 84.2,
    status: 'Completed',
    fileName: 'north-pine-july.pdf',
  },
  {
    id: 'rcpt-1041',
    merchant: 'The Corner Market',
    date: '2026-07-17',
    total: 47.86,
    status: 'Processing',
    fileName: 'corner-market-17-jul.jpg',
  },
  {
    id: 'rcpt-1040',
    merchant: 'Harbour Rail',
    date: '2026-07-15',
    total: 126.5,
    status: 'Pending',
    fileName: 'harbour-rail-ticket.pdf',
  },
];

export const dashboardFixture = {
  totalReceipts: 128,
  totalSpending: 6842.35,
  processingDocuments: 3,
} as const;
