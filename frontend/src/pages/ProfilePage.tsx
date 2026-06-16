import { useEffect, useState } from 'react';
import { api } from '../api';
import { ImportBox } from '../components/ImportBox';
import type { CandidateProfile } from '../types';
import { formatList } from './shared';

export function ProfilePage() {
  const [profile, setProfile] = useState<CandidateProfile | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    void load();
  }, []);

  async function load() {
    try {
      setProfile(await api.profile());
      setError('');
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : 'Profil introuvable.');
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
          <ImportBox accept=".txt,.md,text/plain,text/markdown" label="Importer CV .txt ou .md" onUpload={api.uploadProfile} onDone={(result) => setProfile(result)} />
          {error && <p className="status">{error}</p>}
          {profile && (
            <div className="panel">
              <h2>Profil extrait</h2>
              <p><strong>Séniorité :</strong> {profile.detectedSeniority || 'Non renseignée'}</p>
              <p><strong>Compétences :</strong> {formatList(profile.detectedSkills)}</p>
              <p><strong>Rôles :</strong> {formatList(profile.detectedRoles)}</p>
              <p><strong>Domaines :</strong> {formatList(profile.detectedDomains)}</p>
              <p><strong>Résumé :</strong> {profile.experiencesSummary ?? 'Non renseigné'}</p>
            </div>
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
