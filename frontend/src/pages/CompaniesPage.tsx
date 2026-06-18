import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { api } from '../api';
import { ImportBox } from '../components/ImportBox';
import type { Company, Job, Score } from '../types';
import { domainColor, formatList, matchesText } from './shared';

type CompanySortKey = 'name' | 'domain' | 'city' | 'stack' | 'jobCount' | 'score';
type SortDirection = 'asc' | 'desc';
type CompanySort = { key: CompanySortKey; direction: SortDirection };

export function CompaniesPage() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [selected, setSelected] = useState<Company | null>(null);
  const [search, setSearch] = useState('');
  const [sort, setSort] = useState<CompanySort | null>(null);

  useEffect(() => {
    void load();
  }, []);

  async function load() {
    const nextCompanies = await api.companies();
    setCompanies(nextCompanies);
    setSelected(nextCompanies[0] ?? null);
  }

  const filtered = useMemo(() => {
    return companies
      .filter((company) =>
        matchesText(search, company.name, company.city, company.domain, company.knownStack.join(' '), company.notes ?? '')
      )
      .sort((left, right) => compareCompanies(left, right, sort));
  }, [companies, search, sort]);

  function changeSort(key: CompanySortKey) {
    setSort((current) => {
      if (current?.key !== key) {
        return { key, direction: 'asc' };
      }

      return { key, direction: current.direction === 'asc' ? 'desc' : 'asc' };
    });
  }

  const resultText = search.trim()
    ? `${filtered.length} résultat${filtered.length > 1 ? 's' : ''} sur ${companies.length}`
    : `${companies.length} entreprise${companies.length > 1 ? 's' : ''}`;

  return (
    <div className="companies-page">
      <div className="page-header companies-header">
        <div>
          <h1>Entreprises</h1>
          <p className="muted">
            {companies.length} entreprise{companies.length > 1 ? 's' : ''} suivie{companies.length > 1 ? 's' : ''}
            {search.trim() ? ` · ${filtered.length} visible${filtered.length > 1 ? 's' : ''}` : ''}
          </p>
        </div>
      </div>

      <ImportBox accept=".csv" label="Importer des entreprises CSV" onUpload={api.uploadCompanies} onDone={load} />

      <div className="companies-layout">
        <section className="panel companies-table-panel">
          <div className="companies-toolbar">
            <label className="companies-search">
              Rechercher
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Nom, ville, domaine, stack" />
            </label>
            <p className="muted companies-result-count">{resultText}</p>
          </div>

          <div className="table-wrap companies-table-wrap">
            <table className="companies-table">
              <thead>
                <tr>
                  <SortableHeader label="Nom" sortKey="name" sort={sort} onSort={changeSort} />
                  <SortableHeader label="Domaine" sortKey="domain" sort={sort} onSort={changeSort} />
                  <SortableHeader label="Ville" sortKey="city" sort={sort} onSort={changeSort} />
                  <SortableHeader label="Stack" sortKey="stack" sort={sort} onSort={changeSort} />
                  <SortableHeader label="Offres" sortKey="jobCount" sort={sort} onSort={changeSort} />
                  <SortableHeader label="Score" sortKey="score" sort={sort} onSort={changeSort} />
                </tr>
              </thead>
              <tbody>
                {filtered.map((company) => (
                  <tr
                    key={company.id}
                    className={`clickable ${selected?.id === company.id ? 'selected' : ''}`}
                    onClick={() => setSelected(company)}
                  >
                    <td className="company-name-cell">{company.name}</td>
                    <td>
                      <DomainBadge domain={company.domain} />
                    </td>
                    <td>{company.city || 'Non renseigné'}</td>
                    <td>
                      <StackPreview values={company.knownStack} />
                    </td>
                    <td className="numeric-cell">
                      <OfferCount count={company.jobCount} />
                    </td>
                    <td className="numeric-cell">
                      <span className="table-score-value">{company.score?.globalScore ?? '-'}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <CompanyDetail company={selected} />
      </div>
    </div>
  );
}

function SortableHeader({
  label,
  sortKey,
  sort,
  onSort
}: {
  label: string;
  sortKey: CompanySortKey;
  sort: CompanySort | null;
  onSort: (key: CompanySortKey) => void;
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

function compareCompanies(left: Company, right: Company, sort: CompanySort | null) {
  if (!sort) {
    return (right.score?.globalScore ?? 0) - (left.score?.globalScore ?? 0) || left.name.localeCompare(right.name);
  }

  const direction = sort.direction === 'asc' ? 1 : -1;
  const result = compareValues(companySortValue(left, sort.key), companySortValue(right, sort.key));
  return result * direction || left.name.localeCompare(right.name);
}

function companySortValue(company: Company, key: CompanySortKey) {
  switch (key) {
    case 'name':
      return company.name;
    case 'domain':
      return company.domain;
    case 'city':
      return company.city;
    case 'stack':
      return formatList(company.knownStack);
    case 'jobCount':
      return company.jobCount;
    case 'score':
      return company.score?.globalScore ?? -1;
  }
}

function compareValues(left: string | number, right: string | number) {
  if (typeof left === 'number' && typeof right === 'number') {
    return left - right;
  }

  return String(left).localeCompare(String(right), 'fr', { sensitivity: 'base', numeric: true });
}

export function CompanyDetail({ company, jobs }: { company: Company | null; jobs?: Job[] }) {
  if (!company) {
    return (
      <aside className="panel company-detail-card company-detail-empty">
        <p className="muted">Sélectionnez une entreprise pour voir le détail.</p>
      </aside>
    );
  }

  return (
    <aside className="panel company-detail-card">
      <CompanyDetailHeader company={company} />
      <div className="company-detail-sections">
        <ScoreBreakdown score={company.score} />
        <DetailSection title="Domaines secondaires" className="detail-section-domains">
          <InfoChips values={company.secondaryDomains} />
        </DetailSection>
        <DetailSection title="Stack connue" className="detail-section-stack">
          <InfoChips values={company.knownStack} limit={8} />
        </DetailSection>
        <MissingSkills score={company.score} />
        <DetailSection title="Offres liées" className="detail-section-jobs">
          <LinkedJobs count={company.jobCount} jobs={jobs} />
        </DetailSection>
        <CompanyLinks company={company} />
        <DetailSection title="Notes" className="detail-section-notes">
          <p className="detail-note">{company.notes || 'Aucune note renseignée.'}</p>
        </DetailSection>
        <AnalysisQuick score={company.score} />
      </div>
    </aside>
  );
}

function CompanyDetailHeader({ company }: { company: Company }) {
  return (
    <div className="company-detail-header">
      <div className="company-identity">
        {company.logoUrl ? (
          <img className="detail-logo" src={company.logoUrl} alt="" />
        ) : (
          <div className="company-initials" aria-hidden="true">{initials(company.name)}</div>
        )}
        <div>
          <h2>{company.name}</h2>
          <p>
            <DomainBadge domain={company.domain} /> <span className="detail-location">{company.city || 'Non renseigné'}</span>
          </p>
        </div>
      </div>
      <CompanyPriorityBadge score={company.score} />
    </div>
  );
}

function CompanyPriorityBadge({ score }: { score?: Score | null }) {
  if (!score) {
    return <div className="priority-badge priority-empty">Priorité non calculée</div>;
  }

  const level = priorityLevel(score.globalScore);

  return (
    <div className={`priority-badge priority-${level.key}`} title={`${level.label} · ${score.globalScore}`}>
      <strong>{level.label}</strong>
      <span>{score.globalScore}</span>
    </div>
  );
}

function DomainBadge({ domain }: { domain: string }) {
  if (!domain) {
    return <span className="domain-badge muted">Non renseigné</span>;
  }

  return (
    <span className="domain-badge" title={domain}>
      <span className="domain-dot" style={{ background: domainColor(domain) }} />
      <span>{domain}</span>
    </span>
  );
}

function StackPreview({ values }: { values: string[] }) {
  if (values.length === 0) {
    return <span className="muted">Non renseigné</span>;
  }

  const visible = values.slice(0, 3);
  const hidden = values.length - visible.length;

  return (
    <span className="stack-preview" title={formatList(values)}>
      {visible.map((value) => (
        <span key={value} className="stack-chip">{value}</span>
      ))}
      {hidden > 0 && <span className="stack-more">+{hidden}</span>}
    </span>
  );
}

function OfferCount({ count }: { count: number }) {
  return <span className={count > 0 ? 'offer-count has-offers' : 'offer-count'}>{count}</span>;
}

function DetailSection({ title, children, className = '' }: { title: string; children: ReactNode; className?: string }) {
  return (
    <section className={`detail-section ${className}`}>
      <h3>{title}</h3>
      {children}
    </section>
  );
}

function LinkedJobs({ count, jobs }: { count: number; jobs?: Job[] }) {
  if (!jobs) {
    return (
      <p className="linked-jobs">
        {count > 0 ? `${count} offre${count > 1 ? 's' : ''}` : 'Aucune offre liée détectée pour le moment.'}
      </p>
    );
  }

  if (jobs.length === 0) {
    return <p className="muted detail-empty">Aucune offre liée détectée pour le moment.</p>;
  }

  return (
    <ul className="linked-job-list">
      {jobs.map((job) => (
        <li key={job.id}>
          <div>
            {job.url ? <a href={job.url} target="_blank" rel="noreferrer">{job.title}</a> : <strong>{job.title}</strong>}
            <span>{job.seniority ?? 'Séniorité non renseignée'} · score {job.score?.globalScore ?? '-'}</span>
          </div>
        </li>
      ))}
    </ul>
  );
}

function InfoChips({ values, limit }: { values: string[]; limit?: number }) {
  if (values.length === 0) {
    return <p className="muted detail-empty">Non renseigné</p>;
  }

  const visible = limit ? values.slice(0, limit) : values;
  const hidden = values.length - visible.length;

  return (
    <div className="info-chip-list">
      {visible.map((value) => (
        <span key={value} className="info-chip">{value}</span>
      ))}
      {hidden > 0 && <span className="info-chip info-chip-more">+{hidden}</span>}
    </div>
  );
}

function CompanyLinks({ company }: { company: Company }) {
  const links = [
    ['Site', company.website],
    ['Carrière', company.careerUrl],
    ['LinkedIn', company.linkedinUrl]
  ].filter(([, url]) => Boolean(url));

  if (links.length === 0) {
    return null;
  }

  return (
    <DetailSection title="Actions" className="detail-section-actions">
      <div className="company-actions">
        {links.map(([label, url]) => (
          <a key={label} className="secondary-action" href={url ?? '#'} target="_blank" rel="noopener noreferrer" aria-label={`Ouvrir ${label} dans un nouvel onglet`}>
            {label}
          </a>
        ))}
      </div>
    </DetailSection>
  );
}

function ScoreBreakdown({ score }: { score?: Score | null }) {
  if (!score) {
    return <DetailSection title="Détail du score" className="detail-section-score"><p className="muted detail-empty">Score non calculé.</p></DetailSection>;
  }

  const items = [
    ['Technique', score.stackScore, 60],
    ['Domaine', score.domainScore, 25],
    ['Stratégique', score.strategicScore, 15]
  ] as const;

  return (
    <DetailSection title="Détail du score" className="detail-section-score">
      <p className="muted score-help">Ce score sert à prioriser les entreprises selon les données disponibles.</p>
      <div className="score-breakdown">
        {items.map(([label, value, max]) => (
          <div key={label} className="score-row">
            <div>
              <span>{label}</span>
              <strong>{value}</strong>
            </div>
            <div className="score-bar" aria-label={`${label} ${value}/${max}`}>
              <span style={{ width: `${Math.min(100, Math.max(0, (value / max) * 100))}%` }} />
            </div>
          </div>
        ))}
      </div>
    </DetailSection>
  );
}

function AnalysisQuick({ score }: { score?: Score | null }) {
  if (!score) {
    return null;
  }

  return (
    <DetailSection title="Analyse rapide" className="detail-section-analysis">
      <ReasonList title="Points forts" values={score.positiveReasons} tone="positive" />
      <ReasonList title="Points faibles" values={score.negativeReasons} tone="negative" />
    </DetailSection>
  );
}

function MissingSkills({ score }: { score?: Score | null }) {
  if (!score) {
    return null;
  }

  return (
    <DetailSection title="Compétences manquantes" className="detail-section-missing">
      {score.missingSkills.length > 0 ? <InfoChips values={score.missingSkills} /> : <p className="muted detail-empty">Aucune compétence manquante détectée.</p>}
    </DetailSection>
  );
}

function ReasonList({ title, values, tone }: { title: string; values: string[]; tone: 'positive' | 'negative' }) {
  if (values.length === 0) {
    return null;
  }

  return (
    <div className={`analysis-block ${tone}`}>
      <strong>{title}</strong>
      <ul>
        {values.slice(0, 6).map((value) => (
          <li key={value}>{value}</li>
        ))}
      </ul>
    </div>
  );
}

function priorityLevel(score: number) {
  if (score >= 75) return { key: 'top', label: 'Cible prioritaire' };
  if (score >= 60) return { key: 'high', label: 'À suivre activement' };
  if (score >= 40) return { key: 'correct', label: 'Potentiel correct' };
  if (score >= 20) return { key: 'watch', label: 'Entreprise à surveiller' };
  return { key: 'low', label: 'Priorité actuelle faible' };
}

function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('');
}
