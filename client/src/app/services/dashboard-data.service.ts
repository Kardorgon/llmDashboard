import { Injectable, computed, signal } from '@angular/core';
import { DashboardKpi, DashboardRegion, DashboardSnapshot } from '../contracts/chat';

@Injectable({ providedIn: 'root' })
export class DashboardDataService {
  private readonly kpisState = signal<DashboardKpi[]>([
    { id: 'revenue', label: 'Monthly Revenue', value: 128400, unit: 'USD', trendPercent: 7.9 },
    { id: 'arr', label: 'ARR', value: 1541000, unit: 'USD', trendPercent: 5.2 },
    { id: 'active-users', label: 'Active Users', value: 8920, unit: 'users', trendPercent: 3.1 },
    { id: 'support-tickets', label: 'Support Tickets', value: 184, unit: 'tickets', trendPercent: -11.4 }
  ]);

  private readonly regionsState = signal<DashboardRegion[]>([
    { region: 'North America', revenue: 64200, deals: 58, churnPercent: 2.3 },
    { region: 'Europe', revenue: 37100, deals: 44, churnPercent: 2.9 },
    { region: 'APAC', revenue: 19600, deals: 29, churnPercent: 3.8 },
    { region: 'LATAM', revenue: 7500, deals: 17, churnPercent: 4.6 }
  ]);

  readonly kpis = computed(() => this.kpisState());
  readonly regions = computed(() => this.regionsState());

  readonly snapshot = computed<DashboardSnapshot>(() => ({
    title: 'Sales Performance Dashboard',
    generatedAtIso: new Date().toISOString(),
    kpis: this.kpis(),
    regions: this.regions()
  }));

  readonly snapshotId = computed(() => {
    const payload = JSON.stringify(this.snapshot());
    let hash = 0;
    for (let i = 0; i < payload.length; i += 1) {
      hash = (hash << 5) - hash + payload.charCodeAt(i);
      hash |= 0;
    }

    return `snapshot-${Math.abs(hash)}`;
  });
}
