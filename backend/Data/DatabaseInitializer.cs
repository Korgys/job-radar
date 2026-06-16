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
                glassdoor_url TEXT NULL,
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
            CREATE INDEX IF NOT EXISTS idx_jobs_dedupe ON jobs(company_name, title, url);

            CREATE TABLE IF NOT EXISTS candidate_profiles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                raw_text TEXT NOT NULL,
                detected_skills TEXT NOT NULL DEFAULT '',
                detected_roles TEXT NOT NULL DEFAULT '',
                detected_domains TEXT NOT NULL DEFAULT '',
                detected_seniority TEXT NOT NULL DEFAULT '',
                experiences_summary TEXT NULL,
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

            CREATE TABLE IF NOT EXISTS report_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_name TEXT NOT NULL UNIQUE,
                path TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        NormalizeExistingStacks(connection);
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
