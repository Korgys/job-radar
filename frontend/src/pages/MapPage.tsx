import L from 'leaflet';
import type { CSSProperties } from 'react';
import { useEffect, useMemo, useState } from 'react';
import { MapContainer, Marker, Popup, TileLayer, useMap } from 'react-leaflet';
import { api } from '../api';
import type { Company, Job } from '../types';
import { CompanyDetail } from './CompaniesPage';
import { domainColor, matchesText, normalize, unique } from './shared';

type JobPresenceFilter = 'all' | 'with' | 'without';

type Filters = {
  domains: string[];
  stacks: string[];
  seniority: string;
  jobPresence: JobPresenceFilter;
  minScore: string;
  search: string;
};

const emptyFilters: Filters = {
  domains: [],
  stacks: [],
  seniority: '',
  jobPresence: 'all',
  minScore: '0',
  search: ''
};

export function MapPage() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [selected, setSelected] = useState<Company | null>(null);
  const [filters, setFilters] = useState<Filters>(emptyFilters);

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

  const filteredCompanies = useMemo(() => {
    return companies.filter((company) => companyMatches(company, jobsByCompany[company.id] ?? [], filters));
  }, [companies, filters, jobsByCompany]);

  useEffect(() => {
    if (selected && filteredCompanies.some((company) => company.id === selected.id)) {
      return;
    }
    setSelected(filteredCompanies[0] ?? null);
  }, [filteredCompanies, selected]);

  const markerGroups = useMemo(() => groupCompanies(filteredCompanies), [filteredCompanies]);
  const sortedCompanies = useMemo(() => [...filteredCompanies].sort((left, right) => compareCompanies(left, right, jobsByCompany)), [filteredCompanies, jobsByCompany]);
  const selectedJobs = useMemo(() => selected ? [...(jobsByCompany[selected.id] ?? [])].sort(compareJobs) : [], [jobsByCompany, selected]);
  const visibleJobCount = useMemo(() => filteredCompanies.reduce((sum, company) => sum + (jobsByCompany[company.id]?.length ?? 0), 0), [filteredCompanies, jobsByCompany]);
  const center = markerGroups[0] ? [markerGroups[0].lat, markerGroups[0].lng] as [number, number] : [48.5839, 7.7455] as [number, number];

  return (
    <div className="map-page">
      <div className="page-header">
        <div>
          <h1>Carte</h1>
          <p className="muted">{filteredCompanies.length} entreprises et {visibleJobCount} offres liées après filtres.</p>
        </div>
        <button type="button" onClick={() => setFilters(emptyFilters)}>Réinitialiser</button>
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
                          {company.domain} · {company.city} · score {companyEffectiveScore(company, jobsByCompany[company.id] ?? [])}
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
        </div>

        <aside className="panel side-list">
          {sortedCompanies.map((company) => (
            <div key={company.id} className={`list-item ${selected?.id === company.id ? 'selected' : ''}`} onClick={() => setSelected(company)}>
              <strong>{company.name}</strong>
              <p>
                <span className="domain-dot" style={{ background: domainColor(company.domain) }} />
                {company.domain} · {company.city}
              </p>
              <p className="muted">{jobsByCompany[company.id]?.length ?? 0} offres · score {companyEffectiveScore(company, jobsByCompany[company.id] ?? [])}</p>
            </div>
          ))}
        </aside>
      </div>

      <div className="map-detail-wide">
        <CompanyDetail company={selected} jobs={selectedJobs} />
      </div>
    </div>
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
    seniorities: unique(jobs.map((job) => job.seniority ?? ''))
  };

  function update<K extends keyof Filters>(key: K, value: Filters[K]) {
    setFilters({ ...filters, [key]: value });
  }

  return (
    <div className="map-filter-panel">
      <DomainFilterChips values={filters.domains} options={options.domains} onChange={(value) => update('domains', value)} />
      <StackFilterInput values={filters.stacks} options={options.stacks} onChange={(value) => update('stacks', value)} />
      <div className="filter-row">
        <Select label="Séniorité" value={filters.seniority} values={options.seniorities} onChange={(value) => update('seniority', value)} />
        <JobPresenceSelect value={filters.jobPresence} onChange={(value) => update('jobPresence', value)} />
        <ScoreStepper value={filters.minScore} onChange={(value) => update('minScore', value)} />
        <SearchFilter value={filters.search} onChange={(value) => update('search', value)} />
      </div>
    </div>
  );
}

function DomainFilterChips({
  values,
  options,
  onChange
}: {
  values: string[];
  options: string[];
  onChange: (value: string[]) => void;
}) {
  function toggle(value: string) {
    onChange(values.some((item) => normalize(item) === normalize(value)) ? values.filter((item) => normalize(item) !== normalize(value)) : [...values, value]);
  }

  return (
    <fieldset className="multi-filter">
      <legend>Domaine</legend>
      <div className="choice-list">
        {options.map((item) => {
          const selected = values.some((value) => normalize(value) === normalize(item));
          return (
            <label key={item} className={`choice domain-choice ${selected ? 'selected' : ''}`} style={{ '--domain-color': domainColor(item) } as CSSProperties}>
              <input type="checkbox" checked={selected} onChange={() => toggle(item)} />
              <span className="domain-dot" style={{ background: domainColor(item) }} />
              {selected && <span className="choice-check">✓</span>}
              {item}
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}

function StackFilterInput({ values, options, onChange }: { values: string[]; options: string[]; onChange: (value: string[]) => void }) {
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
            <button key={item} type="button" className="filter-pill stack-pill" onClick={() => onChange(values.filter((value) => normalize(value) !== normalize(item)))} aria-label={`Retirer ${item}`}>
              <span>{item}</span>
              <span aria-hidden="true">×</span>
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

function SearchFilter({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return (
    <label className="search-filter">
      Recherche
      <div className="search-box">
        <input value={value} onChange={(event) => onChange(event.target.value)} placeholder="Nom, entreprise, offre, stack" />
        {value && <button type="button" onClick={() => onChange('')} aria-label="Vider la recherche">×</button>}
      </div>
    </label>
  );
}

function clampScore(value: number) {
  if (Number.isNaN(value)) {
    return 0;
  }

  return Math.min(100, Math.max(0, Math.round(value / 5) * 5));
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

function JobPresenceSelect({ value, onChange }: { value: JobPresenceFilter; onChange: (value: JobPresenceFilter) => void }) {
  return (
    <label>
      Offres
      <select value={value} onChange={(event) => onChange(event.target.value as JobPresenceFilter)}>
        <option value="all">Toutes</option>
        <option value="with">Avec offres</option>
        <option value="without">Sans offre</option>
      </select>
    </label>
  );
}

function companyMatches(company: Company, companyJobs: Job[], filters: Filters) {
  const minScore = Number(filters.minScore || 0);
  if (filters.domains.length > 0 && !filters.domains.some((domain) => normalize(company.domain) === normalize(domain))) return false;
  if (companyEffectiveScore(company, companyJobs) < minScore) return false;
  if (filters.jobPresence === 'with' && companyJobs.length === 0) return false;
  if (filters.jobPresence === 'without' && companyJobs.length > 0) return false;
  if (filters.stacks.length > 0 && !filters.stacks.every((stack) => includesValue(company.knownStack, stack) || companyJobs.some((job) => includesValue(job.stack, stack)))) return false;
  if (filters.seniority && !companyJobs.some((job) => normalize(job.seniority) === normalize(filters.seniority))) return false;
  if (filters.search && !matchesText(filters.search, company.name, company.city, company.domain, company.knownStack.join(' '), company.notes ?? '', companyJobs.map((job) => `${job.title} ${job.stack.join(' ')}`).join(' '))) return false;
  return true;
}

function compareCompanies(left: Company, right: Company, jobsByCompany: Record<number, Job[]>) {
  return companyEffectiveScore(right, jobsByCompany[right.id] ?? []) - companyEffectiveScore(left, jobsByCompany[left.id] ?? [])
    || (jobsByCompany[right.id]?.length ?? 0) - (jobsByCompany[left.id]?.length ?? 0);
}

function compareJobs(left: Job, right: Job) {
  return (right.score?.globalScore ?? 0) - (left.score?.globalScore ?? 0) || left.title.localeCompare(right.title);
}

function companyEffectiveScore(company: Company, jobs: Job[]) {
  return Math.max(company.score?.globalScore ?? 0, ...jobs.map((job) => job.score?.globalScore ?? 0));
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
