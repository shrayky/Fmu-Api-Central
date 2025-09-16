using Domain.Configuration;

namespace Domain.Configuration.Interfaces
{
    public interface IParametersService
    {
        Task<Parameters> Current();
        Task<bool> Update(Parameters parameters);
    }
}
