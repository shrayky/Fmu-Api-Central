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
                RefreshTokenLifetimeDays = 30
            };

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettngs.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettngs.Audience,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettngs.Key)),
                    ValidateIssuerSigningKey = true
                };
                options.SaveToken = true;
            });

            services.AddSingleton(jwtSettngs);
            services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);

            return services;
        }

        private static string SigningKey()
        {
            string configFolder = Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name);

            string keyFile = Path.Combine(configFolder, "jwt.key");

            if (!Directory.Exists(configFolder))
                Directory.CreateDirectory(configFolder);

            if (!File.Exists(keyFile))
            {
                var keyBytes = new byte[64];
                using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
                rng.GetBytes(keyBytes);
                var key = Convert.ToBase64String(keyBytes);

                File.WriteAllText(keyFile, key);
                return key;
            }

            return File.ReadAllText(keyFile);
        }
    }
}
