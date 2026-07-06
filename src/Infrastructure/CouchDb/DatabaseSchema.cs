namespace CouchDb
{
    public static class DatabaseSchema
    {
        public const string Users = "fmu-api-central-users";
        public const string Instance = "fmu-api-central-instance";
        public const string SoftwareUpdateFiles = "fmu-api-central-software-update-files";
        public const string MarkCheckingStatistic = "fmu-api-central-mark-checking-statistic";

        public static string[] All() => [Users,  Instance,  SoftwareUpdateFiles, MarkCheckingStatistic];
        
        public static Dictionary<string, object[]> DatabaseIndexSchema()
        {
            return new Dictionary<string, object[]>
            {
                { Instance, InstanceIndexSchema() },
                { SoftwareUpdateFiles, SoftwareUpdateFilesSchema() }
            };
        }

        private static object[] InstanceIndexSchema() =>
        [
            new { name = "name-idx", index = new { fields = new[] { "data.name" } } },
            new { name = "updatedAt-idx", index = new { fields = new[] { "data.updatedAt" } } }
        ];

        private static object[] SoftwareUpdateFilesSchema() =>
        [
            new { name = "updatedAt-idx", index = new { fields = new[] { "data.updatedAt" } } },
            new {
                name = "max-update-by-version-assemly-idx", index = new
                {
                    fields = new[]
                    {
                        "data.os",
                        "data.architecture",
                        "data.version",
                        "data.assembly"
                    }
                }
            },
            new {
                name = "max-update-by-version-idx", index = new
                {
                    fields = new[]
                    {
                        "data.os",
                        "data.architecture",
                        "data.version",
                    }
                }
            },
        ];
    }
}
