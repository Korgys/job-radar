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
    public void Initialize_DoesNotCreateReportFilesTable()
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(directory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();

            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'report_files';";

            Assert.Equal(0L, command.ExecuteScalar());
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
