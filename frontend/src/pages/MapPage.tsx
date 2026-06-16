import L from 'leaflet';
import { useEffect, useMemo, useState } from 'react';
import { MapContainer, Marker, Popup, TileLayer, useMap } from 'react-leaflet';
import { api } from '../api';
import { CompanyDetail } from './CompaniesPage';
import type { Company, Job } from '../types';
import { domainColor, formatList, matchesText, normalize, unique } from './shared';

type Filters = {
  domain: string;
  city: string;
  stack: string;
  jobType: string;
  seniority: string;
  remote: string;
  minScore: string;
  search: string;
};

const emptyFilters: Filters = {
  domain: '',
  city: '',
  stack: '',
  jobType: '',
  seniority: '',
  remote: '',
  minScore: '',
  search: ''
};

export function MapPage() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [selected, setSelected] = useState<Company | null>(null);
  const [filters, setFilters] = useState<Filters>(emptyFilters);
  const [baseLayer, setBaseLayer] = useState<'osm' | 'local'>('osm');

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

  const filteredJobs = useMemo(() => {
    return jobs.filter((job) => jobMatches(job, filters));
  }, [jobs, filters]);

  useEffect(() => {
    if (selected && filteredCompanies.some((company) => company.id === selected.id)) {
      return;
    }
    setSelected(filteredCompanies[0] ?? null);
  }, [filteredCompanies, selected]);

  const markerGroups = useMemo(() => groupCompanies(filteredCompanies), [filteredCompanies]);
  const center = markerGroups[0] ? [markerGroups[0].lat, markerGroups[0].lng] as [number, number] : [48.5839, 7.7455] as [number, number];

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Carte</h1>
          <p className="muted">{filteredCompanies.length} entreprises et {filteredJobs.length} offres après filtres.</p>
        </div>
        <button type="button" onClick={() => setFilters(emptyFilters)}>Réinitialiser</button>
      </div>

      <section className="panel">
        <div className="map-controls">
          <FilterControls filters={filters} setFilters={setFilters} companies={companies} jobs={jobs} />
          <label className="base-layer-control">
            Fond de carte
            <select value={baseLayer} onChange={(event) => setBaseLayer(event.target.value as 'osm' | 'local')}>
              <option value="osm">OpenStreetMap</option>
              <option value="local">Local schematique</option>
            </select>
          </label>
        </div>
      </section>

      <div className="map-layout">
        <section className="map-shell">
          <MapContainer center={center} zoom={10} scrollWheelZoom>
            {baseLayer === 'osm' ? (
              <TileLayer
                attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              />
            ) : (
              <LocalMapLayer />
            )}
            {markerGroups.map((group) => (
              <Marker
                key={group.key}
                position={[group.lat, group.lng]}
                icon={markerIcon(domainColor(group.companies[0].domain), group.companies.length)}
                eventHandlers={{ click: () => setSelected(group.companies[0]) }}
              >
                <Popup>
                  <div className="popup-content">
                    {group.companies.map((company) => (
                      <div key={company.id}>
                        <strong>{company.name}</strong>
                        <br />
                        {company.domain} · {company.city} · {company.score?.globalScore ?? '-'} / 100
                        <br />
                        <button type="button" onClick={() => setSelected(company)}>Voir détail</button>
                      </div>
                    ))}
                  </div>
                </Popup>
              </Marker>
            ))}
          </MapContainer>
        </section>

        <aside className="grid">
          <div className="panel side-list">
            {filteredCompanies.map((company) => (
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
                <th>Type</th>
                <th>Séniorité</th>
                <th>Stack</th>
                <th>Score</th>
              </tr>
            </thead>
            <tbody>
              {filteredJobs.map((job) => (
                <tr key={job.id}>
                  <td>{job.url ? <a href={job.url} target="_blank" rel="noreferrer">{job.title}</a> : job.title}</td>
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
    cities: unique(companies.map((company) => company.city).concat(jobs.map((job) => job.location ?? ''))),
    stacks: unique(companies.flatMap((company) => company.knownStack).concat(jobs.flatMap((job) => job.stack))),
    jobTypes: unique(jobs.map((job) => job.jobType ?? '')),
    seniorities: unique(jobs.map((job) => job.seniority ?? '')),
    remotes: unique(jobs.map((job) => job.remotePolicy ?? ''))
  };

  function update<K extends keyof Filters>(key: K, value: Filters[K]) {
    setFilters({ ...filters, [key]: value });
  }

  return (
    <div className="filter-grid">
      <Select label="Domaine" value={filters.domain} values={options.domains} onChange={(value) => update('domain', value)} />
      <Select label="Ville" value={filters.city} values={options.cities} onChange={(value) => update('city', value)} />
      <Select label="Stack" value={filters.stack} values={options.stacks} onChange={(value) => update('stack', value)} />
      <Select label="Type de poste" value={filters.jobType} values={options.jobTypes} onChange={(value) => update('jobType', value)} />
      <Select label="Séniorité" value={filters.seniority} values={options.seniorities} onChange={(value) => update('seniority', value)} />
      <Select label="Télétravail" value={filters.remote} values={options.remotes} onChange={(value) => update('remote', value)} />
      <label>
        Score minimum
        <input type="number" min="0" max="100" value={filters.minScore} onChange={(event) => update('minScore', event.target.value)} />
      </label>
      <label>
        Recherche
        <input value={filters.search} onChange={(event) => update('search', event.target.value)} placeholder="Nom, offre, stack" />
      </label>
    </div>
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
  if (filters.domain && normalize(company.domain) !== normalize(filters.domain)) return false;
  if (filters.city && normalize(company.city) !== normalize(filters.city)) return false;
  if (filters.minScore && (company.score?.globalScore ?? 0) < minScore) return false;
  if (filters.stack && !includesValue(company.knownStack, filters.stack) && !companyJobs.some((job) => includesValue(job.stack, filters.stack))) return false;
  if (filters.jobType && !companyJobs.some((job) => normalize(job.jobType) === normalize(filters.jobType))) return false;
  if (filters.seniority && !companyJobs.some((job) => normalize(job.seniority) === normalize(filters.seniority))) return false;
  if (filters.remote && !companyJobs.some((job) => normalize(job.remotePolicy) === normalize(filters.remote))) return false;
  if (filters.search && !matchesText(filters.search, company.name, company.city, company.domain, company.knownStack.join(' '), company.notes ?? '', companyJobs.map((job) => `${job.title} ${job.stack.join(' ')}`).join(' '))) return false;
  return true;
}

function jobMatches(job: Job, filters: Filters) {
  const minScore = Number(filters.minScore || 0);
  if (filters.domain && normalize(job.companyDomain) !== normalize(filters.domain)) return false;
  if (filters.city && normalize(job.location) !== normalize(filters.city)) return false;
  if (filters.minScore && (job.score?.globalScore ?? 0) < minScore) return false;
  if (filters.stack && !includesValue(job.stack, filters.stack)) return false;
  if (filters.jobType && normalize(job.jobType) !== normalize(filters.jobType)) return false;
  if (filters.seniority && normalize(job.seniority) !== normalize(filters.seniority)) return false;
  if (filters.remote && normalize(job.remotePolicy) !== normalize(filters.remote)) return false;
  if (filters.search && !matchesText(filters.search, job.title, job.companyName, job.location ?? '', job.jobType ?? '', job.stack.join(' '), job.description ?? '')) return false;
  return true;
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

function LocalMapLayer() {
  const map = useMap();

  useEffect(() => {
    const layer = new LocalCanvasLayer({ attribution: 'Fond local' });
    layer.addTo(map);
    layer.bringToBack();

    return () => {
      layer.remove();
    };
  }, [map]);

  return null;
}

class LocalCanvasLayer extends L.GridLayer {
  createTile(coords: L.Coords): HTMLElement {
    const tile = document.createElement('canvas');
    const size = this.getTileSize();
    tile.width = size.x;
    tile.height = size.y;

    const context = tile.getContext('2d');
    if (!context) {
      return tile;
    }

    drawBackground(context, size);
    drawRhine(context, this, coords, size);
    drawCityLabels(context, this, coords, size);
    return tile;
  }
}

function drawBackground(context: CanvasRenderingContext2D, size: L.Point) {
  context.fillStyle = '#edf3f2';
  context.fillRect(0, 0, size.x, size.y);

  context.strokeStyle = '#d6e2e1';
  context.lineWidth = 1;
  for (let position = 0; position <= size.x; position += 64) {
    context.beginPath();
    context.moveTo(position, 0);
    context.lineTo(position, size.y);
    context.stroke();
  }
  for (let position = 0; position <= size.y; position += 64) {
    context.beginPath();
    context.moveTo(0, position);
    context.lineTo(size.x, position);
    context.stroke();
  }

  context.strokeStyle = '#c8d7d4';
  context.lineWidth = 2;
  context.beginPath();
  context.moveTo(-20, size.y * 0.78);
  context.bezierCurveTo(size.x * 0.18, size.y * 0.45, size.x * 0.5, size.y * 0.62, size.x + 20, size.y * 0.25);
  context.stroke();
}

function drawRhine(context: CanvasRenderingContext2D, layer: L.GridLayer, coords: L.Coords, size: L.Point) {
  const points = [
    L.latLng(49.02, 8.2),
    L.latLng(48.8, 8.08),
    L.latLng(48.62, 7.86),
    L.latLng(48.45, 7.78),
    L.latLng(48.28, 7.7)
  ].map((latLng) => projectPoint(layer, coords, size, latLng));

  context.strokeStyle = '#8dc6d1';
  context.lineWidth = 5;
  context.beginPath();
  points.forEach((point, index) => {
    if (index === 0) {
      context.moveTo(point.x, point.y);
    } else {
      context.lineTo(point.x, point.y);
    }
  });
  context.stroke();
}

function drawCityLabels(context: CanvasRenderingContext2D, layer: L.GridLayer, coords: L.Coords, size: L.Point) {
  const cities = [
    { name: 'Strasbourg', lat: 48.5846, lng: 7.7507 },
    { name: 'Schiltigheim', lat: 48.6079, lng: 7.7484 },
    { name: 'Obernai', lat: 48.4598, lng: 7.4826 },
    { name: 'Benfeld', lat: 48.3707, lng: 7.5936 },
    { name: 'Fegersheim', lat: 48.4908, lng: 7.6754 },
    { name: 'Haguenau', lat: 48.8156, lng: 7.7886 }
  ];

  context.font = '600 13px Inter, system-ui, sans-serif';
  context.textBaseline = 'middle';

  for (const city of cities) {
    const point = projectPoint(layer, coords, size, L.latLng(city.lat, city.lng));
    if (point.x < -60 || point.x > size.x + 60 || point.y < -30 || point.y > size.y + 30) {
      continue;
    }

    context.fillStyle = '#6f838b';
    context.beginPath();
    context.arc(point.x, point.y, 3, 0, Math.PI * 2);
    context.fill();

    context.fillStyle = 'rgba(255, 255, 255, 0.82)';
    const width = context.measureText(city.name).width + 12;
    context.fillRect(point.x + 7, point.y - 10, width, 20);

    context.fillStyle = '#33444c';
    context.fillText(city.name, point.x + 13, point.y);
  }
}

function projectPoint(layer: L.GridLayer, coords: L.Coords, size: L.Point, latLng: L.LatLng) {
  const map = (layer as L.GridLayer & { _map: L.Map })._map;
  const point = map.project(latLng, coords.z);
  return {
    x: point.x - coords.x * size.x,
    y: point.y - coords.y * size.y
  };
}
