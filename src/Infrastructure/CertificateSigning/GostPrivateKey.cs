using System.Security;
using System.Security.Cryptography;
using CryptoPro.Security.Cryptography;
using CryptoPro.Security.Cryptography.X509Certificates;

namespace CertificateSigning;

public static class GostPrivateKey
{
    public static AsymmetricAlgorithm? Open(CpX509Certificate2 certificate, string? password)
    {
        var privateKey = certificate.GetGost3410_2012_256PrivateKey()
                         ?? certificate.GetGost3410_2012_512PrivateKey() as Gost3410Algorithm
                         ?? certificate.GetGost3410PrivateKey();
        if (privateKey == null || string.IsNullOrEmpty(password))
            return privateKey;

        using var secure = new SecureString();
        foreach (var character in password)
            secure.AppendChar(character);
        secure.MakeReadOnly();

        switch (privateKey)
        {
            case Gost3410_2012_256CryptoServiceProvider csp256:
                csp256.SetContainerPassword(secure);
                break;
            case Gost3410_2012_512CryptoServiceProvider csp512:
                csp512.SetContainerPassword(secure);
                break;
            case Gost3410CryptoServiceProvider csp3410:
                csp3410.SetContainerPassword(secure);
                break;
        }

        return privateKey;
    }
}
