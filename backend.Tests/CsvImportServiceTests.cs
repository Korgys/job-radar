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
                name,domain,secondary_domains,city,address,latitude,longitude,website,career_url,linkedin_url,known_stack,notes
                Test Corp,SaaS,Finance,Strasbourg,,48.58,7.75,,,,C#;.NET,Note
                ,SaaS,,Strasbourg,,48.58,7.75,,,,C#,
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
                name,domain,secondary_domains,city,address,latitude,longitude,website,career_url,linkedin_url,known_stack,notes
                AGILIA Technology,ESN,Conseil IT,Entzheim,"5 rue Icare, 67960 Entzheim",48.5447,7.6525,https://agilia-technology.com,,https://fr.linkedin.com/company/agilia-technology,C#;.NET,Note
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

    [Fact]
    public async Task ImportCompaniesAsync_ReportsOutOfRangeCoordinates()
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(directory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();
            var imports = new CsvImportService(database);

            const string csv = """
                name,domain,secondary_domains,city,address,latitude,longitude,website,career_url,linkedin_url,known_stack,notes
                Test Corp,SaaS,Finance,Strasbourg,,91,7.75,https://example.test,,,C#;.NET,Note
                """;

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            var result = await imports.ImportCompaniesAsync(stream);

            Assert.Equal(0, result.Imported);
            Assert.Equal(1, result.Skipped);
            var error = Assert.Single(result.Errors);
            Assert.Equal(2, error.Row);
            Assert.Contains("latitude", error.Message);
            Assert.Contains("-90 et 90", error.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImportCompaniesAsync_ReportsInvalidUrl()
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(directory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();
            var imports = new CsvImportService(database);

            const string csv = """
                name,domain,secondary_domains,city,address,latitude,longitude,website,career_url,linkedin_url,known_stack,notes
                Test Corp,SaaS,Finance,Strasbourg,,48.58,7.75,not-a-url,,,C#;.NET,Note
                """;

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            var result = await imports.ImportCompaniesAsync(stream);

            Assert.Equal(0, result.Imported);
            Assert.Equal(1, result.Skipped);
            var error = Assert.Single(result.Errors);
            Assert.Equal(2, error.Row);
            Assert.Contains("website", error.Message);
            Assert.Contains("URL", error.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImportJobsAsync_ImportsInitialJob()
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
                Test Corp,Développeur C#,Strasbourg,hybrid,CDI,45000,55000,confirmed,backend,C#;.NET,Description,https://example.test/jobs/1,2026-01-15
                """;

            await using var jobsStream = new MemoryStream(Encoding.UTF8.GetBytes(jobsCsv));
            var result = await imports.ImportJobsAsync(jobsStream);
            var jobs = await queries.GetJobsAsync();

            Assert.Equal(1, result.Imported);
            Assert.Equal(0, result.Updated);
            Assert.Equal(0, result.Skipped);
            Assert.Single(jobs);
            Assert.Equal("Développeur C#", jobs[0].Title);
            Assert.Equal("Strasbourg", jobs[0].Location);
            Assert.Equal(45000, jobs[0].SalaryMin);
            Assert.Equal(55000, jobs[0].SalaryMax);
            Assert.Equal(new[] { "C#", ".NET" }, jobs[0].Stack);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImportJobsAsync_ReportsInvertedSalaries()
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(directory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();
            var imports = new CsvImportService(database);

            const string jobsCsv = """
                company_name,title,location,remote_policy,contract,salary_min,salary_max,seniority,job_type,stack,description,url,publication_date
                Test Corp,Développeur C#,Strasbourg,hybrid,CDI,65000,45000,confirmed,backend,C#;.NET,Description,https://example.test/jobs/1,2026-01-15
                """;

            await using var jobsStream = new MemoryStream(Encoding.UTF8.GetBytes(jobsCsv));
            var result = await imports.ImportJobsAsync(jobsStream);

            Assert.Equal(0, result.Imported);
            Assert.Equal(1, result.Skipped);
            var error = Assert.Single(result.Errors);
            Assert.Equal(2, error.Row);
            Assert.Contains("salary_min", error.Message);
            Assert.Contains("salary_max", error.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImportJobsAsync_ImportingSameJobTwicePersistsSingleRow()
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
                Test Corp,Développeur C#,Strasbourg,hybrid,CDI,45000,55000,confirmed,backend,C#;.NET,Description,https://example.test/jobs/duplicate,2026-01-15
                """;

            await using var firstJobsStream = new MemoryStream(Encoding.UTF8.GetBytes(jobsCsv));
            var firstResult = await imports.ImportJobsAsync(firstJobsStream);
            await using var secondJobsStream = new MemoryStream(Encoding.UTF8.GetBytes(jobsCsv));
            var secondResult = await imports.ImportJobsAsync(secondJobsStream);
            var jobs = await queries.GetJobsAsync();

            Assert.Equal(1, firstResult.Imported);
            Assert.Equal(0, firstResult.Updated);
            Assert.Equal(0, secondResult.Imported);
            Assert.Equal(1, secondResult.Updated);
            Assert.Equal(0, secondResult.Skipped);
            Assert.Single(jobs);
            Assert.Equal("https://example.test/jobs/duplicate", jobs[0].Url);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImportJobsAsync_ReimportSameUrlUpdatesStackSalaryAndReportsUpdated()
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(directory);
            var database = new Database(paths);
            new DatabaseInitializer(database, paths).Initialize();
            var imports = new CsvImportService(database);
            var queries = new RadarQueryService(database, paths);

            const string initialJobsCsv = """
                company_name,title,location,remote_policy,contract,salary_min,salary_max,seniority,job_type,stack,description,url,publication_date
                Test Corp,Développeur C#,Strasbourg,hybrid,CDI,45000,55000,confirmed,backend,C#;.NET,Description initiale,https://example.test/jobs/1,2026-01-15
                """;
            const string updatedJobsCsv = """
                company_name,title,location,remote_policy,contract,salary_min,salary_max,seniority,job_type,stack,description,url,publication_date
                Test Corp,Développeur C#,Remote,remote,CDI,50000,65000,senior,fullstack,C#;.NET;Azure,Description mise à jour,https://example.test/jobs/1,2026-02-01
                """;

            await using var initialJobsStream = new MemoryStream(Encoding.UTF8.GetBytes(initialJobsCsv));
            var initialResult = await imports.ImportJobsAsync(initialJobsStream);
            await using var updatedJobsStream = new MemoryStream(Encoding.UTF8.GetBytes(updatedJobsCsv));
            var updatedResult = await imports.ImportJobsAsync(updatedJobsStream);
            var jobs = await queries.GetJobsAsync();

            Assert.Equal(1, initialResult.Imported);
            Assert.Equal(0, initialResult.Updated);
            Assert.Equal(0, updatedResult.Imported);
            Assert.Equal(1, updatedResult.Updated);
            Assert.Equal(0, updatedResult.Skipped);
            Assert.Single(jobs);
            Assert.Equal("Remote", jobs[0].Location);
            Assert.Equal("remote", jobs[0].RemotePolicy);
            Assert.Equal(50000, jobs[0].SalaryMin);
            Assert.Equal(65000, jobs[0].SalaryMax);
            Assert.Equal("senior", jobs[0].Seniority);
            Assert.Equal("fullstack", jobs[0].JobType);
            Assert.Equal(new[] { "C#", ".NET", "Azure" }, jobs[0].Stack);
            Assert.Equal("Description mise à jour", jobs[0].Description);
            Assert.Equal(new DateTime(2026, 2, 1), jobs[0].PublicationDate);
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
    [InlineData("Acme", "Acme Digital Est", "Strasbourg")]
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
                name,domain,secondary_domains,city,address,latitude,longitude,website,career_url,linkedin_url,known_stack,notes
                {{companyName}},ESN,Software,{{city}},Adresse,48.5737,7.7716,https://example.test,,,C#;.NET,Note
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
