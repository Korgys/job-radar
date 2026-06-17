export type Score = {
  globalScore: number;
  stackScore: number;
  roleScore: number;
  domainScore: number;
  seniorityScore: number;
  locationScore: number;
  salaryScore: number;
  strategicScore: number;
  positiveReasons: string[];
  negativeReasons: string[];
  missingSkills: string[];
};

export type Company = {
  id: number;
  name: string;
  domain: string;
  secondaryDomains: string[];
  city: string;
  address?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  website?: string | null;
  careerUrl?: string | null;
  linkedinUrl?: string | null;
  glassdoorUrl?: string | null;
  knownStack: string[];
  notes?: string | null;
  logoUrl?: string | null;
  incomplete: boolean;
  jobCount: number;
  score?: Score | null;
};

export type Job = {
  id: number;
  companyId: number;
  companyName: string;
  companyDomain: string;
  title: string;
  location?: string | null;
  remotePolicy?: string | null;
  contract?: string | null;
  salaryMin?: number | null;
  salaryMax?: number | null;
  seniority?: string | null;
  jobType?: string | null;
  stack: string[];
  description?: string | null;
  url?: string | null;
  publicationDate?: string | null;
  score?: Score | null;
};

export type CandidateProfile = {
  id: number;
  rawText: string;
  detectedSkills: string[];
  detectedRoles: string[];
  detectedDomains: string[];
  detectedSeniority: string;
  experiencesSummary?: string | null;
  preferredLocations: string[];
  remotePreference?: string | null;
  targetSalary?: number | null;
  createdAt: string;
  updatedAt: string;
};

export type UpdateProfileRequest = {
  detectedSkills: string[];
  detectedRoles: string[];
  detectedDomains: string[];
  detectedSeniority: string;
  preferredLocations: string[];
  remotePreference?: string | null;
  targetSalary?: number | null;
};

export type ImportResult = {
  imported: number;
  updated: number;
  skipped: number;
  errors: { row: number; message: string }[];
};

export type DashboardStats = {
  companyCount: number;
  jobCount: number;
  lastProfileImport?: string | null;
  compatibleCompanyCount: number;
  compatibleJobCount: number;
};

export type ReportFile = {
  fileName: string;
  createdAt: string;
};

export type RecalculateResult = {
  companyScores: number;
  jobScores: number;
};
