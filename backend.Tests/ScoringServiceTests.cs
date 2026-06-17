using JobRadarLocal.Dtos;
using JobRadarLocal.Services;
using Xunit;

namespace JobRadarLocal.Tests;

public sealed class ScoringServiceTests
{
    [Fact]
    public void CalculateJobScore_ReturnsExplainableHighScoreForAlignedJob()
    {
        var service = new ScoringService(null!, null!);
        var profile = new CandidateProfileDto(
            1,
            "CV",
            ["C#", ".NET", "SQL Server", "Azure DevOps", "React"],
            ["tech lead", "développeur backend"],
            ["banque", "industrie"],
            "lead",
            "Résumé",
            DateTime.UtcNow,
            DateTime.UtcNow);

        var job = new JobDto(
            1,
            1,
            "Banque Test",
            "Banque",
            "Tech Lead .NET",
            "Strasbourg",
            "Hybride",
            "CDI",
            55000,
            70000,
            "lead",
            "tech lead",
            ["C#", ".NET", "SQL Server", "React"],
            "Lead technique finance",
            "https://example.local",
            DateTime.UtcNow,
            null);

        var score = service.CalculateJobScore(profile, job);

        Assert.True(score.GlobalScore >= 80);
        Assert.NotEmpty(score.PositiveReasons);
        Assert.Empty(score.MissingSkills);
    }

    [Fact]
    public void CalculateCompanyScore_UsesPrioritizationRubricForActiveCompany()
    {
        var service = new ScoringService(null!, null!);
        var profile = CreateProfile();
        var company = CreateCompany(
            domain: "Banque",
            stack: ["C#", ".NET", "SQL Server", "Angular", "React"],
            careerUrl: "https://example.local/jobs",
            linkedinUrl: "https://linkedin.local/company",
            website: "https://example.local",
            notes: "Equipe IT locale avec offres régulières.");
        var jobs = new[]
        {
            CreateJob(".NET backend", "backend", ["C#", ".NET", "SQL Server"]),
            CreateJob("Développeur fullstack", "fullstack", ["C#", ".NET", "Angular"]),
            CreateJob("Tech lead .NET", "tech_lead", ["C#", ".NET", "React"])
        };

        var score = service.CalculateCompanyScore(profile, company, jobs);

        Assert.True(score.GlobalScore >= 85);
        Assert.Equal(30, score.StackScore);
        Assert.Equal(21, score.StrategicScore);
        Assert.Equal(15, score.DomainScore);
        Assert.Equal(10, score.RoleScore);
        Assert.Equal(10, score.LocationScore);
        Assert.Equal(10, score.SalaryScore);
    }

    [Fact]
    public void CalculateCompanyScore_CanPrioritizeActionableCompanyWithoutJobs()
    {
        var service = new ScoringService(null!, null!);
        var profile = CreateProfile();
        var company = CreateCompany(
            domain: "Banque",
            stack: ["C#", ".NET", "SQL Server", "Angular", "React"],
            careerUrl: "https://example.local/jobs",
            linkedinUrl: "https://linkedin.local/company",
            website: "https://example.local",
            notes: "Cible bancaire locale à suivre.");

        var score = service.CalculateCompanyScore(profile, company, Array.Empty<JobDto>());

        Assert.True(score.GlobalScore >= 60);
        Assert.Equal(0, score.StrategicScore);
        Assert.Equal(0, score.RoleScore);
        Assert.Equal(10, score.SalaryScore);
        Assert.Contains(score.NegativeReasons, reason => reason.Contains("Aucune offre", StringComparison.OrdinalIgnoreCase));
    }

    private static CandidateProfileDto CreateProfile()
    {
        return new CandidateProfileDto(
            1,
            "CV",
            ["C#", ".NET", "SQL Server", "Angular", "React"],
            ["développeur fullstack"],
            ["banque"],
            "confirmé",
            "Résumé",
            DateTime.UtcNow,
            DateTime.UtcNow);
    }

    private static CompanyDto CreateCompany(
        string domain,
        IReadOnlyList<string> stack,
        string? careerUrl,
        string? linkedinUrl,
        string? website,
        string? notes)
    {
        return new CompanyDto(
            1,
            "Entreprise Test",
            domain,
            Array.Empty<string>(),
            "Strasbourg",
            "1 rue Test",
            48.58,
            7.75,
            website,
            careerUrl,
            linkedinUrl,
            null,
            stack,
            notes,
            null,
            false,
            0,
            null);
    }

    private static JobDto CreateJob(string title, string jobType, IReadOnlyList<string> stack)
    {
        return new JobDto(
            1,
            1,
            "Entreprise Test",
            "Banque",
            title,
            "Strasbourg",
            "hybrid",
            "CDI",
            null,
            null,
            "confirmed",
            jobType,
            stack,
            title,
            "https://example.local/job",
            DateTime.UtcNow,
            null);
    }
}
