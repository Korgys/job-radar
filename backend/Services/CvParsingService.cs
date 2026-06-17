using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using JobRadarLocal.Data;
using JobRadarLocal.Dtos;
using Microsoft.Data.Sqlite;

namespace JobRadarLocal.Services;

public sealed class CvParsingService : ICvParsingService
{
    private static readonly KeywordDefinition[] SkillKeywords =
    [
        new("C#", ["c#", "csharp"]),
        new(".NET", [".net", "dotnet"]),
        new("ASP.NET Core", ["asp.net core", "aspnet core"]),
        new("SQL", ["sql"]),
        new("SQL Server", ["sql server", "mssql"]),
        new("Azure", ["azure"]),
        new("Azure DevOps", ["azure devops", "ado"]),
        new("Git", ["git"]),
        new("CI/CD", ["ci/cd", "cicd", "integration continue", "deploiement continu"]),
        new("Docker", ["docker"]),
        new("Kubernetes", ["kubernetes", "k8s"]),
        new("Angular", ["angular"]),
        new("React", ["react"]),
        new("TypeScript", ["typescript", "ts"]),
        new("JavaScript", ["javascript", "js"]),
        new("Node.js", ["node.js", "nodejs"]),
        new("Python", ["python"]),
        new("PowerShell", ["powershell"]),
        new("REST API", ["rest api", "api rest", "restful"]),
        new("WCF", ["wcf"]),
        new("VB.NET", ["vb.net", "vb net"])
    ];

    private static readonly KeywordDefinition[] RoleKeywords =
    [
        new("développeur backend", ["developpeur backend", "développeur backend", "backend developer"]),
        new("développeur fullstack", ["developpeur fullstack", "développeur fullstack", "fullstack developer", "full stack"]),
        new("tech lead", ["tech lead", "technical leader"]),
        new("lead developer", ["lead developer", "lead développeur", "lead developpeur"]),
        new("ingénieur cybersécurité", ["ingenieur cybersecurite", "ingénieur cybersécurité", "cybersecurity engineer"]),
        new("data engineer", ["data engineer"]),
        new("analytics engineer", ["analytics engineer"]),
        new("devops", ["devops", "site reliability", "sre"])
    ];

    private static readonly KeywordDefinition[] DomainKeywords =
    [
        new("banque", ["banque", "bancaire"]),
        new("assurance", ["assurance"]),
        new("santé", ["sante", "santé", "healthcare"]),
        new("industrie", ["industrie", "industriel"]),
        new("SaaS", ["saas"]),
        new("IoT", ["iot", "internet of things"]),
        new("cybersécurité", ["cybersecurite", "cybersécurité", "cybersecurity"]),
        new("finance", ["finance", "financier"])
    ];

    private readonly Database _database;

    public CvParsingService(Database database)
    {
        _database = database;
    }

    public async Task<CandidateProfileDto> ImportAsync(string fileName, Stream stream)
    {
        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Formats CV supportés en V0.3 : .txt et .md. PDF/DOCX sont volontairement isolés derrière l'interface ICvParsingService.");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rawText = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            throw new InvalidOperationException("Le CV est vide.");
        }

        var parsed = ParseText(rawText);
        return await SaveProfileAsync(parsed);
    }

    public async Task<CandidateProfileDto> UpdateLatestProfileAsync(UpdateCandidateProfileRequest request)
    {
        using var connection = _database.OpenConnection();
        var profileId = await GetLatestProfileIdAsync(connection)
            ?? throw new InvalidOperationException("Aucun CV importe.");

        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE candidate_profiles
            SET detected_skills = $skills,
                detected_domains = $domains,
                detected_seniority = $seniority,
                updated_at = $now
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$skills", NormalizeProfileList(request.DetectedSkills));
        command.Parameters.AddWithValue("$domains", NormalizeProfileList(request.DetectedDomains));
        command.Parameters.AddWithValue("$seniority", request.DetectedSeniority?.Trim() ?? "");
        command.Parameters.AddWithValue("$now", now);
        command.Parameters.AddWithValue("$id", profileId);
        await command.ExecuteNonQueryAsync();

        return await ReadProfileByIdAsync(connection, profileId)
            ?? throw new InvalidOperationException("Profil introuvable.");
    }

    public ParsedCv ParseText(string rawText)
    {
        var normalized = RadarText.NormalizeSearch(rawText);
        var skills = DetectKeywords(normalized, SkillKeywords);
        var roles = DetectKeywords(normalized, RoleKeywords);
        var domains = DetectKeywords(normalized, DomainKeywords);
        var seniority = DetectSeniority(normalized, roles);
        var summary = BuildSummary(rawText);

        return new ParsedCv(rawText, skills, roles, domains, seniority, summary);
    }

    private async Task<CandidateProfileDto> SaveProfileAsync(ParsedCv parsed)
    {
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO candidate_profiles
                (raw_text, detected_skills, detected_roles, detected_domains, detected_seniority, experiences_summary, created_at, updated_at)
            VALUES
                ($rawText, $skills, $roles, $domains, $seniority, $summary, $now, $now)
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$rawText", parsed.RawText);
        command.Parameters.AddWithValue("$skills", RadarText.JoinList(parsed.Skills));
        command.Parameters.AddWithValue("$roles", RadarText.JoinList(parsed.Roles));
        command.Parameters.AddWithValue("$domains", RadarText.JoinList(parsed.Domains));
        command.Parameters.AddWithValue("$seniority", parsed.Seniority);
        command.Parameters.AddWithValue("$summary", parsed.ExperiencesSummary);
        command.Parameters.AddWithValue("$now", now);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return new CandidateProfileDto(
            id,
            parsed.RawText,
            parsed.Skills,
            parsed.Roles,
            parsed.Domains,
            parsed.Seniority,
            parsed.ExperiencesSummary,
            DateTime.Parse(now, CultureInfo.InvariantCulture),
            DateTime.Parse(now, CultureInfo.InvariantCulture));
    }

    private static IReadOnlyList<string> DetectKeywords(string normalizedText, IEnumerable<KeywordDefinition> definitions)
    {
        return definitions
            .Where(definition => definition.Aliases.Any(alias => ContainsAlias(normalizedText, alias)))
            .Select(definition => definition.Name)
            .ToArray();
    }

    private static bool ContainsAlias(string normalizedText, string alias)
    {
        var normalizedAlias = RadarText.NormalizeSearch(alias);
        if (normalizedAlias.Length <= 2)
        {
            return Regex.IsMatch(normalizedText, $@"(^|[^a-z0-9+#.]){Regex.Escape(normalizedAlias)}([^a-z0-9+#.]|$)", RegexOptions.IgnoreCase);
        }

        return normalizedText.Contains(normalizedAlias, StringComparison.Ordinal);
    }

    private static string DetectSeniority(string normalizedText, IReadOnlyList<string> roles)
    {
        if (roles.Any(role => role.Contains("lead", StringComparison.OrdinalIgnoreCase))
            || ContainsAlias(normalizedText, "tech lead")
            || ContainsAlias(normalizedText, "lead developer"))
        {
            return "lead";
        }

        var years = Regex.Matches(normalizedText, @"(\d{1,2})\s*(ans|annees|annee)")
            .Select(match => int.TryParse(match.Groups[1].Value, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        if (ContainsAlias(normalizedText, "senior") || years >= 8)
        {
            return "senior";
        }

        if (ContainsAlias(normalizedText, "confirme") || years >= 4)
        {
            return "confirmé";
        }

        if (ContainsAlias(normalizedText, "junior") || years > 0)
        {
            return "junior";
        }

        return "";
    }

    private static string BuildSummary(string rawText)
    {
        var lines = rawText.Replace("\r\n", "\n").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var selected = lines.Take(6);
        var summary = string.Join(" ", selected);
        return summary.Length <= 700 ? summary : summary[..700];
    }

    private static async Task<int?> GetLatestProfileIdAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM candidate_profiles ORDER BY created_at DESC LIMIT 1;";
        var value = await command.ExecuteScalarAsync();
        return value is null ? null : Convert.ToInt32(value);
    }

    private static async Task<CandidateProfileDto?> ReadProfileByIdAsync(SqliteConnection connection, int id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM candidate_profiles WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapProfile(reader) : null;
    }

    private static CandidateProfileDto MapProfile(SqliteDataReader reader)
    {
        return new CandidateProfileDto(
            ReadInt(reader, "id"),
            ReadString(reader, "raw_text"),
            RadarText.SplitList(ReadString(reader, "detected_skills")),
            RadarText.SplitList(ReadString(reader, "detected_roles")),
            RadarText.SplitList(ReadString(reader, "detected_domains")),
            ReadString(reader, "detected_seniority"),
            ReadNullableString(reader, "experiences_summary"),
            ReadDateTime(reader, "created_at") ?? DateTime.MinValue,
            ReadDateTime(reader, "updated_at") ?? DateTime.MinValue);
    }

    private static string NormalizeProfileList(IEnumerable<string>? values)
    {
        return RadarText.JoinList(values ?? Array.Empty<string>());
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

    private static DateTime? ReadDateTime(SqliteDataReader reader, string name)
    {
        return DateTime.TryParse(ReadNullableString(reader, name), out var date) ? date : null;
    }

    private sealed record KeywordDefinition(string Name, IReadOnlyList<string> Aliases);
}
