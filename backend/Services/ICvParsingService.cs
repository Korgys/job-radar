using JobRadarLocal.Dtos;

namespace JobRadarLocal.Services;

public interface ICvParsingService
{
    Task<CandidateProfileDto> ImportAsync(string fileName, Stream stream);
    Task<CandidateProfileDto> UpdateLatestProfileAsync(UpdateCandidateProfileRequest request);
    ParsedCv ParseText(string rawText);
}
