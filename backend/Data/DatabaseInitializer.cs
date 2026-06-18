using Microsoft.Data.Sqlite;
using JobRadarLocal.Services;

namespace JobRadarLocal.Data;

public sealed class DatabaseInitializer
{
    private readonly Database _database;
    private readonly AppPaths _paths;

    public DatabaseInitializer(Database database, AppPaths paths)
    {
        _database = database;
        _paths = paths;
    }

    public void Initialize()
    {
        _paths.EnsureDirectories();
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS companies (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                domain TEXT NOT NULL DEFAULT 'Autre',
                secondary_domains TEXT NOT NULL DEFAULT '',
                city TEXT NOT NULL DEFAULT '',
                address TEXT NULL,
                latitude REAL NULL,
                longitude REAL NULL,
                website TEXT NULL,
                career_url TEXT NULL,
                linkedin_url TEXT NULL,
                known_stack TEXT NOT NULL DEFAULT '',
                notes TEXT NULL,
                logo_url TEXT NULL,
                incomplete INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                UNIQUE(name, city)
            );

            CREATE TABLE IF NOT EXISTS jobs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                company_id INTEGER NOT NULL,
                company_name TEXT NOT NULL,
                title TEXT NOT NULL,
                location TEXT NULL,
                remote_policy TEXT NULL,
                contract TEXT NULL,
                salary_min REAL NULL,
                salary_max REAL NULL,
                seniority TEXT NULL,
                job_type TEXT NULL,
                stack TEXT NOT NULL DEFAULT '',
                description TEXT NULL,
                url TEXT NULL,
                publication_date TEXT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_jobs_company ON jobs(company_id);

            CREATE TABLE IF NOT EXISTS candidate_profiles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                raw_text TEXT NOT NULL,
                detected_skills TEXT NOT NULL DEFAULT '',
                detected_roles TEXT NOT NULL DEFAULT '',
                detected_domains TEXT NOT NULL DEFAULT '',
                detected_seniority TEXT NOT NULL DEFAULT '',
                experiences_summary TEXT NULL,
                preferred_locations TEXT NOT NULL DEFAULT '',
                remote_preference TEXT NULL,
                target_salary REAL NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS company_scores (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                company_id INTEGER NOT NULL UNIQUE,
                global_score INTEGER NOT NULL,
                stack_score INTEGER NOT NULL,
                role_score INTEGER NOT NULL,
                domain_score INTEGER NOT NULL,
                seniority_score INTEGER NOT NULL,
                location_score INTEGER NOT NULL,
                salary_score INTEGER NOT NULL DEFAULT 0,
                strategic_score INTEGER NOT NULL DEFAULT 0,
                positive_reasons TEXT NOT NULL DEFAULT '',
                negative_reasons TEXT NOT NULL DEFAULT '',
                missing_skills TEXT NOT NULL DEFAULT '',
                updated_at TEXT NOT NULL,
                FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS job_scores (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                job_id INTEGER NOT NULL UNIQUE,
                global_score INTEGER NOT NULL,
                stack_score INTEGER NOT NULL,
                role_score INTEGER NOT NULL,
                domain_score INTEGER NOT NULL,
                seniority_score INTEGER NOT NULL,
                location_score INTEGER NOT NULL,
                salary_score INTEGER NOT NULL DEFAULT 0,
                strategic_score INTEGER NOT NULL DEFAULT 0,
                positive_reasons TEXT NOT NULL DEFAULT '',
                negative_reasons TEXT NOT NULL DEFAULT '',
                missing_skills TEXT NOT NULL DEFAULT '',
                updated_at TEXT NOT NULL,
                FOREIGN KEY(job_id) REFERENCES jobs(id) ON DELETE CASCADE
            );
            """;
        command.ExecuteNonQuery();
        EnsureCandidatePreferenceColumns(connection);
        NormalizeExistingStacks(connection);
        EnsureJobDedupeIndexes(connection);
    }

    private static void EnsureCandidatePreferenceColumns(SqliteConnection connection)
    {
        EnsureColumn(connection, "candidate_profiles", "preferred_locations", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "candidate_profiles", "remote_preference", "TEXT NULL");
        EnsureColumn(connection, "candidate_profiles", "target_salary", "REAL NULL");
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 1 && exception.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static void EnsureJobDedupeIndexes(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            DROP INDEX IF EXISTS idx_jobs_dedupe;

            DELETE FROM jobs
            WHERE id IN (
                SELECT jobs.id
                FROM jobs
                INNER JOIN (
                    SELECT lower(url) AS dedupe_url, min(id) AS keep_id
                    FROM jobs
                    WHERE url IS NOT NULL AND trim(url) <> ''
                    GROUP BY lower(url)
                    HAVING count(*) > 1
                ) duplicates
                    ON lower(jobs.url) = duplicates.dedupe_url
                WHERE jobs.id <> duplicates.keep_id
                  AND jobs.url IS NOT NULL
                  AND trim(jobs.url) <> ''
            );

            DELETE FROM jobs
            WHERE id IN (
                SELECT jobs.id
                FROM jobs
                INNER JOIN (
                    SELECT company_id, lower(title) AS dedupe_title, min(id) AS keep_id
                    FROM jobs
                    WHERE url IS NULL OR trim(url) = ''
                    GROUP BY company_id, lower(title)
                    HAVING count(*) > 1
                ) duplicates
                    ON jobs.company_id = duplicates.company_id
                   AND lower(jobs.title) = duplicates.dedupe_title
                WHERE jobs.id <> duplicates.keep_id
                  AND (jobs.url IS NULL OR trim(jobs.url) = '')
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_jobs_unique_url
            ON jobs(lower(url))
            WHERE url IS NOT NULL AND trim(url) <> '';

            CREATE UNIQUE INDEX IF NOT EXISTS idx_jobs_unique_company_title_without_url
            ON jobs(company_id, lower(title))
            WHERE url IS NULL OR trim(url) = '';
            """;
        command.ExecuteNonQuery();
    }

    private static void NormalizeExistingStacks(SqliteConnection connection)
    {
        NormalizeColumn(connection, "companies", "known_stack");
        NormalizeColumn(connection, "jobs", "stack");
    }

    private static void NormalizeColumn(SqliteConnection connection, string table, string column)
    {
        using var select = connection.CreateCommand();
        select.CommandText = $"SELECT id, {column} FROM {table};";

        var updates = new List<(int Id, string Value)>();
        using (var reader = select.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var value = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var normalized = RadarText.CleanStackList(value);
                if (!string.Equals(value, normalized, StringComparison.Ordinal))
                {
                    updates.Add((id, normalized));
                }
            }
        }

        foreach (var update in updates)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"UPDATE {table} SET {column} = $value WHERE id = $id;";
            command.Parameters.AddWithValue("$value", update.Value);
            command.Parameters.AddWithValue("$id", update.Id);
            command.ExecuteNonQuery();
        }
    }
}
