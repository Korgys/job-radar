import { useEffect, useMemo, useState } from 'react';
import { api } from '../api';
import { ImportBox } from '../components/ImportBox';
import { ScorePanel } from '../components/ScorePanel';
import type { Job } from '../types';
import { formatList, matchesText } from './shared';

export function JobsPage() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [selected, setSelected] = useState<Job | null>(null);
  const [search, setSearch] = useState('');
  const [message, setMessage] = useState('');

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
    return jobs
      .filter((job) =>
        matchesText(search, job.title, job.companyName, job.location ?? '', job.jobType ?? '', job.stack.join(' '), job.description ?? '')
      )
      .sort((left, right) => (right.score?.globalScore ?? 0) - (left.score?.globalScore ?? 0));
  }, [jobs, search]);

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
            <div className="toolbar">
              <label>
                Recherche
                <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Titre, entreprise, stack" />
              </label>
            </div>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Titre</th>
                    <th>Entreprise</th>
                    <th>Localisation</th>
                    <th>Type</th>
                    <th>Séniorité</th>
                    <th>Stack</th>
                    <th>Score</th>
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
