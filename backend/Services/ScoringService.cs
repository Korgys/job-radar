using System.Globalization;
using JobRadarLocal.Data;
using JobRadarLocal.Dtos;
using Microsoft.Data.Sqlite;

namespace JobRadarLocal.Services;

public sealed class ScoringService
{
    private readonly Database _database;
    private readonly RadarQueryService _queries;

    public ScoringService(Database database, RadarQueryService queries)
    {
        _database = database;
        _queries = queries;
    }

    public async Task<RecalculateResultDto> RecalculateAsync()
    {
        var profile = await _queries.GetLatestProfileAsync()
            ?? throw new InvalidOperationException("Importez un CV avant de recalculer les scores.");

        var companies = await _queries.GetCompaniesAsync();
        var jobs = await _queries.GetJobsAsync();
        var jobsByCompany = jobs.GroupBy(job => job.CompanyId).ToDictionary(group => group.Key, group => group.ToArray());

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var company in companies)
        {
            jobsByCompany.TryGetValue(company.Id, out var companyJobs);
            var score = CalculateCompanyScore(profile, company, companyJobs ?? Array.Empty<JobDto>());
            await UpsertCompanyScoreAsync(connection, transaction, company.Id, score);
        }

        foreach (var job in jobs)
        {
            var score = CalculateJobScore(profile, job);
            await UpsertJobScoreAsync(connection, transaction, job.Id, score);
        }

        transaction.Commit();
        return new RecalculateResultDto(companies.Count, jobs.Count);
    }

    public ScoreDto CalculateCompanyScore(CandidateProfileDto profile, CompanyDto company, IReadOnlyList<JobDto> jobs)
    {
        var positive = new List<string>();
        var negative = new List<string>();
        var targetStack = company.KnownStack.Concat(jobs.SelectMany(job => job.Stack))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var stackResult = ScoreCompanyStack(profile.DetectedSkills, targetStack, positive, negative);
        var opportunitiesScore = ScoreCompanyOpportunities(jobs, positive, negative);
        var domainScore = ScoreCompanyDomain(profile.DetectedDomains, company, positive, negative);
        var roleScore = ScoreCompanyRoles(profile.DetectedRoles, jobs, positive, negative);
        var locationScore = ScoreCompanyLocation(company, jobs, positive, negative);
        var dataQualityScore = ScoreCompanyActionability(company, targetStack, positive, negative);
        var global = stackResult.Score + opportunitiesScore + domainScore + roleScore + locationScore + dataQualityScore;

        return new ScoreDto(
            Math.Clamp(global, 0, 100),
            stackResult.Score,
            roleScore,
            domainScore,
            0,
            locationScore,
            dataQualityScore,
            opportunitiesScore,
            positive,
            negative,
            stackResult.MissingSkills);
    }

    public ScoreDto CalculateJobScore(CandidateProfileDto profile, JobDto job)
    {
        var positive = new List<string>();
        var negative = new List<string>();
        var stackResult = ScoreStack(profile.DetectedSkills, job.Stack, 35, positive, negative);
        var roleScore = ScoreRoles(profile.DetectedRoles, [job.Title, job.JobType ?? "", job.Description ?? ""], 25, positive, negative);
        var seniorityScore = ScoreSeniority(profile.DetectedSeniority, job.Seniority, 15, positive, negative);
        var domainScore = ScoreDomain(profile.DetectedDomains, job.CompanyDomain, 10, positive, negative);
        var locationScore = ScoreRemoteOrLocation(job.RemotePolicy, job.Location, 10, positive, negative);
        var salaryScore = ScoreSalary(job, positive, negative);
        var global = stackResult.Score + roleScore + seniorityScore + domainScore + locationScore + salaryScore;

        return new ScoreDto(
            Math.Clamp(global, 0, 100),
            stackResult.Score,
            roleScore,
            domainScore,
            seniorityScore,
            locationScore,
            salaryScore,
            0,
            positive,
            negative,
            stackResult.MissingSkills);
    }

    private static StackScoreResult ScoreStack(IReadOnlyList<string> candidateSkills, IReadOnlyList<string> targetStack, int maxPoints, List<string> positive, List<string> negative)
    {
        var target = targetStack
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (target.Length == 0)
        {
            negative.Add("Stack non renseignée.");
            return new StackScoreResult(0, Array.Empty<string>());
        }

        var candidateKeys = candidateSkills.Select(NormalizeSkill).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matching = target.Where(skill => candidateKeys.Contains(NormalizeSkill(skill))).ToArray();
        var missing = target.Where(skill => !candidateKeys.Contains(NormalizeSkill(skill))).ToArray();
        var score = (int)Math.Round(maxPoints * (double)matching.Length / target.Length);

        if (matching.Length > 0)
        {
            positive.Add($"Compatibilité stack : {string.Join(", ", matching.Take(6))}.");
        }

        if (missing.Length > 0)
        {
            negative.Add($"Compétences non détectées : {string.Join(", ", missing.Take(6))}.");
        }

        return new StackScoreResult(score, missing);
    }

    private static StackScoreResult ScoreCompanyStack(IReadOnlyList<string> candidateSkills, IReadOnlyList<string> targetStack, List<string> positive, List<string> negative)
    {
        var target = targetStack
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (target.Length == 0)
        {
            negative.Add("Stack entreprise non renseignée.");
            return new StackScoreResult(0, Array.Empty<string>());
        }

        var candidateKeys = candidateSkills.Select(NormalizeSkill).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matching = target.Where(skill => candidateKeys.Contains(NormalizeSkill(skill))).ToArray();
        var missing = target.Where(skill => !candidateKeys.Contains(NormalizeSkill(skill))).ToArray();
        var expectedMatches = Math.Min(5, target.Length);
        var score = (int)Math.Round(30 * Math.Min(matching.Length, expectedMatches) / (double)expectedMatches);

        if (matching.Length > 0)
        {
            positive.Add($"Stack compatible : {string.Join(", ", matching.Take(6))}.");
        }

        if (missing.Length > 0)
        {
            negative.Add($"Stack à vérifier : {string.Join(", ", missing.Take(6))}.");
        }

        return new StackScoreResult(score, missing);
    }

    private static int ScoreCompanyOpportunities(IReadOnlyList<JobDto> jobs, List<string> positive, List<string> negative)
    {
        var count = jobs.Count;
        if (count >= 5)
        {
            positive.Add($"{count} offres liées disponibles.");
            return 25;
        }

        if (count >= 3)
        {
            positive.Add($"{count} offres liées disponibles.");
            return 21;
        }

        if (count == 2)
        {
            positive.Add("2 offres liées disponibles.");
            return 17;
        }

        if (count == 1)
        {
            positive.Add("1 offre liée disponible.");
            return 13;
        }

        negative.Add("Aucune offre liée détectée.");
        return 0;
    }

    private static int ScoreCompanyDomain(IReadOnlyList<string> candidateDomains, CompanyDto company, List<string> positive, List<string> negative)
    {
        var companyDomains = new[] { company.Domain }
            .Concat(company.SecondaryDomains)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (companyDomains.Length == 0)
        {
            negative.Add("Domaine entreprise non renseigné.");
            return 0;
        }

        var normalizedCandidateDomains = candidateDomains.Select(RadarText.NormalizeSearch).ToArray();
        var exactMatch = companyDomains.FirstOrDefault(domain => normalizedCandidateDomains.Contains(RadarText.NormalizeSearch(domain)));
        if (exactMatch is not null)
        {
            positive.Add($"Domaine cohérent : {exactMatch}.");
            return 15;
        }

        var relatedMatch = companyDomains.FirstOrDefault(domain => normalizedCandidateDomains.Any(candidate => DomainsAreRelated(candidate, domain)));
        if (relatedMatch is not null)
        {
            positive.Add($"Domaine proche du profil : {relatedMatch}.");
            return 11;
        }

        negative.Add($"Domaine moins aligné : {company.Domain}.");
        return 0;
    }

    private static int ScoreCompanyRoles(IReadOnlyList<string> candidateRoles, IReadOnlyList<JobDto> jobs, List<string> positive, List<string> negative)
    {
        if (jobs.Count == 0)
        {
            negative.Add("Aucun type de poste disponible à comparer.");
            return 0;
        }

        return ScoreRoles(candidateRoles, jobs.SelectMany(job => new[] { job.Title, job.JobType ?? "", job.Description ?? "" }), 10, positive, negative);
    }

    private static int ScoreDomain(IReadOnlyList<string> candidateDomains, string companyDomain, int maxPoints, List<string> positive, List<string> negative)
    {
        if (string.IsNullOrWhiteSpace(companyDomain))
        {
            negative.Add("Domaine entreprise non renseigné.");
            return 0;
        }

        var normalizedDomain = RadarText.NormalizeSearch(companyDomain);
        var domains = candidateDomains.Select(RadarText.NormalizeSearch).ToArray();

        if (domains.Contains(normalizedDomain))
        {
            positive.Add($"Domaine cohérent : {companyDomain}.");
            return maxPoints;
        }

        if ((normalizedDomain is "banque" or "assurance" && domains.Contains("finance")) || (normalizedDomain == "finance" && domains.Contains("banque")))
        {
            positive.Add($"Domaine proche du profil : {companyDomain}.");
            return (int)Math.Round(maxPoints * 0.7);
        }

        negative.Add($"Domaine moins aligné : {companyDomain}.");
        return 0;
    }

    private static int ScoreRoles(IReadOnlyList<string> candidateRoles, IEnumerable<string> targetTexts, int maxPoints, List<string> positive, List<string> negative)
    {
        var combinedTarget = RadarText.NormalizeSearch(string.Join(" ", targetTexts));
        if (string.IsNullOrWhiteSpace(combinedTarget))
        {
            negative.Add("Type de poste non renseigné.");
            return 0;
        }

        var matchingRoles = candidateRoles.Where(role => RoleMatches(role, combinedTarget)).ToArray();
        if (matchingRoles.Length > 0)
        {
            positive.Add($"Rôle pertinent : {string.Join(", ", matchingRoles.Take(3))}.");
            return maxPoints;
        }

        negative.Add("Type de poste moins aligné avec les rôles détectés.");
        return 0;
    }

    private static bool RoleMatches(string role, string normalizedTarget)
    {
        var normalizedRole = RadarText.NormalizeSearch(role);
        return normalizedRole switch
        {
            "developpeur backend" => ContainsAny(normalizedTarget, "backend", "back-end", "api", "asp.net", ".net", "software_engineer"),
            "developpeur fullstack" => ContainsAny(normalizedTarget, "fullstack", "full stack", "backend", "frontend", "react", "angular", ".net", "software_engineer"),
            "tech lead" => ContainsAny(normalizedTarget, "tech lead", "lead", "architecte"),
            "lead developer" => ContainsAny(normalizedTarget, "lead developer", "lead dev", "lead"),
            "ingenieur cybersecurite" => ContainsAny(normalizedTarget, "cyber", "securite", "security"),
            "data engineer" => ContainsAny(normalizedTarget, "data engineer", "etl", "pipeline"),
            "analytics engineer" => ContainsAny(normalizedTarget, "analytics", "bi", "dbt"),
            "devops" => ContainsAny(normalizedTarget, "devops", "ci/cd", "kubernetes", "docker", "sre"),
            _ => normalizedTarget.Contains(normalizedRole, StringComparison.Ordinal)
        };
    }

    private static int ScoreCompanyLocation(CompanyDto company, IReadOnlyList<JobDto> jobs, List<string> positive, List<string> negative)
    {
        if (jobs.Any(job => IsRemoteFriendly(job.RemotePolicy)))
        {
            positive.Add("Télétravail ou hybride disponible.");
            return 10;
        }

        if (!string.IsNullOrWhiteSpace(company.City))
        {
            positive.Add($"Localisation exploitable : {company.City}.");
            return 7;
        }

        negative.Add("Localisation insuffisamment renseignée.");
        return 0;
    }

    private static int ScoreCompanyActionability(CompanyDto company, IReadOnlyList<string> targetStack, List<string> positive, List<string> negative)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(company.CareerUrl)) score += 3;
        if (!string.IsNullOrWhiteSpace(company.LinkedinUrl)) score += 2;
        if (!string.IsNullOrWhiteSpace(company.Website)) score += 2;
        if (targetStack.Any(value => !string.IsNullOrWhiteSpace(value))) score += 2;
        if (!string.IsNullOrWhiteSpace(company.Notes)) score += 1;

        if (company.Incomplete)
        {
            negative.Add("Entreprise créée depuis une offre et encore incomplète.");
        }
        else if (score >= 8)
        {
            positive.Add("Données actionnables pour candidater ou suivre l'entreprise.");
        }
        else
        {
            negative.Add("Données à compléter pour faciliter le suivi.");
        }

        return Math.Min(score, 10);
    }

    private static int ScoreRemoteOrLocation(string? remotePolicy, string? location, int maxPoints, List<string> positive, List<string> negative)
    {
        if (IsRemoteFriendly(remotePolicy))
        {
            positive.Add($"Organisation compatible : {remotePolicy}.");
            return maxPoints;
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            positive.Add($"Localisation précisée : {location}.");
            return (int)Math.Round(maxPoints * 0.6);
        }

        negative.Add("Remote/localisation non précisés.");
        return 0;
    }

    private static int ScoreSeniority(string detectedSeniority, string? targetSeniority, int maxPoints, List<string> positive, List<string> negative)
    {
        var value = ScoreSeniorityValue(detectedSeniority, targetSeniority);
        var score = (int)Math.Round(maxPoints * value / 10.0);

        if (score >= maxPoints * 0.7)
        {
            positive.Add("Séniorité cohérente.");
        }
        else
        {
            negative.Add("Séniorité moins alignée ou non renseignée.");
        }

        return score;
    }

    private static int ScoreSeniorityValue(string detectedSeniority, string? targetSeniority)
    {
        if (string.IsNullOrWhiteSpace(targetSeniority))
        {
            return 4;
        }

        var candidateRank = SeniorityRank(detectedSeniority);
        var targetRank = SeniorityRank(targetSeniority);
        if (candidateRank == 0 || targetRank == 0)
        {
            return 4;
        }

        if (candidateRank >= targetRank)
        {
            return 10;
        }

        return candidateRank + 1 == targetRank ? 6 : 2;
    }

    private static bool DomainsAreRelated(string normalizedCandidateDomain, string companyDomain)
    {
        var normalizedCompanyDomain = RadarText.NormalizeSearch(companyDomain);
        var candidateGroup = DomainGroup(normalizedCandidateDomain);
        var companyGroup = DomainGroup(normalizedCompanyDomain);
        return candidateGroup.Length > 0 && candidateGroup == companyGroup;
    }

    private static string DomainGroup(string normalizedDomain)
    {
        if (ContainsAny(normalizedDomain, "banque", "assurance", "finance", "fintech", "assurtech")) return "finance";
        if (ContainsAny(normalizedDomain, "sante", "biotech", "pharma", "medical")) return "sante";
        if (ContainsAny(normalizedDomain, "industrie", "energie", "iot", "ot", "automatisation", "smart grid")) return "industrie";
        if (ContainsAny(normalizedDomain, "saas", "software", "logiciel", "editeur", "digital", "cloud", "web")) return "software";
        if (ContainsAny(normalizedDomain, "cyber", "securite", "security")) return "cyber";
        return "";
    }

    private static int ScoreSalary(JobDto job, List<string> positive, List<string> negative)
    {
        if (job.SalaryMin is not null || job.SalaryMax is not null)
        {
            positive.Add("Salaire renseigné.");
            return 5;
        }

        negative.Add("Salaire non renseigné.");
        return 0;
    }

    private static bool IsRemoteFriendly(string? remotePolicy)
    {
        var normalized = RadarText.NormalizeSearch(remotePolicy);
        return ContainsAny(normalized, "remote", "teletravail", "hybride", "hybrid");
    }

    private static int SeniorityRank(string? value)
    {
        var normalized = RadarText.NormalizeSearch(value);
        if (normalized.Contains("lead", StringComparison.Ordinal)) return 4;
        if (normalized.Contains("senior", StringComparison.Ordinal)) return 3;
        if (normalized.Contains("confirme", StringComparison.Ordinal) || normalized.Contains("intermediaire", StringComparison.Ordinal)) return 2;
        if (normalized.Contains("junior", StringComparison.Ordinal)) return 1;
        return 0;
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => value.Contains(RadarText.NormalizeSearch(candidate), StringComparison.Ordinal));
    }

    private static string NormalizeSkill(string value)
    {
        var normalized = RadarText.NormalizeSearch(value)
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal);

        return normalized switch
        {
            "dotnet" or ".netcore" or "netcore" => ".net",
            "aspnetcore" or "asp.netcore" => "asp.netcore",
            "mssql" => "sqlserver",
            "restapi" or "apirest" => "restapi",
            "nodejs" => "node.js",
            "cicd" or "ci/cd" => "ci/cd",
            _ => normalized
        };
    }

    private static async Task UpsertCompanyScoreAsync(SqliteConnection connection, SqliteTransaction transaction, int companyId, ScoreDto score)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO company_scores
                (company_id, global_score, stack_score, role_score, domain_score, seniority_score, location_score, salary_score,
                 strategic_score, positive_reasons, negative_reasons, missing_skills, updated_at)
            VALUES
                ($id, $global, $stack, $role, $domain, $seniority, $location, $salary, $strategic, $positive, $negative, $missing, $now)
            ON CONFLICT(company_id) DO UPDATE SET
                global_score = excluded.global_score,
                stack_score = excluded.stack_score,
                role_score = excluded.role_score,
                domain_score = excluded.domain_score,
                seniority_score = excluded.seniority_score,
                location_score = excluded.location_score,
                salary_score = excluded.salary_score,
                strategic_score = excluded.strategic_score,
                positive_reasons = excluded.positive_reasons,
                negative_reasons = excluded.negative_reasons,
                missing_skills = excluded.missing_skills,
                updated_at = excluded.updated_at;
            """;
        AddScoreParameters(command, "$id", companyId, score);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpsertJobScoreAsync(SqliteConnection connection, SqliteTransaction transaction, int jobId, ScoreDto score)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO job_scores
                (job_id, global_score, stack_score, role_score, domain_score, seniority_score, location_score, salary_score,
                 strategic_score, positive_reasons, negative_reasons, missing_skills, updated_at)
            VALUES
                ($id, $global, $stack, $role, $domain, $seniority, $location, $salary, $strategic, $positive, $negative, $missing, $now)
            ON CONFLICT(job_id) DO UPDATE SET
                global_score = excluded.global_score,
                stack_score = excluded.stack_score,
                role_score = excluded.role_score,
                domain_score = excluded.domain_score,
                seniority_score = excluded.seniority_score,
                location_score = excluded.location_score,
                salary_score = excluded.salary_score,
                strategic_score = excluded.strategic_score,
                positive_reasons = excluded.positive_reasons,
                negative_reasons = excluded.negative_reasons,
                missing_skills = excluded.missing_skills,
                updated_at = excluded.updated_at;
            """;
        AddScoreParameters(command, "$id", jobId, score);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddScoreParameters(SqliteCommand command, string idParameter, int id, ScoreDto score)
    {
        command.Parameters.AddWithValue(idParameter, id);
        command.Parameters.AddWithValue("$global", score.GlobalScore);
        command.Parameters.AddWithValue("$stack", score.StackScore);
        command.Parameters.AddWithValue("$role", score.RoleScore);
        command.Parameters.AddWithValue("$domain", score.DomainScore);
        command.Parameters.AddWithValue("$seniority", score.SeniorityScore);
        command.Parameters.AddWithValue("$location", score.LocationScore);
        command.Parameters.AddWithValue("$salary", score.SalaryScore);
        command.Parameters.AddWithValue("$strategic", score.StrategicScore);
        command.Parameters.AddWithValue("$positive", RadarText.JoinList(score.PositiveReasons));
        command.Parameters.AddWithValue("$negative", RadarText.JoinList(score.NegativeReasons));
        command.Parameters.AddWithValue("$missing", RadarText.JoinList(score.MissingSkills));
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    }

    private sealed record StackScoreResult(int Score, IReadOnlyList<string> MissingSkills);
}
