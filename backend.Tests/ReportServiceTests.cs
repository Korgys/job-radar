using JobRadarLocal.Data;
using JobRadarLocal.Dtos;
using JobRadarLocal.Services;
using Xunit;

namespace JobRadarLocal.Tests;

public sealed class ReportServiceTests
{
    [Fact]
    public void BuildMarkdown_IncludesExpectedSectionsAndScores()
    {
        var service = new ReportService(null!, null!, null!);
        var score = new ScoreDto(
            82,
            30,
            20,
            20,
            10,
            7,
            0,
            5,
            ["Compatibilité stack : C#, .NET."],
            ["Salaire non renseigné."],
            ["Kubernetes"]);

        var company = new CompanyDto(
            1,
            "NovaCare Systems",
            "Industrie",
            ["Energie"],
            "Benfeld",
            null,
            48.37,
            7.59,
            "https://example.local",
            null,
            null,
            null,
            ["C#", ".NET"],
            "Notes",
            null,
            false,
            1,
            score);

        var job = new JobDto(
            1,
            1,
            "NovaCare Systems",
            "Industrie",
            "Développeur C#",
            "Benfeld",
            "Hybride",
            "CDI",
            null,
            null,
            "senior",
            "développeur backend",
            ["C#", ".NET"],
            null,
            "https://example.local/job",
            null,
            score);

        var markdown = service.BuildMarkdown(DateTime.Parse("2026-06-16T10:00:00"), [company], [job], null);

        Assert.Contains("# Rapport Job Radar Local", markdown);
        Assert.Contains("## Top entreprises", markdown);
        Assert.Contains("NovaCare Systems", markdown);
        Assert.Contains("82/100", markdown);
        Assert.Contains("## Recommandations", markdown);
    }

    [Fact]
    public async Task GenerateAsync_CreatesDistinctFiles_WhenReportsAreGeneratedCloselyTogether()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"job-radar-tests-{Guid.NewGuid():N}");

        try
        {
            var paths = new AppPaths(dataDirectory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();
            var queries = new RadarQueryService(database, paths);
            var service = new ReportService(paths, database, queries);

            var firstReport = await service.GenerateAsync();
            var secondReport = await service.GenerateAsync();

            Assert.NotEqual(firstReport.FileName, secondReport.FileName);
            Assert.Matches(@"^job-radar-report-\d{8}-\d{6}-[a-f0-9]{6}\.md$", firstReport.FileName);
            Assert.Matches(@"^job-radar-report-\d{8}-\d{6}-[a-f0-9]{6}\.md$", secondReport.FileName);
            Assert.True(File.Exists(Path.Combine(paths.ReportsDirectory, firstReport.FileName)));
            Assert.True(File.Exists(Path.Combine(paths.ReportsDirectory, secondReport.FileName)));
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }
}
