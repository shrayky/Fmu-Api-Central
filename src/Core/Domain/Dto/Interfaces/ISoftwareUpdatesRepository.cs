using CSharpFunctionalExtensions;
using Domain.Dto.Responces;

namespace Domain.Dto.Interfaces;

public interface ISoftwareUpdatesRepository
{
    Task<Result<PaginatedResponse<SoftwareUpdateFilesEntity>>> List(int  pageNumber, int pageSize);
    Task<Result<SoftwareUpdateFilesEntity>> Create(SoftwareUpdateFilesEntity entity);
    Task<Result<bool>> Delete(string id);
    Task<Result<bool>> AttachFile(string entityId, string filePath, string contentType);
    Task<Result<SoftwareUpdateFilesEntity>> ById(string id);
    Task<Result<SoftwareUpdateFilesEntity>> MaxUpdateEntity(string os, string architecture, int version, int assembly);
    Task<Result<Stream>> FmuApiUpdate(string updateId);
}