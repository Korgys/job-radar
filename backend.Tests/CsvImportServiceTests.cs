using System.Text;
using JobRadarLocal.Data;
using JobRadarLocal.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace JobRadarLocal.Tests;

public sealed class CsvImportServiceTests
{
    [Fact]
    public async Task ImportCompaniesAsync_ImportsValidRowsAndReportsInvalidRows()
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(directory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();
            var imports = new CsvImportService(database);
            var queries = new RadarQueryService(database, paths);

            const string csv = """
                name,domain,secondary_domains,city,address,latitude,longitude,website,career_url,linkedin_url,glassdoor_url,known_stack,notes,logo_url
                Test Corp,SaaS,Finance,Strasbourg,,48.58,7.75,,,,,C#;.NET,Note,
                ,SaaS,,Strasbourg,,48.58,7.75,,,,,C#,,
                """;

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            var result = await imports.ImportCompaniesAsync(stream);
            var companies = await queries.GetCompaniesAsync();

            Assert.Equal(1, result.Imported);
            Assert.Equal(1, result.Skipped);
            Assert.Single(result.Errors);
            Assert.Single(companies);
            Assert.Equal("Test Corp", companies[0].Name);
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
