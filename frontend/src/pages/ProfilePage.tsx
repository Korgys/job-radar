import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { api } from '../api';
import { ImportBox } from '../components/ImportBox';
import type { CandidateProfile, Company, Job } from '../types';
import { normalize, unique } from './shared';

type ProfileDraft = {
  detectedSkills: string[];
  detectedRoles: string[];
  detectedDomains: string[];
  detectedSeniority: string;
  preferredLocations: string[];
  remotePreference: string;
  targetSalary: string;
};

const emptyDraft: ProfileDraft = {
  detectedSkills: [],
  detectedRoles: [],
  detectedDomains: [],
  detectedSeniority: '',
  preferredLocations: [],
  remotePreference: '',
  targetSalary: ''
};

const seniorityOptions = ['', 'junior', 'confirmé', 'senior', 'lead'];

const fallbackRoleOptions = [
  'développeur backend',
  'développeur fullstack',
  'tech lead',
  'lead developer',
  'devops'
];

export function ProfilePage() {
  const [profile, setProfile] = useState<CandidateProfile | null>(null);
  const [draft, setDraft] = useState<ProfileDraft>(emptyDraft);
  const [skillOptions, setSkillOptions] = useState<string[]>([]);
  const [domainOptions, setDomainOptions] = useState<string[]>([]);
  const [roleOptions, setRoleOptions] = useState<string[]>(fallbackRoleOptions);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => {
    void load();
  }, []);

  useEffect(() => {
    setDraft(profile ? toDraft(profile) : emptyDraft);
  }, [profile]);

  const skillSuggestions = useMemo(() => unique(skillOptions.concat(draft.detectedSkills)), [draft.detectedSkills, skillOptions]);
  const domainSuggestions = useMemo(() => unique(domainOptions.concat(draft.detectedDomains)), [domainOptions, draft.detectedDomains]);
  const roleSuggestions = useMemo(() => unique(roleOptions.concat(draft.detectedRoles)), [roleOptions, draft.detectedRoles]);

  async function load() {
    try {
      const [nextProfile, companies, jobs] = await Promise.all([api.profile(), api.companies(), api.jobs()]);
      setProfile(nextProfile);
      setSkillOptions(buildSkillOptions(companies, jobs));
      setDomainOptions(buildDomainOptions(companies, jobs));
      setRoleOptions(buildRoleOptions(jobs));
      setError('');
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : 'Profil introuvable.');
    }
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!profile) {
      return;
    }

    setMessage('');
    setError('');
    try {
      const updated = await api.updateProfile({
        ...draft,
        remotePreference: draft.remotePreference.trim() || null,
        targetSalary: draft.targetSalary.trim() ? Number(draft.targetSalary) : null
      });
      setProfile(updated);
      setMessage('Profil mis à jour.');
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : 'Mise à jour impossible.');
    }
  }

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Profil</h1>
          <p className="muted">Extraction déterministe depuis un CV texte ou Markdown.</p>
        </div>
      </div>

      <div className="grid two">
        <section className="grid">
          <ImportBox
            accept=".txt,.md,text/plain,text/markdown"
            label="Importer CV .txt ou .md"
            onUpload={api.uploadProfile}
            onDone={(result) => {
              setProfile(result);
              setMessage('Profil importé.');
            }}
          />
          {error && <p className="status">{error}</p>}
          {message && <p className="status">{message}</p>}
          {profile && (
            <form className="panel profile-form" onSubmit={save}>
              <h2>Profil extrait</h2>
              <label>
                Séniorité
                <select value={draft.detectedSeniority} onChange={(event) => setDraft({ ...draft, detectedSeniority: event.target.value })}>
                  {seniorityOptions.map((value) => (
                    <option key={value} value={value}>{value || 'Non renseignée'}</option>
                  ))}
                </select>
              </label>
              <EditableTagList
                label="Compétences"
                values={draft.detectedSkills}
                options={skillSuggestions}
                datalistId="profile-skill-options"
                placeholder="Ajouter une compétence"
                onChange={(detectedSkills) => setDraft({ ...draft, detectedSkills })}
              />
              <EditableTagList
                label="Rôles"
                values={draft.detectedRoles}
                options={roleSuggestions}
                datalistId="profile-role-options"
                placeholder="Ajouter un rôle"
                onChange={(detectedRoles) => setDraft({ ...draft, detectedRoles })}
              />
              <EditableTagList
                label="Localisations préférées"
                values={draft.preferredLocations}
                options={[]}
                datalistId="profile-location-options"
                placeholder="Ajouter une ville ou région"
                onChange={(preferredLocations) => setDraft({ ...draft, preferredLocations })}
              />
              <label>
                Préférence télétravail
                <input
                  value={draft.remotePreference}
                  maxLength={80}
                  onChange={(event) => setDraft({ ...draft, remotePreference: event.target.value })}
                  placeholder="remote, hybride, présentiel..."
                />
              </label>
              <label>
                Salaire cible annuel
                <input
                  type="number"
                  min="0"
                  step="1000"
                  value={draft.targetSalary}
                  onChange={(event) => setDraft({ ...draft, targetSalary: event.target.value })}
                  placeholder="60000"
                />
              </label>
              <EditableTagList
                label="Domaines"
                values={draft.detectedDomains}
                options={domainSuggestions}
                datalistId="profile-domain-options"
                placeholder="Ajouter un domaine"
                onChange={(detectedDomains) => setDraft({ ...draft, detectedDomains })}
              />
              <div className="profile-actions">
                <button type="submit">Enregistrer</button>
              </div>
            </form>
          )}
        </section>

        <aside className="panel">
          <h2>Texte CV</h2>
          {profile ? <pre className="pre-box">{profile.rawText}</pre> : <p className="muted">Aucun CV importé.</p>}
        </aside>
      </div>
    </>
  );
}

function EditableTagList({
  label,
  values,
  options,
  datalistId,
  placeholder,
  onChange
}: {
  label: string;
  values: string[];
  options: string[];
  datalistId: string;
  placeholder: string;
  onChange: (values: string[]) => void;
}) {
  const [draft, setDraft] = useState('');
  const suggestions = options
    .filter((option) => !values.some((value) => normalize(value) === normalize(option)))
    .filter((option) => !draft || normalize(option).includes(normalize(draft)))
    .slice(0, 10);

  function add(value: string) {
    const next = value.trim().slice(0, 60);
    if (next && !values.some((item) => normalize(item) === normalize(next))) {
      onChange([...values, next]);
    }
    setDraft('');
  }

  return (
    <label className="profile-combo">
      {label}
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
          list={datalistId}
          maxLength={60}
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              add(draft);
            }
          }}
          placeholder={placeholder}
        />
        <datalist id={datalistId}>
          {suggestions.map((item) => (
            <option key={item} value={item} />
          ))}
        </datalist>
        <button type="button" onClick={() => add(draft)}>Ajouter</button>
      </div>
    </label>
  );
}

function toDraft(profile: CandidateProfile): ProfileDraft {
  return {
    detectedSkills: profile.detectedSkills,
    detectedRoles: profile.detectedRoles,
    detectedDomains: profile.detectedDomains,
    detectedSeniority: profile.detectedSeniority,
    preferredLocations: profile.preferredLocations ?? [],
    remotePreference: profile.remotePreference ?? '',
    targetSalary: profile.targetSalary?.toString() ?? ''
  };
}

function buildSkillOptions(companies: Company[], jobs: Job[]) {
  return unique(companies.flatMap((company) => company.knownStack).concat(jobs.flatMap((job) => job.stack)));
}

function buildDomainOptions(companies: Company[], jobs: Job[]) {
  return unique(companies.flatMap((company) => [company.domain, ...company.secondaryDomains]).concat(jobs.map((job) => job.companyDomain)));
}

function buildRoleOptions(jobs: Job[]) {
  return unique(
    jobs
      .flatMap((job) => [job.title, job.jobType ?? ''])
      .filter((value) => value.trim().length > 0)
      .concat(fallbackRoleOptions)
  );
}
