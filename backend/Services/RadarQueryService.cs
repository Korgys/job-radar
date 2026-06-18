using JobRadarLocal.Data;
using JobRadarLocal.Dtos;
using Microsoft.Data.Sqlite;

namespace JobRadarLocal.Services;

public sealed class RadarQueryService
{
    private readonly Database _database;
    private readonly AppPaths _paths;

    public RadarQueryService(Database database, AppPaths paths)
    {
        _database = database;
        _paths = paths;
    }

    public async Task<IReadOnlyList<CompanyDto>> GetCompaniesAsync()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.*,
                   COUNT(j.id) AS job_count,
                   cs.global_score AS score_global,
                   cs.stack_score AS score_stack,
                   cs.role_score AS score_role,
                   cs.domain_score AS score_domain,
                   cs.seniority_score AS score_seniority,
                   cs.location_score AS score_location,
                   cs.salary_score AS score_salary,
                   cs.strategic_score AS score_strategic,
                   cs.positive_reasons AS score_positive,
                   cs.negative_reasons AS score_negative,
                   cs.missing_skills AS score_missing
            FROM companies c
            LEFT JOIN jobs j ON j.company_id = c.id
            LEFT JOIN company_scores cs ON cs.company_id = c.id
            GROUP BY c.id
            ORDER BY c.name;
            """;

        var companies = new List<CompanyDto>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            companies.Add(MapCompany(reader));
        }

        return companies;
    }

    public async Task<IReadOnlyList<JobDto>> GetJobsAsync()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT j.*,
                   c.domain AS company_domain,
                   js.global_score AS score_global,
                   js.stack_score AS score_stack,
                   js.role_score AS score_role,
                   js.domain_score AS score_domain,
                   js.seniority_score AS score_seniority,
                   js.location_score AS score_location,
                   js.salary_score AS score_salary,
                   js.strategic_score AS score_strategic,
                   js.positive_reasons AS score_positive,
                   js.negative_reasons AS score_negative,
                   js.missing_skills AS score_missing
            FROM jobs j
            INNER JOIN companies c ON c.id = j.company_id
            LEFT JOIN job_scores js ON js.job_id = j.id
            ORDER BY j.publication_date DESC, j.created_at DESC;
            """;

        var jobs = new List<JobDto>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            jobs.Add(MapJob(reader));
        }

        return jobs;
    }

    public async Task<CandidateProfileDto?> GetLatestProfileAsync()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM candidate_profiles
            ORDER BY created_at DESC
            LIMIT 1;
            """;

        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new CandidateProfileDto(
            ReadInt(reader, "id"),
            ReadString(reader, "raw_text"),
            RadarText.SplitList(ReadString(reader, "detected_skills")),
            RadarText.SplitList(ReadString(reader, "detected_roles")),
            RadarText.SplitList(ReadString(reader, "detected_domains")),
            ReadString(reader, "detected_seniority"),
            ReadNullableString(reader, "experiences_summary"),
            RadarText.SplitList(ReadString(reader, "preferred_locations")),
            ReadNullableString(reader, "remote_preference"),
            ReadDecimal(reader, "target_salary"),
            ReadDateTime(reader, "created_at") ?? DateTime.MinValue,
            ReadDateTime(reader, "updated_at") ?? DateTime.MinValue);
    }

    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        using var connection = _database.OpenConnection();
        var companyCount = await ExecuteScalarIntAsync(connection, "SELECT COUNT(*) FROM companies;");
        var jobCount = await ExecuteScalarIntAsync(connection, "SELECT COUNT(*) FROM jobs;");
        var compatibleCompanyCount = await ExecuteScalarIntAsync(connection, "SELECT COUNT(*) FROM company_scores WHERE global_score >= 60;");
        var compatibleJobCount = await ExecuteScalarIntAsync(connection, "SELECT COUNT(*) FROM job_scores WHERE global_score >= 60;");
        var lastProfile = await ExecuteScalarStringAsync(connection, "SELECT created_at FROM candidate_profiles ORDER BY created_at DESC LIMIT 1;");

        return new DashboardStatsDto(
            companyCount,
            jobCount,
            ParseDateTime(lastProfile),
            compatibleCompanyCount,
            compatibleJobCount);
    }

    private static CompanyDto MapCompany(SqliteDataReader reader)
    {
        return new CompanyDto(
            ReadInt(reader, "id"),
            ReadString(reader, "name"),
            ReadString(reader, "domain"),
            RadarText.SplitList(ReadString(reader, "secondary_domains")),
            ReadString(reader, "city"),
            ReadNullableString(reader, "address"),
            ReadDouble(reader, "latitude"),
            ReadDouble(reader, "longitude"),
            ReadNullableString(reader, "website"),
            ReadNullableString(reader, "career_url"),
            ReadNullableString(reader, "linkedin_url"),
            RadarText.SplitList(ReadString(reader, "known_stack")),
            ReadNullableString(reader, "notes"),
            ReadNullableString(reader, "logo_url"),
            ReadInt(reader, "incomplete") == 1,
            ReadInt(reader, "job_count"),
            MapScore(reader));
    }

    private static JobDto MapJob(SqliteDataReader reader)
    {
        return new JobDto(
            ReadInt(reader, "id"),
            ReadInt(reader, "company_id"),
            ReadString(reader, "company_name"),
            ReadString(reader, "company_domain"),
            ReadString(reader, "title"),
            ReadNullableString(reader, "location"),
            ReadNullableString(reader, "remote_policy"),
            ReadNullableString(reader, "contract"),
            ReadDecimal(reader, "salary_min"),
            ReadDecimal(reader, "salary_max"),
            ReadNullableString(reader, "seniority"),
            ReadNullableString(reader, "job_type"),
            RadarText.SplitList(ReadString(reader, "stack")),
            ReadNullableString(reader, "description"),
            ReadNullableString(reader, "url"),
            ReadDateTime(reader, "publication_date"),
            MapScore(reader));
    }

    private static ScoreDto? MapScore(SqliteDataReader reader)
    {
        if (reader.IsDBNull(reader.GetOrdinal("score_global")))
        {
            return null;
        }

        return new ScoreDto(
            ReadInt(reader, "score_global"),
            ReadInt(reader, "score_stack"),
            ReadInt(reader, "score_role"),
            ReadInt(reader, "score_domain"),
            ReadInt(reader, "score_seniority"),
            ReadInt(reader, "score_location"),
            ReadInt(reader, "score_salary"),
            ReadInt(reader, "score_strategic"),
            RadarText.SplitList(ReadNullableString(reader, "score_positive")),
            RadarText.SplitList(ReadNullableString(reader, "score_negative")),
            RadarText.SplitList(ReadNullableString(reader, "score_missing")));
    }

    private static async Task<int> ExecuteScalarIntAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value);
    }

    private static async Task<string?> ExecuteScalarStringAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync() as string;
    }

    private static int ReadInt(SqliteDataReader reader, string name)
    {
        return Convert.ToInt32(reader.GetValue(reader.GetOrdinal(name)));
    }

    private static string ReadString(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
    }

    private static string? ReadNullableString(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static double? ReadDouble(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDouble(reader.GetValue(ordinal));
    }

    private static decimal? ReadDecimal(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTime(SqliteDataReader reader, string name)
    {
        return ParseDateTime(ReadNullableString(reader, name));
    }

    private static DateTime? ParseDateTime(string? value)
    {
        return DateTime.TryParse(value, out var date) ? date : null;
    }
}
