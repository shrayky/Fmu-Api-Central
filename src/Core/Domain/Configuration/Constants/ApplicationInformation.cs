namespace Domain.Configuration.Constants
{
    public static class ApplicationInformation
    {
        public const string Name = "FmuApiCentral";
        public const string Manufacture = "Automation";
        public const string Description = "Служба для мониторинга состояния установленных клиентов fmu-api";
        public const string ServiceName = "DS:Fmu-Api-Central";
        public const int Version = 1;
        public const int Assembly = 1;

        public static object Information() => new { Name, Version, Assembly, Description};
    }
}
