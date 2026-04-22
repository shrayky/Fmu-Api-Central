using Domain.Logs.Dto;

namespace Domain.Logs.Interfaces
{
    public interface ILogCollectorService
    {
        Task<LogPacket> Collect();
        Task<LogPacket> Collect(string selectedFileName);
    }
}
