import { useEffect, useState } from 'react';
import { api } from '../api';
import type { DashboardStats, ReportFile } from '../types';

export function DashboardPage() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [message, setMessage] = useState('');

  useEffect(() => {
    void load();
  }, []);

  async function load() {
    setStats(await api.dashboard());
  }

  async function recalculate() {
    setMessage('');
    try {
      const result = await api.recalculate();
      setMessage(`${result.companyScores} scores entreprises et ${result.jobScores} scores offres recalculés.`);
      await load();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Recalcul impossible.');
    }
  }

  async function generateReport() {
    setMessage('');
    try {
      const report: ReportFile = await api.generateReport();
      setMessage(`Rapport généré : ${report.fileName}`);
      await load();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Génération impossible.');
    }
  }

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Dashboard</h1>
          <p className="muted">Vue locale des entreprises, offres et scores.</p>
        </div>
        <div className="toolbar">
          <button type="button" onClick={recalculate}>Recalculer les scores</button>
          <button type="button" onClick={generateReport}>Générer rapport</button>
        </div>
      </div>

      {message && <p className="status">{message}</p>}

      <div className="grid three">
        <Stat label="Entreprises" value={stats?.companyCount ?? 0} />
        <Stat label="Offres" value={stats?.jobCount ?? 0} />
        <Stat label="Dernier CV" value={stats?.lastProfileImport ? new Date(stats.lastProfileImport).toLocaleString() : 'Aucun'} />
        <Stat label="Entreprises compatibles" value={stats?.compatibleCompanyCount ?? 0} />
        <Stat label="Offres compatibles" value={stats?.compatibleJobCount ?? 0} />
      </div>
    </>
  );
}

function Stat({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="card stat">
      <span className="muted">{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
