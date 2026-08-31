using CryptoPro.Security.Cryptography.X509Certificates;
using Domain.Attributes;
using Domain.TrueApiIntegration;
using Domain.TrueApiIntegration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography.X509Certificates;

namespace TrueApiIntegration.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class DigitalSignatureService : IDigitalSignatureService
{
    public List<DigitalSignature> List()
    {
        var answer = new List<DigitalSignature>();

        try
        {
            var store = new CpX509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            foreach (var certificate in store.Certificates)
            {
                if (certificate.NotAfter <= DateTime.Now)
                    continue;

                if (certificate.Issuer.Contains("DO_NOT_TRUST"))
                    continue;

                var signature = new DigitalSignature
                {
                    Presentation = certificate.Subject,
                    WorkUntil = certificate.NotAfter,
                    Number = certificate.GetSerialNumberString()
                };

                var subjectLines = certificate.Subject.Split(",");
                var lineWithInn = subjectLines.FirstOrDefault(l => l.Contains("ИНН ЮЛ"));
                lineWithInn ??= subjectLines.FirstOrDefault(l => l.Contains("ИНН"));

                if (lineWithInn != null)
                {
                    var parts = lineWithInn.Split("=");
                    if (parts.Length >= 2)
                        signature.Inn = parts[1];
                }

                answer.Add(signature);
            }

            store.Close();
        }
        catch (Exception)
        {
            return [];
        }

        return answer;
    }
}
