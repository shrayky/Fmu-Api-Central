using Domain.Attributes;
using Domain.Authentication;
using Domain.Configuration.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.FilesFolders;
using System.Reflection;
using System.Text;

namespace Authentication
{
    public static class AuthenticationExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
        {
            var jwtSettngs = new JwtSettings
            {
                Issuer = ApplicationInformation.Name,
                Audience = "api",
                Key = SigningKey(),
                LifetimeMinutes = 60,
                AgentLifetimeMinutes = 15,
                RefreshTokenLifetimeDays = 30
            };

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = CreateTokenValidationParameters(jwtSettngs);
                options.SaveToken = true;
            });

            services.AddSingleton(jwtSettngs);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(new LoginAttemptOptions());
            services.AddSingleton(new AgentNonceOptions());
            services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);

            return services;
        }

        private static string SigningKey()
        {
            var configFolder = Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name);

            var keyFile = Path.Combine(configFolder, "jwt.key");

            if (!Directory.Exists(configFolder))
                Directory.CreateDirectory(configFolder);

            if (File.Exists(keyFile))
            {
                JwtKeyFileAccess.RestrictToCurrentUserAndSystem(keyFile);
                return File.ReadAllText(keyFile);
            }
            
            var keyBytes = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(keyBytes);
            var key = Convert.ToBase64String(keyBytes);

            File.WriteAllText(keyFile, key);
            JwtKeyFileAccess.RestrictToCurrentUserAndSystem(keyFile);
            return key;

        }

        public static TokenValidationParameters CreateTokenValidationParameters(JwtSettings settings)
            => new()
            {
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                ValidateIssuerSigningKey = true
            };
    }
}
