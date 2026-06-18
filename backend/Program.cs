using JobRadarLocal.Data;
using JobRadarLocal.Dtos;
using JobRadarLocal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://127.0.0.1:5087");

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.WithOrigins("http://127.0.0.1:5173", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<AppPaths>();
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<RadarQueryService>();
builder.Services.AddSingleton<CsvImportService>();
builder.Services.AddSingleton<ICvParsingService, CvParsingService>();
builder.Services.AddSingleton<ScoringService>();

var app = builder.Build();

app.Services.GetRequiredService<DatabaseInitializer>().Initialize();

app.UseCors("frontend");

app.MapGet("/", () => Results.Redirect("/api/dashboard"));

app.MapGet("/api/dashboard", async (RadarQueryService queries) =>
{
    return Results.Ok(await queries.GetStatsAsync());
});

app.MapGet("/api/companies", async (RadarQueryService queries) =>
{
    return Results.Ok(await queries.GetCompaniesAsync());
});

app.MapPost("/api/companies/import-csv", async (HttpRequest request, CsvImportService imports) =>
{
    var file = await ReadUploadedFileAsync(request);
    if (file is null)
    {
        return Results.BadRequest(new { error = "Ajoutez un fichier CSV dans le champ 'file'." });
    }

    await using var stream = file.OpenReadStream();
    return Results.Ok(await imports.ImportCompaniesAsync(stream));
});

app.MapGet("/api/jobs", async (RadarQueryService queries) =>
{
    return Results.Ok(await queries.GetJobsAsync());
});

app.MapPost("/api/jobs/import-csv", async (HttpRequest request, CsvImportService imports) =>
{
    var file = await ReadUploadedFileAsync(request);
    if (file is null)
    {
        return Results.BadRequest(new { error = "Ajoutez un fichier CSV dans le champ 'file'." });
    }

    await using var stream = file.OpenReadStream();
    return Results.Ok(await imports.ImportJobsAsync(stream));
});

app.MapGet("/api/profile", async (RadarQueryService queries) =>
{
    var profile = await queries.GetLatestProfileAsync();
    return profile is null ? Results.NotFound(new { error = "Aucun CV importe." }) : Results.Ok(profile);
});

app.MapPost("/api/profile/import-cv", async (HttpRequest request, ICvParsingService parser) =>
{
    var file = await ReadUploadedFileAsync(request);
    if (file is null)
    {
        return Results.BadRequest(new { error = "Ajoutez un fichier CV dans le champ 'file'." });
    }

    try
    {
        await using var stream = file.OpenReadStream();
        return Results.Ok(await parser.ImportAsync(file.FileName, stream));
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPut("/api/profile", async (UpdateCandidateProfileRequest request, ICvParsingService parser) =>
{
    try
    {
        return Results.Ok(await parser.UpdateLatestProfileAsync(request));
    }
    catch (InvalidOperationException exception) when (exception.Message.Contains("Aucun CV", StringComparison.OrdinalIgnoreCase))
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapPost("/api/scoring/recalculate", async (ScoringService scoring) =>
{
    try
    {
        return Results.Ok(await scoring.RecalculateAsync());
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.Run();

static async Task<IFormFile?> ReadUploadedFileAsync(HttpRequest request)
{
    if (!request.HasFormContentType)
    {
        return null;
    }

    var form = await request.ReadFormAsync();
    return form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
}
