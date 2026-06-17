using JobRadarLocal.Data;
using JobRadarLocal.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace JobRadarLocal.Tests;

public sealed class ReportGenerationTests
{
    [Fact]
    public async Task RecalculateAsync_WithoutCandidateProfile_AsksToImportCvBeforeScoredReport()
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(directory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();
            var queries = new RadarQueryService(database, paths);
            var scoring = new ScoringService(database, queries);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => scoring.RecalculateAsync());

            Assert.Equal("Importez un CV avant de recalculer les scores.", exception.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BuildMarkdown_WhenUnscored_AddsClearWarning()
    {
        var service = new ReportService(null!, null!, null!);

        var markdown = service.BuildMarkdown(
            DateTime.Parse("2026-06-16T10:00:00"),
            [],
            [],
            null,
            scoringIsCurrent: false);

        Assert.Contains("Rapport non scoré", markdown);
        Assert.Contains("ne contient pas de scoring à jour", markdown);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "job-radar-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
