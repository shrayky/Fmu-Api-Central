namespace Domain.Logs.Dto
{
    public record LogPacket
    {
        public List<string> LogFileNames { get; set; } = [];
        public string SelectedLogFileName {  get; set; } = string.Empty;
        public string LogText {  get; set; } = string.Empty;
    }
}
