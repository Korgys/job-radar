namespace JobRadarLocal.Models;

public sealed class JobOffer
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Location { get; set; }
    public string? RemotePolicy { get; set; }
    public string? Contract { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? Seniority { get; set; }
    public string? JobType { get; set; }
    public string Stack { get; set; } = "";
    public string? Description { get; set; }
    public string? Url { get; set; }
    public DateTime? PublicationDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
