import { useEffect, useMemo, useState } from 'react';
import { api } from '../api';
import { ImportBox } from '../components/ImportBox';
import { ScorePanel } from '../components/ScorePanel';
import type { Company } from '../types';
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
          <p className="muted">{filtered.length} entreprises affichées.</p>
        </div>
      </div>

      <div className="grid two">
        <section className="grid">
          <ImportBox accept=".csv,text/csv" label="Importer companies.csv" onUpload={api.uploadCompanies} onDone={() => void load()} />
          <div className="panel">
            <div className="toolbar">
              <label>
                Recherche
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
    <aside className="panel grid">
      <div>
        {company.logoUrl && <img className="detail-logo" src={company.logoUrl} alt="" />}
        <h2>{company.name}</h2>
        <p>
          <span className="domain-dot" style={{ background: domainColor(company.domain) }} />
          {company.domain} · {company.city}
        </p>
      </div>

      <div>
        <strong>Domaines secondaires</strong>
        <p>{formatList(company.secondaryDomains)}</p>
      </div>
      <div>
        <strong>Stack connue</strong>
        <p>{formatList(company.knownStack)}</p>
      </div>
      <div>
        <strong>Offres liées</strong>
        <p>{company.jobCount}</p>
      </div>
      <LinkList company={company} />
      {company.notes && (
        <div>
          <strong>Notes</strong>
          <p>{company.notes}</p>
        </div>
      )}
      <ScorePanel score={company.score} />
    </aside>
  );
}

function LinkList({ company }: { company: Company }) {
  const links = [
    ['Site', company.website],
    ['Carrière', company.careerUrl],
    ['LinkedIn', company.linkedinUrl],
    ['Glassdoor', company.glassdoorUrl]
  ].filter(([, url]) => Boolean(url));

  if (links.length === 0) {
    return null;
  }

  return (
    <div className="link-list">
      {links.map(([label, url]) => (
        <a key={label} href={url ?? '#'} target="_blank" rel="noreferrer">
          {label}
        </a>
      ))}
    </div>
  );
}
