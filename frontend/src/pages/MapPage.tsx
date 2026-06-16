import L from 'leaflet';
import { useEffect, useMemo, useState } from 'react';
import { MapContainer, Marker, Popup, TileLayer, useMap } from 'react-leaflet';
import { api } from '../api';
import { CompanyDetail } from './CompaniesPage';
import { JobDetail } from './JobsPage';
import type { Company, Job } from '../types';
import { domainColor, formatList, matchesText, normalize, unique } from './shared';

type Filters = {
  domains: string[];
  stacks: string[];
  seniority: string;
  remote: string;
  minScore: string;
  search: string;
};

type SectorBounds = {
  north: number;
  south: number;
  east: number;
  west: number;
};

const emptyFilters: Filters = {
  domains: [],
  stacks: [],
  seniority: '',
  remote: '',
  minScore: '',
  search: ''
};

export function MapPage() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [selected, setSelected] = useState<Company | null>(null);
  const [selectedJob, setSelectedJob] = useState<Job | null>(null);
  const [filters, setFilters] = useState<Filters>(emptyFilters);
  const [sectorBounds, setSectorBounds] = useState<SectorBounds | null>(null);

  useEffect(() => {
    void load();
  }, []);

  async function load() {
    const [nextCompanies, nextJobs] = await Promise.all([api.companies(), api.jobs()]);
    setCompanies(nextCompanies);
    setJobs(nextJobs);
    setSelected(nextCompanies[0] ?? null);
  }

  const jobsByCompany = useMemo(() => {
    return jobs.reduce<Record<number, Job[]>>((accumulator, job) => {
      accumulator[job.companyId] = [...(accumulator[job.companyId] ?? []), job];
      return accumulator;
    }, {});
  }, [jobs]);

  const companiesById = useMemo(() => {
    return companies.reduce<Record<number, Company>>((accumulator, company) => {
      accumulator[company.id] = company;
      return accumulator;
    }, {});
  }, [companies]);

  const filteredCompanies = useMemo(() => {
    return companies.filter((company) => companyMatches(company, jobsByCompany[company.id] ?? [], filters));
  }, [companies, filters, jobsByCompany]);

  const filteredJobs = useMemo(() => {
    return jobs
      .filter((job) => jobMatches(job, filters))
      .filter((job) => jobInSector(job, companiesById, sectorBounds))
      .sort(compareJobs);
  }, [companiesById, jobs, filters, sectorBounds]);

  useEffect(() => {
    if (selected && filteredCompanies.some((company) => company.id === selected.id)) {
      return;
    }
    setSelected(filteredCompanies[0] ?? null);
  }, [filteredCompanies, selected]);

  const markerGroups = useMemo(() => groupCompanies(filteredCompanies), [filteredCompanies]);
  const sortedCompanies = useMemo(() => [...filteredCompanies].sort(compareCompanies), [filteredCompanies]);
  const center = markerGroups[0] ? [markerGroups[0].lat, markerGroups[0].lng] as [number, number] : [48.5839, 7.7455] as [number, number];

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Carte</h1>
          <p className="muted">{filteredCompanies.length} entreprises et {filteredJobs.length} offres après filtres.</p>
        </div>
        <button type="button" onClick={() => {
          setFilters(emptyFilters);
          setSectorBounds(null);
        }}>Réinitialiser</button>
      </div>

      <section className="panel">
        <div className="map-controls">
          <FilterControls filters={filters} setFilters={setFilters} companies={companies} jobs={jobs} />
        </div>
      </section>

      <div className="map-layout">
        <div className="map-main-column">
        <section className="map-shell">
          <MapContainer center={center} zoom={10} scrollWheelZoom>
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <ScaleControl />
            <SectorButton active={Boolean(sectorBounds)} onUpdate={setSectorBounds} />
            {markerGroups.map((group) => (
              <Marker
                key={group.key}
                position={[group.lat, group.lng]}
                icon={markerIcon(domainColor(group.companies[0].domain), group.companies.length)}
                eventHandlers={{ click: () => setSelected(group.companies[0]) }}
              >
                <Popup>
                  <div className="popup-content">
                    {group.companies.slice(0, 5).map((company) => (
                      <div key={company.id}>
                        <strong>{company.name}</strong>
                        <br />
                        {company.domain} · {company.city} · {company.score?.globalScore ?? '-'} / 100
                        <br />
                        <button type="button" onClick={() => setSelected(company)}>Voir détail</button>
                      </div>
                    ))}
                    {group.companies.length > 5 && (
                      <p className="muted popup-more">et {group.companies.length - 5} autres entreprises</p>
                    )}
                  </div>
                </Popup>
              </Marker>
            ))}
          </MapContainer>
        </section>
        {selectedJob && <JobDetail job={selectedJob} />}
        </div>

        <aside className="grid">
          <div className="panel side-list">
            {sortedCompanies.map((company) => (
              <div key={company.id} className={`list-item ${selected?.id === company.id ? 'selected' : ''}`} onClick={() => setSelected(company)}>
                <strong>{company.name}</strong>
                <p>
                  <span className="domain-dot" style={{ background: domainColor(company.domain) }} />
                  {company.domain} · {company.city}
                </p>
                <p className="muted">{company.jobCount} offres · score {company.score?.globalScore ?? '-'}</p>
              </div>
            ))}
          </div>
          <CompanyDetail company={selected} />
        </aside>
      </div>

      <section className="panel">
        <h2>Offres filtrées</h2>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Titre</th>
                <th>Entreprise</th>
                <th>Localisation</th>
                <th>Séniorité</th>
                <th>Stack</th>
                <th>Score</th>
              </tr>
            </thead>
            <tbody>
              {filteredJobs.map((job) => (
                <tr key={job.id} className="clickable" onClick={() => setSelectedJob(job)}>
                  <td>{job.url ? <a href={job.url} target="_blank" rel="noreferrer">{job.title}</a> : job.title}</td>
                  <td>{job.companyName}</td>
                  <td>{job.location ?? '-'}</td>
                  <td>{job.seniority ?? '-'}</td>
                  <td>{formatList(job.stack)}</td>
                  <td>{job.score?.globalScore ?? '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </>
  );
}

function FilterControls({
  filters,
  setFilters,
  companies,
  jobs
}: {
  filters: Filters;
  setFilters: (filters: Filters) => void;
  companies: Company[];
  jobs: Job[];
}) {
  const options = {
    domains: unique(companies.map((company) => company.domain)),
    stacks: unique(companies.flatMap((company) => company.knownStack).concat(jobs.flatMap((job) => job.stack))),
    seniorities: unique(jobs.map((job) => job.seniority ?? '')),
    remotes: unique(jobs.map((job) => job.remotePolicy ?? ''))
  };

  function update<K extends keyof Filters>(key: K, value: Filters[K]) {
    setFilters({ ...filters, [key]: value });
  }

  return (
    <div className="map-filter-panel">
      <MultiSelect label="Domaine" values={filters.domains} options={options.domains} colored onChange={(value) => update('domains', value)} />
      <StackCombo values={filters.stacks} options={options.stacks} onChange={(value) => update('stacks', value)} />
      <div className="filter-row">
      <Select label="Séniorité" value={filters.seniority} values={options.seniorities} onChange={(value) => update('seniority', value)} />
      <Select label="Télétravail" value={filters.remote} values={options.remotes} onChange={(value) => update('remote', value)} />
      <label>
        Score minimum
        <input type="number" min="0" max="100" value={filters.minScore} onChange={(event) => update('minScore', event.target.value)} />
      </label>
      <label className="search-filter">
        Recherche
        <input value={filters.search} onChange={(event) => update('search', event.target.value)} placeholder="Nom, offre, stack" />
      </label>
      </div>
    </div>
  );
}

function MultiSelect({
  label,
  values,
  options,
  colored = false,
  onChange
}: {
  label: string;
  values: string[];
  options: string[];
  colored?: boolean;
  onChange: (value: string[]) => void;
}) {
  function toggle(value: string) {
    onChange(values.some((item) => normalize(item) === normalize(value)) ? values.filter((item) => normalize(item) !== normalize(value)) : [...values, value]);
  }

  return (
    <fieldset className="multi-filter">
      <legend>{label}</legend>
      <div className="choice-list">
        {options.map((item) => {
          const selected = values.some((value) => normalize(value) === normalize(item));
          return (
            <label key={item} className={`choice ${selected ? 'selected' : ''}`}>
              <input type="checkbox" checked={selected} onChange={() => toggle(item)} />
              {colored && <span className="domain-dot" style={{ background: domainColor(item) }} />}
              {item}
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}

function StackCombo({ values, options, onChange }: { values: string[]; options: string[]; onChange: (value: string[]) => void }) {
  const [draft, setDraft] = useState('');
  const suggestions = options
    .filter((option) => !values.some((value) => normalize(value) === normalize(option)))
    .filter((option) => !draft || normalize(option).includes(normalize(draft)))
    .slice(0, 8);

  function add(value: string) {
    const next = value.trim().slice(0, 30);
    if (next && !values.some((item) => normalize(item) === normalize(next))) {
      onChange([...values, next]);
    }
    setDraft('');
  }

  return (
    <label className="stack-combo">
      Stack
      <div className="combo-box">
        <div className="pill-list">
          {values.map((item) => (
            <button key={item} type="button" className="filter-pill" onClick={() => onChange(values.filter((value) => value !== item))}>
              {item} x
            </button>
          ))}
        </div>
        <input
          value={draft}
          maxLength={30}
          list="stack-options"
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              add(draft);
            }
          }}
          placeholder="Ajouter une stack"
        />
        <datalist id="stack-options">
          {suggestions.map((item) => (
            <option key={item} value={item} />
          ))}
        </datalist>
        <button type="button" onClick={() => add(draft)}>Ajouter</button>
      </div>
    </label>
  );
}

function Select({ label, value, values, onChange }: { label: string; value: string; values: string[]; onChange: (value: string) => void }) {
  return (
    <label>
      {label}
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">Tous</option>
        {values.map((item) => (
          <option key={item} value={item}>{item}</option>
        ))}
      </select>
    </label>
  );
}

function companyMatches(company: Company, companyJobs: Job[], filters: Filters) {
  const minScore = Number(filters.minScore || 0);
  if (filters.domains.length > 0 && !filters.domains.some((domain) => normalize(company.domain) === normalize(domain))) return false;
  if (filters.minScore && (company.score?.globalScore ?? 0) < minScore) return false;
  if (filters.stacks.length > 0 && !filters.stacks.every((stack) => includesValue(company.knownStack, stack) || companyJobs.some((job) => includesValue(job.stack, stack)))) return false;
  if (filters.seniority && !companyJobs.some((job) => normalize(job.seniority) === normalize(filters.seniority))) return false;
  if (filters.remote && !companyJobs.some((job) => normalize(job.remotePolicy) === normalize(filters.remote))) return false;
  if (filters.search && !matchesText(filters.search, company.name, company.city, company.domain, company.knownStack.join(' '), company.notes ?? '', companyJobs.map((job) => `${job.title} ${job.stack.join(' ')}`).join(' '))) return false;
  return true;
}

function jobMatches(job: Job, filters: Filters) {
  const minScore = Number(filters.minScore || 0);
  if (filters.domains.length > 0 && !filters.domains.some((domain) => normalize(job.companyDomain) === normalize(domain))) return false;
  if (filters.minScore && (job.score?.globalScore ?? 0) < minScore) return false;
  if (filters.stacks.length > 0 && !filters.stacks.every((stack) => includesValue(job.stack, stack))) return false;
  if (filters.seniority && normalize(job.seniority) !== normalize(filters.seniority)) return false;
  if (filters.remote && normalize(job.remotePolicy) !== normalize(filters.remote)) return false;
  if (filters.search && !matchesText(filters.search, job.title, job.companyName, job.location ?? '', job.jobType ?? '', job.stack.join(' '), job.description ?? '')) return false;
  return true;
}

function jobInSector(job: Job, companiesById: Record<number, Company>, bounds: SectorBounds | null) {
  if (!bounds) {
    return true;
  }

  const company = companiesById[job.companyId];
  if (company?.latitude == null || company.longitude == null) {
    return false;
  }

  return company.latitude <= bounds.north
    && company.latitude >= bounds.south
    && company.longitude <= bounds.east
    && company.longitude >= bounds.west;
}

function compareCompanies(left: Company, right: Company) {
  return (right.score?.globalScore ?? 0) - (left.score?.globalScore ?? 0) || right.jobCount - left.jobCount;
}

function compareJobs(left: Job, right: Job) {
  return (right.score?.globalScore ?? 0) - (left.score?.globalScore ?? 0);
}

function includesValue(values: string[], expected: string) {
  return values.some((value) => normalize(value) === normalize(expected));
}

function groupCompanies(companies: Company[]) {
  const groups = new Map<string, { key: string; lat: number; lng: number; companies: Company[] }>();

  for (const company of companies) {
    if (company.latitude == null || company.longitude == null) {
      continue;
    }

    const key = `${company.latitude.toFixed(3)}:${company.longitude.toFixed(3)}`;
    const existing = groups.get(key);
    if (existing) {
      existing.companies.push(company);
      existing.lat = existing.companies.reduce((sum, item) => sum + (item.latitude ?? 0), 0) / existing.companies.length;
      existing.lng = existing.companies.reduce((sum, item) => sum + (item.longitude ?? 0), 0) / existing.companies.length;
    } else {
      groups.set(key, { key, lat: company.latitude, lng: company.longitude, companies: [company] });
    }
  }

  return Array.from(groups.values());
}

function markerIcon(color: string, count: number) {
  const label = count > 1 ? String(count) : '';
  return L.divIcon({
    className: 'radar-marker',
    html: `<span style="background:${color}"><b>${label}</b></span>`,
    iconSize: [28, 28],
    iconAnchor: [14, 28]
  });
}

function SectorButton({ active, onUpdate }: { active: boolean; onUpdate: (bounds: SectorBounds) => void }) {
  const map = useMap();

  function updateSector() {
    const bounds = map.getBounds();
    onUpdate({
      north: bounds.getNorth(),
      south: bounds.getSouth(),
      east: bounds.getEast(),
      west: bounds.getWest()
    });
  }

  return (
    <div className="map-sector-action leaflet-control">
      <button type="button" className={active ? 'active' : ''} onClick={updateSector}>
        Actualiser les offres du secteur
      </button>
    </div>
  );
}

function ScaleControl() {
  const map = useMap();

  useEffect(() => {
    const control = L.control.scale({ imperial: false, metric: true });
    control.addTo(map);

    return () => {
      control.remove();
    };
  }, [map]);

  return null;
}
