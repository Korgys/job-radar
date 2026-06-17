namespace JobRadarLocal.Dtos;

public sealed record ScoreDto(
    int GlobalScore,
    int StackScore,
    int RoleScore,
    int DomainScore,
    int SeniorityScore,
    int LocationScore,
    int SalaryScore,
    int StrategicScore,
    IReadOnlyList<string> PositiveReasons,
    IReadOnlyList<string> NegativeReasons,
    IReadOnlyList<string> MissingSkills);

public sealed record CompanyDto(
    int Id,
    string Name,
    string Domain,
    IReadOnlyList<string> SecondaryDomains,
    string City,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? Website,
    string? CareerUrl,
    string? LinkedinUrl,
    string? GlassdoorUrl,
    IReadOnlyList<string> KnownStack,
    string? Notes,
    string? LogoUrl,
    bool Incomplete,
    int JobCount,
    ScoreDto? Score);

public sealed record JobDto(
    int Id,
    int CompanyId,
    string CompanyName,
    string CompanyDomain,
    string Title,
    string? Location,
    string? RemotePolicy,
    string? Contract,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? Seniority,
    string? JobType,
    IReadOnlyList<string> Stack,
    string? Description,
    string? Url,
    DateTime? PublicationDate,
    ScoreDto? Score);

public sealed record CandidateProfileDto(
    int Id,
    string RawText,
    IReadOnlyList<string> DetectedSkills,
    IReadOnlyList<string> DetectedRoles,
    IReadOnlyList<string> DetectedDomains,
    string DetectedSeniority,
    string? ExperiencesSummary,
    IReadOnlyList<string> PreferredLocations,
    string? RemotePreference,
    decimal? TargetSalary,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record UpdateCandidateProfileRequest(
    IReadOnlyList<string>? DetectedSkills,
    IReadOnlyList<string>? DetectedRoles,
    IReadOnlyList<string>? DetectedDomains,
    string? DetectedSeniority,
    IReadOnlyList<string>? PreferredLocations,
    string? RemotePreference,
    decimal? TargetSalary);

public sealed record ReportFileDto(string FileName, DateTime CreatedAt);

public sealed record DashboardStatsDto(
    int CompanyCount,
    int JobCount,
    DateTime? LastProfileImport,
    int CompatibleCompanyCount,
    int CompatibleJobCount);

public sealed record RecalculateResultDto(int CompanyScores, int JobScores);
