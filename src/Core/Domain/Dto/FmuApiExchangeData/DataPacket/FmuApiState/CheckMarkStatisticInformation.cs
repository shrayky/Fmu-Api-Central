using Domain.MarkInformation.Models;

namespace Domain.Dto.FmuApiExchangeData.DataPacket.FmuApiState;

public record CheckMarkStatisticInformation
{
    public long Date {  get; set; }
    public MarkCheckStatistics MarkCheckStatistics { get; set; } = new();
}
