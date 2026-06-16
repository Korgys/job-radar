using JobRadarLocal.Dtos;

namespace JobRadarLocal.Services;

public interface ICvParsingService
{
    Task<CandidateProfileDto> ImportAsync(string fileName, Stream stream);
    ParsedCv ParseText(string rawText);
}
