namespace Application.Logs.DTO
{
    public record LogsPacket
    {
        public List<string> LogFilesNames { get; set; } = [];
        public string SelectedLogFileName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
