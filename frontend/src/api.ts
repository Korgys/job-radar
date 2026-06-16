import type { CandidateProfile, Company, DashboardStats, ImportResult, Job, RecalculateResult, ReportFile } from './types';

async function request<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, options);
  if (!response.ok) {
    let message = `Erreur HTTP ${response.status}`;
    try {
      const payload = (await response.json()) as { error?: string };
      message = payload.error ?? message;
    } catch {
      message = await response.text();
    }
    throw new Error(message);
  }

  return (await response.json()) as T;
}

export const api = {
  dashboard: () => request<DashboardStats>('/api/dashboard'),
  companies: () => request<Company[]>('/api/companies'),
  jobs: () => request<Job[]>('/api/jobs'),
  profile: async () => {
    const response = await fetch('/api/profile');
    if (response.status === 404) {
      return null;
    }
    if (!response.ok) {
      throw new Error(await response.text());
    }
    return (await response.json()) as CandidateProfile;
  },
  reports: () => request<ReportFile[]>('/api/reports'),
  uploadCompanies: (file: File) => upload('/api/companies/import-csv', file),
  uploadJobs: (file: File) => upload('/api/jobs/import-csv', file),
  uploadProfile: (file: File) => uploadProfile(file),
  recalculate: () => request<RecalculateResult>('/api/scoring/recalculate', { method: 'POST' }),
  generateReport: () => request<ReportFile>('/api/reports/generate', { method: 'POST' })
};

async function upload(url: string, file: File): Promise<ImportResult> {
  const form = new FormData();
  form.append('file', file);
  return request<ImportResult>(url, { method: 'POST', body: form });
}

async function uploadProfile(file: File): Promise<CandidateProfile> {
  const form = new FormData();
  form.append('file', file);
  return request<CandidateProfile>('/api/profile/import-cv', { method: 'POST', body: form });
}
