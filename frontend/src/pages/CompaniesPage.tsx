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

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Entreprises</h1>
          <p className="muted">{filtered.length} entreprises suivies.</p>
        </div>
      </div>

      <ImportBox accept=".csv" label="Importer des entreprises CSV" onUpload={api.uploadCompanies} onDone={load} />

      <div className="grid two">
        <section className="panel">
          <div className="toolbar">
            <label>
              Rechercher
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Nom, ville, domaine, stack" />
            </label>
          </div>
          <div className="table-wrap">
            <table>
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
                  <tr key={company.id} className="clickable" onClick={() => setSelected(company)}>
                    <td>{company.name}</td>
                    <td>
                      <span className="domain-dot" style={{ background: domainColor(company.domain) }} />
                      {company.domain}
                    </td>
                    <td>{company.city}</td>
                    <td>{formatList(company.knownStack)}</td>
                    <td>{company.jobCount}</td>
                    <td>{company.score?.globalScore ?? '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <CompanyDetail company={selected} />
      </div>
    </>
  );
}

export function CompanyDetail({ company }: { company: Company | null }) {
  if (!company) {
    return <aside className="panel muted">Aucune entreprise sélectionnée.</aside>;
  }

  return (
    <aside className="panel company-detail-card">
      <CompanyDetailHeader company={company} />
      <div className="company-detail-layout">
        <div className="company-detail-main">
          <DetailSection title="Domaines secondaires">
            <InfoChips values={company.secondaryDomains} />
          </DetailSection>
          <DetailSection title="Stack connue">
            <InfoChips values={company.knownStack} />
          </DetailSection>
          <DetailSection title="Notes">
            <p className="detail-note">{company.notes || 'Aucune note renseignée.'}</p>
          </DetailSection>
          <AnalysisQuick score={company.score} />
        </div>
        <div className="company-detail-side">
          <ScoreBreakdown score={company.score} />
          <DetailSection title="Offres liées">
            <p className="linked-jobs">
              {company.jobCount > 0 ? `${company.jobCount} offre${company.jobCount > 1 ? 's' : ''}` : 'Aucune offre liée détectée pour le moment.'}
            </p>
          </DetailSection>
          <CompanyLinks company={company} />
        </div>
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
            <span className="domain-dot" style={{ background: domainColor(company.domain) }} />
            {company.domain} · {company.city}
          </p>
        </div>
      </div>
      <ScoreBadge score={company.score} />
    </div>
  );
}

function ScoreBadge({ score }: { score?: Score | null }) {
  if (!score) {
    return <div className="score-badge score-empty">Score non calculé</div>;
  }

  const level = scoreLevel(score.globalScore);

  return (
    <div className={`score-badge score-${level.key}`}>
      <strong>{score.globalScore}/100</strong>
      <span>{level.label}</span>
    </div>
  );
}

function DetailSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="detail-section">
      <h3>{title}</h3>
      {children}
    </section>
  );
}

function InfoChips({ values }: { values: string[] }) {
  if (values.length === 0) {
    return <p className="muted detail-empty">Non renseigné</p>;
  }

  return (
    <div className="info-chip-list">
      {values.map((value) => (
        <span key={value} className="info-chip">{value}</span>
      ))}
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

function scoreLevel(score: number) {
  if (score >= 80) return { key: 'great', label: 'Très bonne compatibilité' };
  if (score >= 60) return { key: 'good', label: 'Bonne compatibilité' };
  if (score >= 30) return { key: 'medium', label: 'Compatibilité moyenne' };
  return { key: 'low', label: 'Compatibilité faible' };
}

function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('');
}
