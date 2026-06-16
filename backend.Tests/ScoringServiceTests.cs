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
}
