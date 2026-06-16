namespace JobRadarLocal.Dtos;

public sealed record ImportErrorDto(int Row, string Message);

public sealed record ImportResultDto(int Imported, int Updated, int Skipped, IReadOnlyList<ImportErrorDto> Errors)
{
    public static ImportResultDto Empty => new(0, 0, 0, Array.Empty<ImportErrorDto>());
}
