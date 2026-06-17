namespace JobRadarLocal.Models;

public sealed class CandidateProfile
{
    public int Id { get; set; }
    public string RawText { get; set; } = "";
    public string DetectedSkills { get; set; } = "";
    public string DetectedRoles { get; set; } = "";
    public string DetectedDomains { get; set; } = "";
    public string DetectedSeniority { get; set; } = "";
    public string? ExperiencesSummary { get; set; }
    public string PreferredLocations { get; set; } = "";
    public string? RemotePreference { get; set; }
    public decimal? TargetSalary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
