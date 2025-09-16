using CSharpFunctionalExtensions;
using Domain.Dto.Interfaces;
using Domain.Dto.Responces;
using Microsoft.AspNetCore.Http;

namespace Application.SoftwareUpdates.Interfaces;

public interface ISoftwareUpdatesManagerService
{
    Task<Result<PaginatedResponse<SoftwareUpdateFilesEntity>>> List(int pageNumber, int pageSize);
    Task<Result<SoftwareUpdateFilesEntity>> ById(string id);
    Task<Result<string>> Create(string data);
    Task<Result<SoftwareUpdateFilesEntity>> Update(SoftwareUpdateFilesEntity entity);
    Task<Result<bool>> Delete(string id);
    Task<Result<bool>> AttachFile(string id, IFormFile file);
}