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

    [Fact]
    public async Task ImportCompaniesAsync_MergesIncompleteCompanyCreatedFromJob()
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(directory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();
            var imports = new CsvImportService(database);
            var queries = new RadarQueryService(database, paths);

            const string jobsCsv = """
                company_name,title,location,remote_policy,contract,salary_min,salary_max,seniority,job_type,stack,description,url,publication_date
                AGILIA Technology,Développeur C#,Strasbourg,hybrid,CDI,,,confirmed,backend,C#;.NET,Description,https://example.test/agilia,
                """;
            const string companiesCsv = """
                name,domain,secondary_domains,city,address,latitude,longitude,website,career_url,linkedin_url,glassdoor_url,known_stack,notes,logo_url
                AGILIA Technology,ESN,Conseil IT,Entzheim,"5 rue Icare, 67960 Entzheim",48.5447,7.6525,https://agilia-technology.com,,https://fr.linkedin.com/company/agilia-technology,,C#;.NET,Note,
                """;

            await using var jobsStream = new MemoryStream(Encoding.UTF8.GetBytes(jobsCsv));
            await imports.ImportJobsAsync(jobsStream);
            await using var companiesStream = new MemoryStream(Encoding.UTF8.GetBytes(companiesCsv));
            await imports.ImportCompaniesAsync(companiesStream);

            var companies = await queries.GetCompaniesAsync();
            var jobs = await queries.GetJobsAsync();

            Assert.Single(companies);
            Assert.Equal("AGILIA Technology", companies[0].Name);
            Assert.Equal("Entzheim", companies[0].City);
            Assert.False(companies[0].Incomplete);
            Assert.Single(jobs);
            Assert.Equal(companies[0].Id, jobs[0].CompanyId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("Caisse d'Épargne Grand Est Europe", "Caisse d'Epargne Grand Est Europe", "Strasbourg")]
    [InlineData("Capgemini", "Capgemini Est", "Schiltigheim")]
    [InlineData("Davidson Est", "Davidson Digital Est", "Strasbourg")]
    [InlineData("Hager", "Hager Group", "Obernai")]
    [InlineData("Objectware Strasbourg", "Objectware", "Strasbourg")]
    [InlineData("SFEIR Est", "SFEIR", "Schiltigheim")]
    public async Task ImportJobsAsync_AttachesAliasToExistingCompany(string companyName, string jobCompanyName, string city)
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(directory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();
            var imports = new CsvImportService(database);
            var queries = new RadarQueryService(database, paths);

            var companiesCsv = $$"""
                name,domain,secondary_domains,city,address,latitude,longitude,website,career_url,linkedin_url,glassdoor_url,known_stack,notes,logo_url
                {{companyName}},ESN,Software,{{city}},Adresse,48.5737,7.7716,https://example.test,,,,C#;.NET,Note,
                """;
            var jobsCsv = $$"""
                company_name,title,location,remote_policy,contract,salary_min,salary_max,seniority,job_type,stack,description,url,publication_date
                {{jobCompanyName}},Analyste Développeur C#,{{city}},unknown,CDI,,,confirmed,backend,C#;.NET,Description,https://example.test/{{Guid.NewGuid():N}},
                """;

            await using var companiesStream = new MemoryStream(Encoding.UTF8.GetBytes(companiesCsv));
            await imports.ImportCompaniesAsync(companiesStream);
            await using var jobsStream = new MemoryStream(Encoding.UTF8.GetBytes(jobsCsv));
            await imports.ImportJobsAsync(jobsStream);

            var companies = await queries.GetCompaniesAsync();
            var jobs = await queries.GetJobsAsync();

            Assert.Single(companies);
            Assert.Equal(companyName, companies[0].Name);
            Assert.Single(jobs);
            Assert.Equal(companies[0].Id, jobs[0].CompanyId);
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
