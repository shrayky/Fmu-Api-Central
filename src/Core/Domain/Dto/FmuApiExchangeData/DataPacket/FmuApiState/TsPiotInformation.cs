namespace Domain.Dto.FmuApiExchangeData.DataPacket.FmuApiState;

public record TsPiotInformation
{
    public string Name { get; set; } = string.Empty;
    
    public string Address { get; set; } = string.Empty;
    
    public int ProtocolVersion { get; set; } = 0;
    
    public bool Online { get; set; } = false;
    
    public DateTime LastCheckTime { get; set; }
    
    public string Version { get; set; } = string.Empty;
    
    private DateTime? _licenseActiveTill;

    public DateTime? LicenseActiveTill
    {
        get => _licenseActiveTill;
        set
        {
            _licenseActiveTill = value;
            LicenseActiveTillTimeStamp = value.HasValue
                ? (int)new DateTimeOffset(value.Value).ToUnixTimeSeconds()
                : null;
        }
    }

    public int? LicenseActiveTillTimeStamp { get; set; }
}
