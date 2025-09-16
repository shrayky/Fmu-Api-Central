﻿namespace Domain.Authentication
{
    public class JwtSettings
    {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public int LifetimeMinutes { get; set; }
        public int RefreshTokenLifetimeDays { get; set; }
    }
}
