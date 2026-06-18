namespace JobRadarLocal.Data;

public sealed class AppPaths
{
    public AppPaths(IHostEnvironment environment)
        : this(Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "data")))
    {
    }

    public AppPaths(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        SamplesDirectory = Path.Combine(dataDirectory, "samples");
        DatabasePath = Path.Combine(dataDirectory, "job-radar-local.db");
    }

    public string DataDirectory { get; }
    public string SamplesDirectory { get; }
    public string DatabasePath { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(SamplesDirectory);
    }
}
