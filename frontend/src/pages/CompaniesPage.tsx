import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { api } from '../api';
import { ImportBox } from '../components/ImportBox';
import type { Company, Score } from '../types';
import { domainColor, formatList, matchesText } from './shared';

export function CompaniesPage() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [selected, setSelected] = useState<Company | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    void load();
  }, []);

  async function load() {
    const nextCompanies = await api.companies();
    setCompanies(nextCompanies);
    setSelected(nextCompanies[0] ?? null);
  }

  const filtered = useMemo(() => {
    return companies.filter((company) =>
      matchesText(search, company.name, company.city, company.domain, company.knownStack.join(' '), company.notes ?? '')
    );
  }, [companies, search]);

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
                  <th>Nom</th>
                  <th>Domaine</th>
                  <th>Ville</th>
                  <th>Stack</th>
                  <th>Offres</th>
                  <th>Score</th>
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

export function CompanyDetail({ company }: { company: Company | null }) {
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
        <DetailSection title="Domaines secondaires">
          <InfoChips values={company.secondaryDomains} />
        </DetailSection>
        <DetailSection title="Stack connue">
          <InfoChips values={company.knownStack} limit={8} />
        </DetailSection>
        <DetailSection title="Offres liées">
          <p className="linked-jobs">
            {company.jobCount > 0 ? `${company.jobCount} offre${company.jobCount > 1 ? 's' : ''}` : 'Aucune offre liée détectée pour le moment.'}
          </p>
        </DetailSection>
        <CompanyLinks company={company} />
        <DetailSection title="Notes">
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

function CompanyPriorityBadge({ score, compact = false }: { score?: Score | null; compact?: boolean }) {
  if (!score) {
    return <div className={`priority-badge priority-empty ${compact ? 'priority-compact' : ''}`}>Priorité non calculée</div>;
  }

  const level = priorityLevel(score.globalScore);

  return (
    <div className={`priority-badge priority-${level.key} ${compact ? 'priority-compact' : ''}`} title={`${level.label} · ${score.globalScore}`}>
      <strong>{compact ? level.shortLabel : level.label}</strong>
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

function DetailSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="detail-section">
      <h3>{title}</h3>
      {children}
    </section>
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
    <DetailSection title="Actions">
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
    return <DetailSection title="Détail du score"><p className="muted detail-empty">Score non calculé.</p></DetailSection>;
  }

  const items = [
    ['Stack', score.stackScore],
    ['Rôle', score.roleScore],
    ['Domaine', score.domainScore],
    ['Séniorité', score.seniorityScore],
    ['Localisation', score.locationScore],
    ['Salaire', score.salaryScore]
  ] as const;

  return (
    <DetailSection title="Détail du score">
      <p className="muted score-help">Ce score sert à prioriser les entreprises selon les données disponibles.</p>
      <div className="score-breakdown">
        {items.map(([label, value]) => (
          <div key={label} className="score-row">
            <div>
              <span>{label}</span>
              <strong>{value}</strong>
            </div>
            <div className="score-bar" aria-label={`${label} ${value}`}>
              <span style={{ width: `${Math.min(100, Math.max(0, value))}%` }} />
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
    <DetailSection title="Analyse rapide">
      <ReasonList title="Points forts" values={score.positiveReasons} tone="positive" />
      <ReasonList title="Points faibles" values={score.negativeReasons} tone="negative" />
      <div className="analysis-block">
        <strong>Compétences manquantes</strong>
        {score.missingSkills.length > 0 ? <InfoChips values={score.missingSkills} /> : <p className="muted detail-empty">Aucune compétence manquante détectée.</p>}
      </div>
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
  if (score >= 80) return { key: 'top', label: 'Cible forte', shortLabel: 'Forte' };
  if (score >= 60) return { key: 'high', label: 'Entreprise prioritaire', shortLabel: 'Prioritaire' };
  if (score >= 40) return { key: 'correct', label: 'Potentiel correct', shortLabel: 'Correct' };
  if (score >= 20) return { key: 'watch', label: 'Entreprise à surveiller', shortLabel: 'Surveiller' };
  return { key: 'low', label: 'Priorité actuelle faible', shortLabel: 'Faible' };
}

function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('');
}
