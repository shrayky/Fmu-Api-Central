using Application.Logs.DTO;

namespace Application.Logs.Interfaces
{
    public interface ILogInfoService
    {
        Task<LogsPacket> Packet(string fileName);
    }
}
