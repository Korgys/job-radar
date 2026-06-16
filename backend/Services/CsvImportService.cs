using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using JobRadarLocal.Data;
using JobRadarLocal.Dtos;
using Microsoft.Data.Sqlite;

namespace JobRadarLocal.Services;

public sealed class CsvImportService
{
    private readonly Database _database;

    public CsvImportService(Database database)
    {
        _database = database;
    }

    public async Task<ImportResultDto> ImportCompaniesAsync(Stream stream)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var reader = CreateCsvReader(stream);

        if (!await ReadHeaderAsync(reader))
        {
            return new ImportResultDto(0, 0, 0, new[] { new ImportErrorDto(1, "Le fichier CSV ne contient pas d'en-tête.") });
        }

        var imported = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<ImportErrorDto>();

        while (await reader.ReadAsync())
        {
            var row = reader.Context.Parser?.Row ?? 0;
            try
            {
                var name = Field(reader, "name");
                var domain = Field(reader, "domain");
                var city = Field(reader, "city");
                var latitudeText = Field(reader, "latitude");
                var longitudeText = Field(reader, "longitude");

                var validationError = ValidateRequired(
                    ("name", name),
                    ("domain", domain),
                    ("city", city),
                    ("latitude", latitudeText),
                    ("longitude", longitudeText));

                if (validationError is not null)
                {
                    skipped++;
                    errors.Add(new ImportErrorDto(row, validationError));
                    continue;
                }

                if (!TryParseDouble(latitudeText, out var latitude) || !TryParseDouble(longitudeText, out var longitude))
                {
                    skipped++;
                    errors.Add(new ImportErrorDto(row, "Les colonnes latitude et longitude doivent être numériques."));
                    continue;
                }

                var company = new CompanyImportRow(
                    name,
                    domain,
                    RadarText.CleanList(Field(reader, "secondary_domains")),
                    city,
                    EmptyToNull(Field(reader, "address")),
                    latitude,
                    longitude,
                    EmptyToNull(Field(reader, "website")),
                    EmptyToNull(Field(reader, "career_url")),
                    EmptyToNull(Field(reader, "linkedin_url")),
                    EmptyToNull(Field(reader, "glassdoor_url")),
                    RadarText.CleanList(Field(reader, "known_stack")),
                    EmptyToNull(Field(reader, "notes")),
                    EmptyToNull(Field(reader, "logo_url")));

                if (await UpsertCompanyAsync(connection, transaction, company))
                {
                    updated++;
                }
                else
                {
                    imported++;
                }
            }
            catch (Exception exception)
            {
                skipped++;
                errors.Add(new ImportErrorDto(row, $"Ligne ignorée : {exception.Message}"));
            }
        }

        transaction.Commit();
        return new ImportResultDto(imported, updated, skipped, errors);
    }

    public async Task<ImportResultDto> ImportJobsAsync(Stream stream)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var reader = CreateCsvReader(stream);

        if (!await ReadHeaderAsync(reader))
        {
            return new ImportResultDto(0, 0, 0, new[] { new ImportErrorDto(1, "Le fichier CSV ne contient pas d'en-tête.") });
        }

        var imported = 0;
        var skipped = 0;
        var errors = new List<ImportErrorDto>();

        while (await reader.ReadAsync())
        {
            var row = reader.Context.Parser?.Row ?? 0;
            try
            {
                var companyName = Field(reader, "company_name");
                var title = Field(reader, "title");
                var validationError = ValidateRequired(("company_name", companyName), ("title", title));

                if (validationError is not null)
                {
                    skipped++;
                    errors.Add(new ImportErrorDto(row, validationError));
                    continue;
                }

                var url = EmptyToNull(Field(reader, "url"));
                if (await JobExistsAsync(connection, transaction, companyName, title, url))
                {
                    skipped++;
                    continue;
                }

                var location = EmptyToNull(Field(reader, "location"));
                var companyId = await FindOrCreateCompanyAsync(connection, transaction, companyName, location);

                var job = new JobImportRow(
                    companyId,
                    companyName,
                    title,
                    location,
                    EmptyToNull(Field(reader, "remote_policy")),
                    EmptyToNull(Field(reader, "contract")),
                    ParseDecimal(Field(reader, "salary_min")),
                    ParseDecimal(Field(reader, "salary_max")),
                    EmptyToNull(Field(reader, "seniority")),
                    EmptyToNull(Field(reader, "job_type")),
                    RadarText.CleanList(Field(reader, "stack")),
                    EmptyToNull(Field(reader, "description")),
                    url,
                    ParseDate(Field(reader, "publication_date")));

                await InsertJobAsync(connection, transaction, job);
                imported++;
            }
            catch (Exception exception)
            {
                skipped++;
                errors.Add(new ImportErrorDto(row, $"Ligne ignorée : {exception.Message}"));
            }
        }

        transaction.Commit();
        return new ImportResultDto(imported, 0, skipped, errors);
    }

    private static CsvReader CreateCsvReader(Stream stream)
    {
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            HeaderValidated = null,
            MissingFieldFound = null,
            PrepareHeaderForMatch = args => args.Header.Trim()
        };

        return new CsvReader(new StreamReader(stream), configuration);
    }

    private static async Task<bool> ReadHeaderAsync(CsvReader reader)
    {
        if (!await reader.ReadAsync())
        {
            return false;
        }

        reader.ReadHeader();
        return reader.HeaderRecord is { Length: > 0 };
    }

    private static string Field(CsvReader reader, string name)
    {
        return (reader.GetField(name) ?? "").Trim();
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? ValidateRequired(params (string Name, string Value)[] values)
    {
        var missing = values.Where(value => string.IsNullOrWhiteSpace(value.Value)).Select(value => value.Name).ToArray();
        return missing.Length == 0 ? null : $"Colonnes obligatoires manquantes : {string.Join(", ", missing)}.";
    }

    private static bool TryParseDouble(string value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("fr-FR"), out result);
    }

    private static decimal? ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantResult))
        {
            return invariantResult;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("fr-FR"), out var frenchResult)
            ? frenchResult
            : null;
    }

    private static DateTime? ParseDate(string value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
            ? date.Date
            : null;
    }

    private static async Task<bool> UpsertCompanyAsync(SqliteConnection connection, SqliteTransaction transaction, CompanyImportRow company)
    {
        var existingId = await FindCompanyByNameAndCityAsync(connection, transaction, company.Name, company.City);
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        if (existingId is null)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO companies
                    (name, domain, secondary_domains, city, address, latitude, longitude, website, career_url, linkedin_url,
                     glassdoor_url, known_stack, notes, logo_url, incomplete, created_at, updated_at)
                VALUES
                    ($name, $domain, $secondaryDomains, $city, $address, $latitude, $longitude, $website, $careerUrl, $linkedinUrl,
                     $glassdoorUrl, $knownStack, $notes, $logoUrl, 0, $now, $now);
                """;
            AddCompanyParameters(insert, company, now);
            await insert.ExecuteNonQueryAsync();
            return false;
        }

        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE companies
            SET domain = $domain,
                secondary_domains = $secondaryDomains,
                address = $address,
                latitude = $latitude,
                longitude = $longitude,
                website = $website,
                career_url = $careerUrl,
                linkedin_url = $linkedinUrl,
                glassdoor_url = $glassdoorUrl,
                known_stack = $knownStack,
                notes = $notes,
                logo_url = $logoUrl,
                incomplete = 0,
                updated_at = $now
            WHERE id = $id;
            """;
        AddCompanyParameters(update, company, now);
        update.Parameters.AddWithValue("$id", existingId.Value);
        await update.ExecuteNonQueryAsync();
        return true;
    }

    private static async Task<int?> FindCompanyByNameAndCityAsync(SqliteConnection connection, SqliteTransaction transaction, string name, string city)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id
            FROM companies
            WHERE lower(name) = lower($name) AND lower(city) = lower($city)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$city", city);

        var value = await command.ExecuteScalarAsync();
        return value is null ? null : Convert.ToInt32(value);
    }

    private static async Task<int?> FindCompanyByNameAsync(SqliteConnection connection, SqliteTransaction transaction, string name)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id
            FROM companies
            WHERE lower(name) = lower($name)
            ORDER BY incomplete ASC, id ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$name", name);

        var value = await command.ExecuteScalarAsync();
        return value is null ? null : Convert.ToInt32(value);
    }

    private static async Task<int> FindOrCreateCompanyAsync(SqliteConnection connection, SqliteTransaction transaction, string companyName, string? location)
    {
        var existingId = await FindCompanyByNameAsync(connection, transaction, companyName);
        if (existingId is not null)
        {
            return existingId.Value;
        }

        var city = string.IsNullOrWhiteSpace(location) ? "Inconnue" : location.Trim();
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO companies
                (name, domain, secondary_domains, city, known_stack, incomplete, created_at, updated_at)
            VALUES
                ($name, 'Autre', '', $city, '', 1, $now, $now)
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$name", companyName);
        command.Parameters.AddWithValue("$city", city);
        command.Parameters.AddWithValue("$now", now);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> JobExistsAsync(SqliteConnection connection, SqliteTransaction transaction, string companyName, string title, string? url)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id
            FROM jobs
            WHERE lower(company_name) = lower($companyName)
              AND lower(title) = lower($title)
              AND lower(coalesce(url, '')) = lower($url)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$companyName", companyName);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$url", url ?? "");
        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task InsertJobAsync(SqliteConnection connection, SqliteTransaction transaction, JobImportRow job)
    {
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO jobs
                (company_id, company_name, title, location, remote_policy, contract, salary_min, salary_max, seniority,
                 job_type, stack, description, url, publication_date, created_at, updated_at)
            VALUES
                ($companyId, $companyName, $title, $location, $remotePolicy, $contract, $salaryMin, $salaryMax, $seniority,
                 $jobType, $stack, $description, $url, $publicationDate, $now, $now);
            """;
        command.Parameters.AddWithValue("$companyId", job.CompanyId);
        command.Parameters.AddWithValue("$companyName", job.CompanyName);
        command.Parameters.AddWithValue("$title", job.Title);
        command.Parameters.AddWithValue("$location", ToDb(job.Location));
        command.Parameters.AddWithValue("$remotePolicy", ToDb(job.RemotePolicy));
        command.Parameters.AddWithValue("$contract", ToDb(job.Contract));
        command.Parameters.AddWithValue("$salaryMin", ToDb(job.SalaryMin));
        command.Parameters.AddWithValue("$salaryMax", ToDb(job.SalaryMax));
        command.Parameters.AddWithValue("$seniority", ToDb(job.Seniority));
        command.Parameters.AddWithValue("$jobType", ToDb(job.JobType));
        command.Parameters.AddWithValue("$stack", job.Stack);
        command.Parameters.AddWithValue("$description", ToDb(job.Description));
        command.Parameters.AddWithValue("$url", ToDb(job.Url));
        command.Parameters.AddWithValue("$publicationDate", ToDb(job.PublicationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        command.Parameters.AddWithValue("$now", now);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddCompanyParameters(SqliteCommand command, CompanyImportRow company, string now)
    {
        command.Parameters.AddWithValue("$name", company.Name);
        command.Parameters.AddWithValue("$domain", company.Domain);
        command.Parameters.AddWithValue("$secondaryDomains", company.SecondaryDomains);
        command.Parameters.AddWithValue("$city", company.City);
        command.Parameters.AddWithValue("$address", ToDb(company.Address));
        command.Parameters.AddWithValue("$latitude", company.Latitude);
        command.Parameters.AddWithValue("$longitude", company.Longitude);
        command.Parameters.AddWithValue("$website", ToDb(company.Website));
        command.Parameters.AddWithValue("$careerUrl", ToDb(company.CareerUrl));
        command.Parameters.AddWithValue("$linkedinUrl", ToDb(company.LinkedinUrl));
        command.Parameters.AddWithValue("$glassdoorUrl", ToDb(company.GlassdoorUrl));
        command.Parameters.AddWithValue("$knownStack", company.KnownStack);
        command.Parameters.AddWithValue("$notes", ToDb(company.Notes));
        command.Parameters.AddWithValue("$logoUrl", ToDb(company.LogoUrl));
        command.Parameters.AddWithValue("$now", now);
    }

    private static object ToDb(object? value)
    {
        return value ?? DBNull.Value;
    }

    private sealed record CompanyImportRow(
        string Name,
        string Domain,
        string SecondaryDomains,
        string City,
        string? Address,
        double Latitude,
        double Longitude,
        string? Website,
        string? CareerUrl,
        string? LinkedinUrl,
        string? GlassdoorUrl,
        string KnownStack,
        string? Notes,
        string? LogoUrl);

    private sealed record JobImportRow(
        int CompanyId,
        string CompanyName,
        string Title,
        string? Location,
        string? RemotePolicy,
        string? Contract,
        decimal? SalaryMin,
        decimal? SalaryMax,
        string? Seniority,
        string? JobType,
        string Stack,
        string? Description,
        string? Url,
        DateTime? PublicationDate);
}
