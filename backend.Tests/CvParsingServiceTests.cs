using System.Text;
using JobRadarLocal.Data;
using JobRadarLocal.Dtos;
using JobRadarLocal.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace JobRadarLocal.Tests;

public sealed class CvParsingServiceTests
{
    [Fact]
    public void ParseText_DetectsSkillsRolesDomainsAndSeniority()
    {
        var parser = new CvParsingService(null!);
        var parsed = parser.ParseText("""
            Tech lead C# .NET ASP.NET Core SQL Server Azure DevOps Git CI/CD Docker React TypeScript.
            10 ans d'expérience en banque, industrie et SaaS.
            """);

        Assert.Contains("C#", parsed.Skills);
        Assert.Contains(".NET", parsed.Skills);
        Assert.Contains("SQL Server", parsed.Skills);
        Assert.Contains("tech lead", parsed.Roles);
        Assert.Contains("banque", parsed.Domains);
        Assert.Equal("lead", parsed.Seniority);
    }

    [Fact]
    public async Task UpdateLatestProfileAsync_UpdatesEditableProfileFields()
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(directory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();
            var parser = new CvParsingService(database);

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Développeur junior C# en banque."));
            await parser.ImportAsync("cv.txt", stream);

            var updated = await parser.UpdateLatestProfileAsync(new UpdateCandidateProfileRequest(
                ["C#", "React", "C#"],
                ["architecte logiciel", "tech lead", "architecte logiciel"],
                ["finance"],
                "senior",
                ["Lyon"],
                "hybride",
                70000));

            Assert.Equal(["C#", "React"], updated.DetectedSkills);
            Assert.Equal(["architecte logiciel", "tech lead"], updated.DetectedRoles);
            Assert.Equal(["finance"], updated.DetectedDomains);
            Assert.Equal("senior", updated.DetectedSeniority);
            Assert.NotNull(updated.ExperiencesSummary);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateLatestProfileAsync_RejectsNegativeTargetSalary()
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(directory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();
            var parser = new CvParsingService(database);

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Développeur junior C# en banque."));
            await parser.ImportAsync("cv.txt", stream);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => parser.UpdateLatestProfileAsync(new UpdateCandidateProfileRequest(
                ["C#"],
                ["développeur backend"],
                ["banque"],
                "junior",
                [],
                null,
                -1)));

            Assert.Contains("salaire cible", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "job-radar-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
