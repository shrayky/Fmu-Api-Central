namespace CouchDb
{
    public static class DatabaseNames
    {
        public const string Users = "fmu-api-central-users";
        public const string Instance = "fmu-api-central-instance";
        public const string  SoftwareUpdateFiles = "fmu-api-central-software-update-files";
        public static string[] All() => [Users,  Instance,  SoftwareUpdateFiles];
    }
}
