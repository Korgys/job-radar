namespace JobRadarLocal.Models;

public sealed class CompatibilityScore
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int? JobId { get; set; }
    public int GlobalScore { get; set; }
    public int StackScore { get; set; }
    public int RoleScore { get; set; }
    public int DomainScore { get; set; }
    public int SeniorityScore { get; set; }
    public int LocationScore { get; set; }
    public int SalaryScore { get; set; }
    public int StrategicScore { get; set; }
    public string PositiveReasons { get; set; } = "";
    public string NegativeReasons { get; set; } = "";
    public string MissingSkills { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}
