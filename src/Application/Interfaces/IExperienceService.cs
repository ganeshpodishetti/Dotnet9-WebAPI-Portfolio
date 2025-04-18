using Application.DTOs.Experience;
using Domain.Common.ResultPattern;

namespace Application.Interfaces;

public interface IExperienceService
{
    Task<Result<IEnumerable<ExperienceResponseDto>>> GetExperiencesByUserIdAsync();
    Task<Result<bool>> AddExperienceAsync(ExperienceRequestDto experienceRequestDto, string accessToken);
    Task<Result<bool>> UpdateExperienceAsync(ExperienceRequestDto experienceRequestDto, Guid id, string accessToken);
    Task<Result<bool>> DeleteExperienceAsync(Guid id, string accessToken);
}