import { useEffect, useState } from 'react';
import { api } from '../api';
import type { ReportFile } from '../types';

export function ReportPage() {
  const [reports, setReports] = useState<ReportFile[]>([]);
  const [message, setMessage] = useState('');

  useEffect(() => {
    void load();
  }, []);

  async function load() {
    setReports(await api.reports());
  }

  async function generate(allowUnscored = false) {
    setMessage('');
    try {
      const report = await api.generateReport(allowUnscored);
      setMessage(`Rapport généré : ${report.fileName}`);
      await load();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Génération impossible.');
    }
  }

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Rapports</h1>
          <p className="muted">Fichiers Markdown générés dans data/reports.</p>
        </div>
        <div className="toolbar">
          <button type="button" onClick={() => void generate()}>Générer rapport</button>
          <button type="button" className="secondary-action" onClick={() => void generate(true)}>Générer sans scoring</button>
        </div>
      </div>

      {message && <p className="status">{message}</p>}

      <section className="panel">
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Fichier</th>
                <th>Date</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {reports.map((report) => (
                <tr key={report.fileName}>
                  <td>{report.fileName}</td>
                  <td>{new Date(report.createdAt).toLocaleString()}</td>
                  <td className="link-list">
                    <a href={`/api/reports/${report.fileName}`} target="_blank" rel="noreferrer">Ouvrir</a>
                    <a href={`/api/reports/${report.fileName}`} download={report.fileName}>Télécharger</a>
                  </td>
                </tr>
              ))}
              {reports.length === 0 && (
                <tr>
                  <td colSpan={3} className="muted">Aucun rapport généré.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </>
  );
}
