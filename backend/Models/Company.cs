namespace JobRadarLocal.Models;

public sealed class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Domain { get; set; } = "";
    public string SecondaryDomains { get; set; } = "";
    public string City { get; set; } = "";
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Website { get; set; }
    public string? CareerUrl { get; set; }
    public string? LinkedinUrl { get; set; }
    public string? GlassdoorUrl { get; set; }
    public string KnownStack { get; set; } = "";
    public string? Notes { get; set; }
    public string? LogoUrl { get; set; }
    public bool Incomplete { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
