using System.Globalization;
using System.Text;
using JobRadarLocal.Data;
using JobRadarLocal.Dtos;
using Microsoft.Data.Sqlite;

namespace JobRadarLocal.Services;

public sealed class ReportService
{
    private readonly AppPaths _paths;
    private readonly Database _database;
    private readonly RadarQueryService _queries;

    public ReportService(AppPaths paths, Database database, RadarQueryService queries)
    {
        _paths = paths;
        _database = database;
        _queries = queries;
    }

    public async Task<ReportFileDto> GenerateAsync(bool scoringIsCurrent = true)
    {
        _paths.EnsureDirectories();

        var companies = await _queries.GetCompaniesAsync();
        var jobs = await _queries.GetJobsAsync();
        var profile = await _queries.GetLatestProfileAsync();
        var now = DateTime.Now;
        var uniqueSuffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..6];
        var fileName = $"job-radar-report-{now:yyyyMMdd-HHmmss}-{uniqueSuffix}.md";
        var path = Path.Combine(_paths.ReportsDirectory, fileName);
        var markdown = BuildMarkdown(now, companies, jobs, profile, scoringIsCurrent);

        await File.WriteAllTextAsync(path, markdown, Encoding.UTF8);
        await SaveReportFileAsync(fileName, path, now);

        return new ReportFileDto(fileName, now);
    }

    public string BuildMarkdown(DateTime generatedAt, IReadOnlyList<CompanyDto> companies, IReadOnlyList<JobDto> jobs, CandidateProfileDto? profile, bool scoringIsCurrent = true)
    {
        var topCompanies = companies
            .Where(company => company.Score is not null)
            .OrderByDescending(company => company.Score!.GlobalScore)
            .ThenBy(company => company.Name)
            .Take(10)
            .ToArray();

        var topJobs = jobs
            .Where(job => job.Score is not null)
            .OrderByDescending(job => job.Score!.GlobalScore)
            .ThenBy(job => job.Title)
            .Take(10)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("# Rapport Job Radar Local");
        builder.AppendLine();
        builder.AppendLine($"Date de génération : {generatedAt:yyyy-MM-dd HH:mm}");
        builder.AppendLine();

        if (!scoringIsCurrent)
        {
            builder.AppendLine("> ⚠️ Rapport non scoré : aucun CV candidat n’a été importé, les scores ne sont pas recalculés et le rapport ne contient pas de scoring à jour.");
            builder.AppendLine();
        }

        builder.AppendLine("## Synthèse");
        builder.AppendLine();
        builder.AppendLine($"- Nombre d’entreprises analysées : {companies.Count}");
        builder.AppendLine($"- Nombre d’offres analysées : {jobs.Count}");
        builder.AppendLine($"- Nombre d’entreprises compatibles : {companies.Count(company => company.Score?.GlobalScore >= 60)}");
        builder.AppendLine($"- Nombre d’offres compatibles : {jobs.Count(job => job.Score?.GlobalScore >= 60)}");
        builder.AppendLine();

        AppendTopCompanies(builder, topCompanies);
        AppendTopJobs(builder, topJobs);
        AppendTechnologies(builder, jobs, profile);
        AppendDomains(builder, companies, jobs);
        AppendRecommendations(builder, topCompanies, topJobs, jobs);

        return builder.ToString();
    }

    private static void AppendTopCompanies(StringBuilder builder, IReadOnlyList<CompanyDto> companies)
    {
        builder.AppendLine("## Top entreprises");
        builder.AppendLine();

        if (companies.Count == 0)
        {
            builder.AppendLine("Aucun score entreprise disponible.");
            builder.AppendLine();
            return;
        }

        for (var index = 0; index < companies.Count; index++)
        {
            var company = companies[index];
            var score = company.Score!;
            builder.AppendLine($"### {index + 1}. {company.Name}");
            builder.AppendLine();
            builder.AppendLine($"- Ville : {company.City}");
            builder.AppendLine($"- Domaine : {company.Domain}");
            builder.AppendLine($"- Score global : {score.GlobalScore}/100");
            builder.AppendLine($"- Stack connue : {FormatList(company.KnownStack)}");
            builder.AppendLine($"- Nombre d’offres : {company.JobCount}");
            builder.AppendLine($"- Raisons principales : {FormatList(score.PositiveReasons.Take(3))}");
            builder.AppendLine($"- Points de vigilance : {FormatList(score.NegativeReasons.Take(3))}");
            builder.AppendLine($"- Liens utiles : {FormatLinks(company)}");
            builder.AppendLine();
        }
    }

    private static void AppendTopJobs(StringBuilder builder, IReadOnlyList<JobDto> jobs)
    {
        builder.AppendLine("## Top offres");
        builder.AppendLine();

        if (jobs.Count == 0)
        {
            builder.AppendLine("Aucun score offre disponible.");
            builder.AppendLine();
            return;
        }

        for (var index = 0; index < jobs.Count; index++)
        {
            var job = jobs[index];
            var score = job.Score!;
            builder.AppendLine($"### {index + 1}. {job.Title}");
            builder.AppendLine();
            builder.AppendLine($"- Entreprise : {job.CompanyName}");
            builder.AppendLine($"- Localisation : {job.Location ?? "Non renseignée"}");
            builder.AppendLine($"- Type de poste : {job.JobType ?? "Non renseigné"}");
            builder.AppendLine($"- Séniorité : {job.Seniority ?? "Non renseignée"}");
            builder.AppendLine($"- Stack : {FormatList(job.Stack)}");
            builder.AppendLine($"- Score global : {score.GlobalScore}/100");
            builder.AppendLine($"- Raisons principales : {FormatList(score.PositiveReasons.Take(3))}");
            builder.AppendLine($"- Compétences manquantes : {FormatList(score.MissingSkills.Take(8))}");
            builder.AppendLine($"- Lien vers l’offre : {FormatLink(job.Url, "Offre")}");
            builder.AppendLine();
        }
    }

    private static void AppendTechnologies(StringBuilder builder, IReadOnlyList<JobDto> jobs, CandidateProfileDto? profile)
    {
        builder.AppendLine("## Technos les plus fréquentes dans les offres");
        builder.AppendLine();
        builder.AppendLine("| Technologie | Nombre d’occurrences | Présente dans mon CV |");
        builder.AppendLine("|---|---:|---|");

        var profileSkills = (profile?.DetectedSkills ?? Array.Empty<string>())
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var technology in jobs.SelectMany(job => job.Stack)
                     .GroupBy(stack => stack, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key)
                     .Take(20))
        {
            var present = profileSkills.Contains(Normalize(technology.Key)) ? "oui" : "non";
            builder.AppendLine($"| {EscapeTable(technology.Key)} | {technology.Count()} | {present} |");
        }

        builder.AppendLine();
    }

    private static void AppendDomains(StringBuilder builder, IReadOnlyList<CompanyDto> companies, IReadOnlyList<JobDto> jobs)
    {
        builder.AppendLine("## Domaines les plus représentés");
        builder.AppendLine();
        builder.AppendLine("| Domaine | Nombre d’entreprises | Nombre d’offres | Score moyen |");
        builder.AppendLine("|---|---:|---:|---:|");

        foreach (var domain in companies.GroupBy(company => company.Domain, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key))
        {
            var jobCount = jobs.Count(job => string.Equals(job.CompanyDomain, domain.Key, StringComparison.OrdinalIgnoreCase));
            var scoredCompanies = domain.Where(company => company.Score is not null).ToArray();
            var average = scoredCompanies.Length == 0 ? 0 : (int)Math.Round(scoredCompanies.Average(company => company.Score!.GlobalScore));
            builder.AppendLine($"| {EscapeTable(domain.Key)} | {domain.Count()} | {jobCount} | {average}/100 |");
        }

        builder.AppendLine();
    }

    private static void AppendRecommendations(StringBuilder builder, IReadOnlyList<CompanyDto> topCompanies, IReadOnlyList<JobDto> topJobs, IReadOnlyList<JobDto> allJobs)
    {
        builder.AppendLine("## Recommandations");
        builder.AppendLine();
        builder.AppendLine($"- Entreprises à cibler en priorité : {FormatList(topCompanies.Take(3).Select(company => $"{company.Name} ({company.Score!.GlobalScore}/100)"))}");
        builder.AppendLine($"- Offres à regarder en premier : {FormatList(topJobs.Take(5).Select(job => $"{job.Title} chez {job.CompanyName} ({job.Score!.GlobalScore}/100)"))}");

        var missingSkills = topJobs.SelectMany(job => job.Score?.MissingSkills ?? Array.Empty<string>())
            .GroupBy(skill => skill, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(6)
            .Select(group => group.Key);

        builder.AppendLine($"- Compétences à renforcer : {FormatList(missingSkills)}");

        var sectors = allJobs.GroupBy(job => job.CompanyDomain, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Take(4)
            .Select(group => group.Key);

        builder.AppendLine($"- Secteurs à surveiller : {FormatList(sectors)}");
        builder.AppendLine();
    }

    private async Task SaveReportFileAsync(string fileName, string path, DateTime createdAt)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO report_files (file_name, path, created_at)
            VALUES ($fileName, $path, $createdAt)
            ON CONFLICT(file_name) DO UPDATE SET
                path = excluded.path,
                created_at = excluded.created_at;
            """;
        command.Parameters.AddWithValue("$fileName", fileName);
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }

    private static string FormatList(IEnumerable<string> values)
    {
        var list = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return list.Length == 0 ? "Non renseigné" : string.Join(", ", list);
    }

    private static string FormatLinks(CompanyDto company)
    {
        var links = new[]
        {
            FormatLink(company.Website, "Site"),
            FormatLink(company.CareerUrl, "Carrière"),
            FormatLink(company.LinkedinUrl, "LinkedIn"),
            FormatLink(company.GlassdoorUrl, "Glassdoor")
        }.Where(link => link != "Non renseigné");

        return FormatList(links);
    }

    private static string FormatLink(string? url, string label)
    {
        return string.IsNullOrWhiteSpace(url) ? "Non renseigné" : $"[{label}]({url})";
    }

    private static string EscapeTable(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return RadarText.NormalizeSearch(value).Replace(" ", "", StringComparison.Ordinal);
    }
}
