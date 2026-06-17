using System.Globalization;
using System.Text.RegularExpressions;
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
        var jobScores = jobs.ToDictionary(job => job.Id, job => CalculateJobScore(profile, job));
        var jobsByCompany = jobs
            .Select(job => job with { Score = jobScores[job.Id] })
            .GroupBy(job => job.CompanyId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var job in jobs)
        {
            await UpsertJobScoreAsync(connection, transaction, job.Id, jobScores[job.Id]);
        }

        foreach (var company in companies)
        {
            jobsByCompany.TryGetValue(company.Id, out var companyJobs);
            var score = CalculateCompanyScore(profile, company, companyJobs ?? Array.Empty<JobDto>());
            await UpsertCompanyScoreAsync(connection, transaction, company.Id, score);
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
        var domainScore = ScoreCompanyDomain(profile.DetectedDomains, company, positive, negative);
        var strategicScore = ScoreCompanyStrategic(profile, company, jobs, positive, negative);
        var companyScore = stackResult.Score + domainScore + strategicScore;
        var bestJobScore = jobs.Select(job => job.Score?.GlobalScore ?? 0).DefaultIfEmpty(0).Max();
        var global = Math.Max(companyScore, bestJobScore);

        if (bestJobScore > companyScore)
        {
            positive.Add($"Score relevé par la meilleure offre liée : {bestJobScore}/100.");
        }

        return new ScoreDto(
            Math.Clamp(global, 0, 100),
            stackResult.Score,
            0,
            domainScore,
            0,
            0,
            0,
            strategicScore,
            positive,
            negative,
            stackResult.MissingSkills);
    }

    public ScoreDto CalculateJobScore(CandidateProfileDto profile, JobDto job)
    {
        var positive = new List<string>();
        var negative = new List<string>();
        var stackResult = ScoreStack(profile.DetectedSkills, job.Stack, 35, positive, negative);
        var seniorityScore = ScoreJobSeniority(profile.DetectedSeniority, job, 25, positive, negative);
        var roleScore = ScoreJobRole(profile.DetectedRoles, job, 15, positive, negative);
        var domainScore = ScoreDomain(profile.DetectedDomains, job.CompanyDomain, 10, positive, negative);
        var locationScore = ScoreJobLocation(profile, job, positive, negative);
        var salaryScore = ScoreJobSalary(profile, job, positive, negative);
        var strategicScore = ScoreJobStrategic(job, positive, negative);
        var global = stackResult.Score + seniorityScore + roleScore + domainScore + locationScore + salaryScore + strategicScore;

        return new ScoreDto(
            Math.Clamp(global, 0, 100),
            stackResult.Score,
            roleScore,
            domainScore,
            seniorityScore,
            locationScore,
            salaryScore,
            strategicScore,
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
        var score = (int)Math.Round(60 * Math.Min(matching.Length, expectedMatches) / (double)expectedMatches);

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
            return 25;
        }

        var relatedMatch = companyDomains.FirstOrDefault(domain => normalizedCandidateDomains.Any(candidate => DomainsAreRelated(candidate, domain)));
        if (relatedMatch is not null)
        {
            positive.Add($"Domaine proche du profil : {relatedMatch}.");
            return 18;
        }

        negative.Add($"Domaine moins aligné : {company.Domain}.");
        return 0;
    }

    private static int ScoreJobLocation(CandidateProfileDto profile, JobDto job, List<string> positive, List<string> negative)
    {
        var remote = RadarText.NormalizeSearch(job.RemotePolicy);
        var preference = RadarText.NormalizeSearch(profile.RemotePreference);
        var locations = profile.PreferredLocations.Select(RadarText.NormalizeSearch).Where(v => v.Length > 0).ToArray();
        var jobLocation = RadarText.NormalizeSearch(job.Location);

        if (preference.Contains("remote", StringComparison.Ordinal) || preference.Contains("teletravail", StringComparison.Ordinal))
        {
            if (ContainsAny(remote, "remote", "full remote", "teletravail", "hybride", "hybrid"))
            {
                positive.Add("Localisation compatible avec la préférence télétravail.");
                return 5;
            }
        }

        if (locations.Length > 0 && locations.Any(location => jobLocation.Contains(location, StringComparison.Ordinal)))
        {
            positive.Add($"Localisation alignée : {job.Location}.");
            return 5;
        }

        if (locations.Length == 0 && string.IsNullOrWhiteSpace(profile.RemotePreference))
        {
            negative.Add("Préférences de localisation non renseignées.");
            return 0;
        }

        negative.Add("Localisation moins alignée avec les préférences.");
        return 0;
    }

    private static int ScoreJobSalary(CandidateProfileDto profile, JobDto job, List<string> positive, List<string> negative)
    {
        if (profile.TargetSalary is null)
        {
            negative.Add("Salaire cible non renseigné.");
            return 0;
        }

        var bestSalary = job.SalaryMax ?? job.SalaryMin;
        if (bestSalary is null)
        {
            negative.Add("Salaire offre non renseigné.");
            return 0;
        }

        if (bestSalary >= profile.TargetSalary)
        {
            positive.Add($"Salaire compatible avec la cible de {profile.TargetSalary:0}.");
            return 5;
        }

        var ratio = bestSalary.Value / profile.TargetSalary.Value;
        if (ratio >= 0.9m)
        {
            positive.Add("Salaire proche de la cible.");
            return 3;
        }

        negative.Add("Salaire sous la cible.");
        return 0;
    }

    private static int ScoreJobStrategic(JobDto job, List<string> positive, List<string> negative)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(job.Url)) score += 2;
        if (!string.IsNullOrWhiteSpace(job.Description) && job.Description.Length >= 80) score += 2;
        if (job.PublicationDate is not null && job.PublicationDate >= DateTime.UtcNow.AddDays(-45)) score += 1;

        if (score > 0) positive.Add($"Signal stratégique offre : {score}/5.");
        else negative.Add("Peu de signaux stratégiques sur l'offre.");
        return score;
    }

    private static int ScoreCompanyStrategic(CandidateProfileDto profile, CompanyDto company, IReadOnlyList<JobDto> jobs, List<string> positive, List<string> negative)
    {
        var score = 0;
        if (profile.DetectedDomains.Any(domain => DomainsAreRelated(RadarText.NormalizeSearch(domain), company.Domain))) score += 5;
        if (!string.IsNullOrWhiteSpace(company.CareerUrl)) score += 4;
        if (!string.IsNullOrWhiteSpace(company.Notes)) score += 3;
        if (company.JobCount > 0 || jobs.Count > 0) score += 3;
        score = Math.Min(score, 15);

        if (score > 0) positive.Add($"Signaux stratégiques entreprise : {score}/15.");
        else negative.Add("Peu de signaux stratégiques entreprise.");
        return score;
    }

    private static int ScoreJobRole(IReadOnlyList<string> candidateRoles, JobDto job, int maxPoints, List<string> positive, List<string> negative)
    {
        var targetRole = DetectRoleCategory([job.Title, job.JobType ?? "", job.Description ?? ""]);
        if (targetRole.Length == 0)
        {
            negative.Add("Type de poste non renseigné.");
            return 0;
        }

        var candidateRoleCategories = candidateRoles
            .Select(role => DetectRoleCategory([role]))
            .Where(role => role.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (candidateRoleCategories.Contains(targetRole, StringComparer.Ordinal))
        {
            positive.Add($"Rôle aligné : {RoleLabel(targetRole)}.");
            return maxPoints;
        }

        var partialRole = candidateRoleCategories.FirstOrDefault(candidateRole => RolesAreAdjacent(candidateRole, targetRole));
        if (partialRole is not null)
        {
            positive.Add($"Rôle partiellement aligné : {RoleLabel(partialRole)} vers {RoleLabel(targetRole)}.");
            return maxPoints / 2;
        }

        negative.Add($"Rôle moins aligné : {RoleLabel(targetRole)}.");
        return 0;
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

    private static int ScoreJobSeniority(string detectedSeniority, JobDto job, int maxPoints, List<string> positive, List<string> negative)
    {
        var targetRank = SeniorityRank(job.Seniority);
        var inferred = false;
        if (targetRank == 0)
        {
            targetRank = SeniorityRank($"{job.Title} {job.Description}");
        }

        if (targetRank == 0)
        {
            targetRank = 2;
            inferred = true;
        }

        var candidateRank = SeniorityRank(detectedSeniority);
        var score = ScoreSeniorityRank(candidateRank, targetRank, maxPoints);

        if (score == maxPoints)
        {
            positive.Add(inferred ? "Expérience cohérente avec l'hypothèse confirmé / 3-4 ans." : "Expérience cohérente.");
        }
        else if (score >= 10)
        {
            negative.Add(inferred ? "Expérience partiellement alignée avec l'hypothèse confirmé / 3-4 ans." : "Expérience partiellement alignée.");
        }
        else
        {
            negative.Add("Expérience non compatible avec le niveau attendu.");
        }

        return score;
    }

    private static int ScoreSeniorityRank(int candidateRank, int targetRank, int maxPoints)
    {
        if (candidateRank == 0)
        {
            return 10;
        }

        var gap = targetRank - candidateRank;
        if (gap <= 0)
        {
            return maxPoints;
        }

        if (gap == 1)
        {
            return 20;
        }

        if (gap == 2)
        {
            return 10;
        }

        return 0;
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

    private static int SeniorityRank(string? value)
    {
        var normalized = RadarText.NormalizeSearch(value);
        var years = Regex.Matches(normalized, @"(\d{1,2})\s*(ans|annees|annee|years|year)")
            .Select(match => int.TryParse(match.Groups[1].Value, out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max();

        if (normalized.Contains("lead", StringComparison.Ordinal)) return 4;
        if (normalized.Contains("senior", StringComparison.Ordinal) || years >= 8) return 3;
        if (normalized.Contains("confirme", StringComparison.Ordinal)
            || normalized.Contains("confirmed", StringComparison.Ordinal)
            || normalized.Contains("intermediaire", StringComparison.Ordinal)
            || normalized.Contains("intermediate", StringComparison.Ordinal)
            || normalized.Contains("middle", StringComparison.Ordinal)
            || normalized.Contains("mid", StringComparison.Ordinal)
            || years >= 3) return 2;
        if (normalized.Contains("junior", StringComparison.Ordinal)
            || normalized.Contains("debutant", StringComparison.Ordinal)
            || normalized.Contains("entry", StringComparison.Ordinal)
            || years > 0) return 1;
        return 0;
    }

    private static string DetectRoleCategory(IEnumerable<string> texts)
    {
        var normalized = RadarText.NormalizeSearch(string.Join(" ", texts));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "";
        }

        if (ContainsAny(normalized, "devops", "ci/cd", "kubernetes", "docker", "sre")) return "devops";
        if (ContainsAny(normalized, "cyber", "securite", "security")) return "security";
        if (ContainsAny(normalized, "data engineer", "etl", "pipeline")) return "data";
        if (ContainsAny(normalized, "analytics", "bi", "dbt")) return "analytics";
        if (ContainsAny(normalized, "tech lead", "lead developer", "lead dev", "architecte", "lead")) return "lead";
        if (ContainsAny(normalized, "fullstack", "full stack")) return "fullstack";
        if (ContainsAny(normalized, "backend", "back-end", "back end", "api", "asp.net", ".net", "c#", "software engineer")) return "backend";
        if (ContainsAny(normalized, "frontend", "front-end", "front end", "react", "angular", "typescript", "javascript")) return "frontend";
        return "";
    }

    private static bool RolesAreAdjacent(string candidateRole, string targetRole)
    {
        return (candidateRole is "backend" or "frontend" && targetRole == "fullstack")
            || (candidateRole == "fullstack" && targetRole is "backend" or "frontend");
    }

    private static string RoleLabel(string role)
    {
        return role switch
        {
            "backend" => "backend",
            "frontend" => "frontend",
            "fullstack" => "fullstack",
            "devops" => "devops",
            "security" => "cybersécurité",
            "data" => "data",
            "analytics" => "analytics",
            "lead" => "lead",
            _ => "non renseigné"
        };
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
