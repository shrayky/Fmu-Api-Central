using Application.Configuration.DTO;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;

namespace Application.Configuration.Interfaces
{
    public interface IConfigurationApplicationService
    {
        Task<string> Current();
        Task<Result> Update(string jsonConfiguration);
        object AppInformation();

        Task<Result<PortableSettingsFile>> ExportPortable(CancellationToken cancellationToken);

        Task<Result> ImportPortable(IFormFile file, CancellationToken cancellationToken);
    }
}
