using JobRadarLocal.Services;
using Xunit;

namespace JobRadarLocal.Tests;

public sealed class CvParsingServiceTests
{
    [Fact]
    public void ParseText_DetectsSkillsRolesDomainsAndSeniority()
    {
        var parser = new CvParsingService(null!);
        var parsed = parser.ParseText("""
            Tech lead C# .NET ASP.NET Core SQL Server Azure DevOps Git CI/CD Docker React TypeScript.
            10 ans d'expérience en banque, industrie et SaaS.
            """);

        Assert.Contains("C#", parsed.Skills);
        Assert.Contains(".NET", parsed.Skills);
        Assert.Contains("SQL Server", parsed.Skills);
        Assert.Contains("tech lead", parsed.Roles);
        Assert.Contains("banque", parsed.Domains);
        Assert.Equal("lead", parsed.Seniority);
    }
}
