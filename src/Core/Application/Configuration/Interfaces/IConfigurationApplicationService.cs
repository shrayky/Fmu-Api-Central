namespace Application.Configuration.Interfaces
{
    public interface IConfigurationApplicationService
    {
        Task<string> Current();
        Task<bool> Update(string jsonConfiguration);
        object AppInformation();
    }
}
