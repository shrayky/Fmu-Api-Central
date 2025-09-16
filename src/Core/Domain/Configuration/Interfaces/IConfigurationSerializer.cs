// Ignore Spelling: Serializer

using CSharpFunctionalExtensions;

namespace Domain.Configuration.Interfaces
{
    public interface IConfigurationSerializer
    {
        Task<Result<Parameters>> DeserializeAsync(string json);
        Task<Result<string>> SerializeAsync(Parameters parameters);
        bool IsValidConfiguration(string json);
    }
}
