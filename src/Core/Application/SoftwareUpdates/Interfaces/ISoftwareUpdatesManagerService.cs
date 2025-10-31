using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys;
using Microsoft.AspNetCore.Http;

namespace Application.SoftwareUpdates.Interfaces;

public interface ISoftwareUpdatesManagerService
{
    Task<Result<PaginatedResponse<SoftwareUpdateFilesEntity>>> List(int pageNumber, int pageSize);
    Task<Result<string>> Create(string data);
    Task<Result<bool>> Delete(string id);
    Task<Result<bool>> AttachFile(string id, IFormFile file);
    Task<(bool, string)> NeedUpdate(string os, string architecture, int version, int assembly);
    Task<Result<Stream>> FmuApiUpdateData(string os, string architecture, int version, int assembly);
    Task<Result<Stream>> FmuApiUpdateFile(string id);
}