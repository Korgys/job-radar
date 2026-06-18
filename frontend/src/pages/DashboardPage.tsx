import { useEffect, useMemo, useState } from 'react';
import { api } from '../api';
import type { DashboardStats, Job } from '../types';
import { formatList } from './shared';

const SHORTLIST_SIZE = 10;

export function DashboardPage() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [message, setMessage] = useState('');
  const [messageType, setMessageType] = useState<'success' | 'error'>('success');

  useEffect(() => {
    void load();
  }, []);

  async function load() {
    const [nextStats, nextJobs] = await Promise.all([api.dashboard(), api.jobs()]);
    setStats(nextStats);
    setJobs(nextJobs);
  }

  async function recalculate() {
    clearMessage();
    try {
      const result = await api.recalculate();
      showSuccess(`${result.companyScores} scores entreprises et ${result.jobScores} scores offres recalculés.`);
      await load();
    } catch (error) {
      showError(error instanceof Error ? error.message : 'Recalcul impossible.');
    }
  }

  const topJobs = useMemo(() => getTopJobs(jobs), [jobs]);

  async function copyMarkdown() {
    clearMessage();
    try {
      await navigator.clipboard.writeText(buildShortlistMarkdown(topJobs));
      showSuccess('Shortlist copiée en Markdown.');
    } catch {
      showError('Copie impossible dans ce navigateur.');
    }
  }

  function clearMessage() {
    setMessage('');
    setMessageType('success');
  }

  function showSuccess(nextMessage: string) {
    setMessage(nextMessage);
    setMessageType('success');
  }

  function showError(nextMessage: string) {
    setMessage(nextMessage);
    setMessageType('error');
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
        </div>
      </div>

      {message && <p className={`status ${messageType === 'error' ? 'status-error' : 'status-success'}`} role={messageType === 'error' ? 'alert' : 'status'}>{message}</p>}

      <div className="grid three">
        <Stat label="Entreprises" value={stats?.companyCount ?? 0} />
        <Stat label="Offres" value={stats?.jobCount ?? 0} />
        <Stat label="Dernier CV" value={stats?.lastProfileImport ? new Date(stats.lastProfileImport).toLocaleString() : 'Aucun'} />
        <Stat label="Entreprises compatibles" value={stats?.compatibleCompanyCount ?? 0} />
        <Stat label="Offres compatibles" value={stats?.compatibleJobCount ?? 0} />
      </div>

      <section className="panel shortlist-panel">
        <div className="shortlist-header">
          <div>
            <h2>Top 10 offres</h2>
            <p className="muted">Shortlist générée depuis les données déjà chargées via /api/jobs.</p>
          </div>
          <button type="button" onClick={copyMarkdown} disabled={topJobs.length === 0}>Copier en Markdown</button>
        </div>

        {topJobs.length > 0 ? (
          <div className="shortlist-list">
            {topJobs.map((job, index) => (
              <article className="shortlist-card" key={job.id}>
                <div className="shortlist-rank">#{index + 1}</div>
                <div className="shortlist-content">
                  <h3>{job.title}</h3>
                  <p className="muted">{job.companyName} · {job.location ?? 'Localisation non renseignée'}</p>
                  <p><strong>Score :</strong> {formatScore(job)}</p>
                  <ReasonList title="Raisons positives" items={job.score?.positiveReasons ?? []} emptyLabel="Aucune raison positive renseignée." />
                  <ReasonList title="Compétences manquantes" items={job.score?.missingSkills ?? []} emptyLabel="Aucune compétence manquante renseignée." />
                  {job.url ? <a href={job.url} target="_blank" rel="noreferrer">Ouvrir l’offre</a> : <span className="muted">Lien non renseigné</span>}
                </div>
              </article>
            ))}
          </div>
        ) : (
          <p className="muted">Aucune offre disponible pour construire la shortlist.</p>
        )}
      </section>
    </>
  );
}

function getTopJobs(jobs: Job[]) {
  return [...jobs]
    .sort((left, right) => (right.score?.globalScore ?? -1) - (left.score?.globalScore ?? -1) || left.title.localeCompare(right.title, 'fr'))
    .slice(0, SHORTLIST_SIZE);
}

function formatScore(job: Job) {
  return job.score ? `${job.score.globalScore}/100` : 'Non scoré';
}

function ReasonList({ title, items, emptyLabel }: { title: string; items: string[]; emptyLabel: string }) {
  return (
    <div>
      <strong>{title}</strong>
      {items.length > 0 ? (
        <ul>
          {items.map((item) => <li key={item}>{item}</li>)}
        </ul>
      ) : (
        <p className="muted">{emptyLabel}</p>
      )}
    </div>
  );
}

function buildShortlistMarkdown(jobs: Job[]) {
  const lines = ['# Top 10 offres', ''];
  for (const [index, job] of jobs.entries()) {
    lines.push(`## ${index + 1}. ${job.title} — ${job.companyName}`);
    lines.push(`- Score : ${formatScore(job)}`);
    lines.push(`- Localisation : ${job.location ?? 'Non renseignée'}`);
    lines.push(`- Stack : ${formatList(job.stack)}`);
    lines.push('- Raisons positives :');
    appendMarkdownList(lines, job.score?.positiveReasons ?? [], 'Aucune raison positive renseignée.');
    lines.push('- Compétences manquantes :');
    appendMarkdownList(lines, job.score?.missingSkills ?? [], 'Aucune compétence manquante renseignée.');
    lines.push(`- Lien : ${job.url ?? 'Non renseigné'}`);
    lines.push('');
  }
  return lines.join('\n');
}

function appendMarkdownList(lines: string[], items: string[], emptyLabel: string) {
  if (items.length === 0) {
    lines.push(`  - ${emptyLabel}`);
    return;
  }

  for (const item of items) {
    lines.push(`  - ${item}`);
  }
}

function Stat({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="card stat">
      <span className="muted">{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
