namespace JobRadarLocal.Services;

public sealed record ParsedCv(
    string RawText,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Domains,
    string Seniority,
    string ExperiencesSummary);
