import { useEffect, useMemo, useState } from 'react';
import { api } from '../api';
import { ImportBox } from '../components/ImportBox';
import { ScorePanel } from '../components/ScorePanel';
import type { Job } from '../types';
import { formatList, matchesText } from './shared';

type JobSortKey = 'title' | 'company' | 'location' | 'type' | 'seniority' | 'stack' | 'score';
type SortDirection = 'asc' | 'desc';
type JobSort = { key: JobSortKey; direction: SortDirection };

export function JobsPage() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [selected, setSelected] = useState<Job | null>(null);
  const [search, setSearch] = useState('');
  const [minScore, setMinScore] = useState('60');
  const [message, setMessage] = useState('');
  const [sort, setSort] = useState<JobSort | null>(null);

  useEffect(() => {
    void load();
  }, []);

  async function load() {
    const nextJobs = await api.jobs();
    setJobs(nextJobs);
    setSelected(nextJobs[0] ?? null);
  }

  async function recalculate() {
    setMessage('');
    try {
      const result = await api.recalculate();
      setMessage(`${result.jobScores} scores offres recalculés.`);
      await load();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Recalcul impossible.');
    }
  }

  const filtered = useMemo(() => {
    const score = Number(minScore || 0);

    return jobs
      .filter((job) =>
        (job.score?.globalScore ?? 0) >= score &&
        matchesText(search, job.title, job.companyName, job.location ?? '', job.jobType ?? '', job.stack.join(' '), job.description ?? '')
      )
      .sort((left, right) => compareJobs(left, right, sort));
  }, [jobs, minScore, search, sort]);

  function changeSort(key: JobSortKey) {
    setSort((current) => {
      if (current?.key !== key) {
        return { key, direction: 'asc' };
      }

      return { key, direction: current.direction === 'asc' ? 'desc' : 'asc' };
    });
  }

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Offres</h1>
          <p className="muted">{filtered.length} offres affichées.</p>
        </div>
        <button type="button" onClick={recalculate}>Recalculer</button>
      </div>

      {message && <p className="status">{message}</p>}

      <div className="grid">
        <section className="grid">
          <ImportBox accept=".csv,text/csv" label="Importer jobs.csv" onUpload={api.uploadJobs} onDone={() => void load()} />
          <div className="panel">
            <div className="toolbar jobs-filter-toolbar">
              <input
                className="jobs-search-input"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="rechercher un titre, une entreprise, une stack"
                aria-label="Recherche"
              />
              <ScoreStepper value={minScore} onChange={setMinScore} />
            </div>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <SortableHeader label="Titre" sortKey="title" sort={sort} onSort={changeSort} />
                    <SortableHeader label="Entreprise" sortKey="company" sort={sort} onSort={changeSort} />
                    <SortableHeader label="Localisation" sortKey="location" sort={sort} onSort={changeSort} />
                    <SortableHeader label="Type" sortKey="type" sort={sort} onSort={changeSort} />
                    <SortableHeader label="Séniorité" sortKey="seniority" sort={sort} onSort={changeSort} />
                    <SortableHeader label="Stack" sortKey="stack" sort={sort} onSort={changeSort} />
                    <SortableHeader label="Score" sortKey="score" sort={sort} onSort={changeSort} />
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((job) => (
                    <tr key={job.id} className="clickable" onClick={() => setSelected(job)}>
                      <td>{job.title}</td>
                      <td>{job.companyName}</td>
                      <td>{job.location ?? '-'}</td>
                      <td>{job.jobType ?? '-'}</td>
                      <td>{job.seniority ?? '-'}</td>
                      <td>{formatList(job.stack)}</td>
                      <td>{job.score?.globalScore ?? '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </section>

      </div>

      <JobDetail job={selected} />
    </>
  );
}

function SortableHeader({
  label,
  sortKey,
  sort,
  onSort
}: {
  label: string;
  sortKey: JobSortKey;
  sort: JobSort | null;
  onSort: (key: JobSortKey) => void;
}) {
  const active = sort?.key === sortKey;
  const direction = active ? sort.direction : undefined;

  return (
    <th aria-sort={active ? (direction === 'asc' ? 'ascending' : 'descending') : 'none'}>
      <button type="button" className="sort-header-button" onClick={() => onSort(sortKey)}>
        <span>{label}</span>
        <span className="sort-indicator" aria-hidden="true">{active ? (direction === 'asc' ? '▲' : '▼') : ''}</span>
      </button>
    </th>
  );
}

function compareJobs(left: Job, right: Job, sort: JobSort | null) {
  if (!sort) {
    return (right.score?.globalScore ?? 0) - (left.score?.globalScore ?? 0) || left.title.localeCompare(right.title);
  }

  const direction = sort.direction === 'asc' ? 1 : -1;
  const result = compareValues(jobSortValue(left, sort.key), jobSortValue(right, sort.key));
  return result * direction || left.title.localeCompare(right.title);
}

function jobSortValue(job: Job, key: JobSortKey) {
  switch (key) {
    case 'title':
      return job.title;
    case 'company':
      return job.companyName;
    case 'location':
      return job.location ?? '';
    case 'type':
      return job.jobType ?? '';
    case 'seniority':
      return job.seniority ?? '';
    case 'stack':
      return formatList(job.stack);
    case 'score':
      return job.score?.globalScore ?? -1;
  }
}

function compareValues(left: string | number, right: string | number) {
  if (typeof left === 'number' && typeof right === 'number') {
    return left - right;
  }

  return String(left).localeCompare(String(right), 'fr', { sensitivity: 'base', numeric: true });
}

function ScoreStepper({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const score = clampScore(Number(value || 0));

  function update(delta: number) {
    onChange(String(clampScore(score + delta)));
  }

  return (
    <div className="score-stepper-field">
      <span>Score minimum</span>
      <div className="score-stepper" role="group" aria-label="Score minimum">
        <button type="button" onClick={() => update(-5)} disabled={score <= 0}>-</button>
        <strong>{score}</strong>
        <button type="button" onClick={() => update(5)} disabled={score >= 100}>+</button>
      </div>
    </div>
  );
}

function clampScore(value: number) {
  if (Number.isNaN(value)) {
    return 0;
  }

  return Math.min(100, Math.max(0, Math.round(value / 5) * 5));
}

export function JobDetail({ job }: { job: Job | null }) {
  if (!job) {
    return <aside className="panel muted">Aucune offre sélectionnée.</aside>;
  }

  return (
    <aside className="panel grid">
      <div>
        <h2>{job.title}</h2>
        <p>{job.companyName} · {job.location ?? 'Localisation non renseignée'}</p>
      </div>
      <div>
        <strong>Type / contrat</strong>
        <p>{job.jobType ?? 'Non renseigné'} · {job.contract ?? 'Non renseigné'}</p>
      </div>
      <div>
        <strong>Télétravail</strong>
        <p>{job.remotePolicy ?? 'Non renseigné'}</p>
      </div>
      <div>
        <strong>Séniorité</strong>
        <p>{job.seniority ?? 'Non renseignée'}</p>
      </div>
      <div>
        <strong>Salaire</strong>
        <p>{formatSalary(job)}</p>
      </div>
      <div>
        <strong>Stack</strong>
        <p>{formatList(job.stack)}</p>
      </div>
      {job.description && (
        <div>
          <strong>Description</strong>
          <p>{job.description}</p>
        </div>
      )}
      {job.url && (
        <a href={job.url} target="_blank" rel="noreferrer">
          Ouvrir l’offre
        </a>
      )}
      <ScorePanel score={job.score} />
    </aside>
  );
}

function formatSalary(job: Job) {
  if (job.salaryMin && job.salaryMax) {
    return `${job.salaryMin.toLocaleString()} - ${job.salaryMax.toLocaleString()} €`;
  }
  if (job.salaryMin) {
    return `Dès ${job.salaryMin.toLocaleString()} €`;
  }
  if (job.salaryMax) {
    return `Jusqu'à ${job.salaryMax.toLocaleString()} €`;
  }
  return 'Non renseigné';
}
