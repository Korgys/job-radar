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
            ["Strasbourg"],
            "hybride",
            60000,
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
            "Lead technique finance avec contexte détaillé, responsabilités, environnement produit et collaboration métier.",
            "https://example.local",
            DateTime.UtcNow,
            null);

        var score = service.CalculateJobScore(profile, job);

        Assert.Equal(100, score.GlobalScore);
        Assert.Equal(35, score.StackScore);
        Assert.Equal(25, score.SeniorityScore);
        Assert.Equal(15, score.RoleScore);
        Assert.Equal(10, score.DomainScore);
        Assert.NotEmpty(score.PositiveReasons);
        Assert.Empty(score.MissingSkills);
    }

    [Fact]
    public void CalculateJobScore_GivesPartialRoleForAdjacentBackendAndFullstackJob()
    {
        var service = new ScoringService(null!, null!);
        var profile = new CandidateProfileDto(
            1,
            "CV",
            ["C#", ".NET"],
            ["développeur backend"],
            ["banque"],
            "confirmé",
            "Résumé",
            ["Strasbourg"],
            "hybride",
            60000,
            DateTime.UtcNow,
            DateTime.UtcNow);

        var job = new JobDto(
            1,
            1,
            "Banque Test",
            "Banque",
            "Développeur fullstack .NET",
            "Strasbourg",
            "Hybride",
            "CDI",
            null,
            null,
            null,
            "fullstack",
            ["C#", ".NET"],
            "Développement web full stack",
            "https://example.local",
            DateTime.UtcNow,
            null);

        var score = service.CalculateJobScore(profile, job);

        Assert.Equal(7, score.RoleScore);
        Assert.Equal(25, score.SeniorityScore);
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

        Assert.Equal(100, score.GlobalScore);
        Assert.Equal(60, score.StackScore);
        Assert.Equal(25, score.DomainScore);
        Assert.Equal(15, score.StrategicScore);
        Assert.Equal(0, score.RoleScore);
        Assert.Equal(0, score.LocationScore);
        Assert.Equal(0, score.SalaryScore);
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

        Assert.Equal(100, score.GlobalScore);
        Assert.Equal(60, score.StackScore);
        Assert.Equal(25, score.DomainScore);
        Assert.Equal(12, score.StrategicScore);
        Assert.Equal(0, score.RoleScore);
        Assert.Equal(0, score.SalaryScore);
    }

    [Fact]
    public void CalculateCompanyScore_UsesBestLinkedJobScoreAsGlobalFloor()
    {
        var service = new ScoringService(null!, null!);
        var profile = new CandidateProfileDto(
            1,
            "CV",
            ["COBOL"],
            ["développeur backend"],
            ["banque"],
            "senior",
            "Résumé",
            ["Strasbourg"],
            "hybride",
            60000,
            DateTime.UtcNow,
            DateTime.UtcNow);
        var company = CreateCompany(
            domain: "Industrie",
            stack: ["SAP"],
            careerUrl: null,
            linkedinUrl: null,
            website: null,
            notes: null);
        var job = CreateJob("Backend Java", "backend", ["Java"]) with
        {
            Score = new ScoreDto(85, 30, 20, 10, 25, 0, 0, 0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
        };

        var score = service.CalculateCompanyScore(profile, company, [job]);

        Assert.Equal(85, score.GlobalScore);
        Assert.Contains(score.PositiveReasons, reason => reason.Contains("meilleure offre", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CalculateJobScore_ScoresLocationFromCandidatePreferences()
    {
        var service = new ScoringService(null!, null!);
        var score = service.CalculateJobScore(CreateProfile(), CreateJob("Backend .NET", "backend", ["C#", ".NET"]) with
        {
            Location = "Strasbourg centre",
            RemotePolicy = "Hybride"
        });

        Assert.Equal(5, score.LocationScore);
    }

    [Fact]
    public void CalculateJobScore_ScoresSalaryFromCandidateTarget()
    {
        var service = new ScoringService(null!, null!);
        var score = service.CalculateJobScore(CreateProfile(), CreateJob("Backend .NET", "backend", ["C#", ".NET"]) with
        {
            SalaryMin = 55000,
            SalaryMax = 65000
        });

        Assert.Equal(5, score.SalaryScore);
    }

    [Fact]
    public void CalculateJobScore_ScoresStrategicSignalsFromActionableOffer()
    {
        var service = new ScoringService(null!, null!);
        var score = service.CalculateJobScore(CreateProfile(), CreateJob("Backend .NET", "backend", ["C#", ".NET"]) with
        {
            Description = "Offre détaillée avec mission produit, contexte entreprise, responsabilités, stack et modalités de candidature.",
            Url = "https://example.local/jobs/1",
            PublicationDate = DateTime.UtcNow
        });

        Assert.Equal(5, score.StrategicScore);
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
            ["Strasbourg"],
            "hybride",
            60000,
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
