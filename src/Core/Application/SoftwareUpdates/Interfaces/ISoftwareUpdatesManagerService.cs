using CSharpFunctionalExtensions;
using Domain.Dto.Interfaces;
using Domain.Dto.Responces;
using Microsoft.AspNetCore.Http;

namespace Application.SoftwareUpdates.Interfaces;

public interface ISoftwareUpdatesManagerService
{
    Task<Result<PaginatedResponse<SoftwareUpdateFilesEntity>>> List(int pageNumber, int pageSize);
    Task<Result<string>> Create(string data);
    Task<Result<bool>> Delete(string id);
    Task<Result<bool>> AttachFile(string id, IFormFile file);
    Task<bool> NeedUpdate(string os, string architecture, int version, int assembly);
}