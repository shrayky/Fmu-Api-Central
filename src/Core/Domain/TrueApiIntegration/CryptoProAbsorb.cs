namespace Domain.TrueApiIntegration;

public static class CryptoProAbsorb
{
    public static bool LinksContainer(string absorbOutput, string containerName)
    {
        return absorbOutput.Contains($@"HDIMAGE\{containerName}\", StringComparison.OrdinalIgnoreCase)
            || absorbOutput.Contains($@"HDIMAGE\\{containerName}\", StringComparison.OrdinalIgnoreCase)
            || absorbOutput.Contains($"HDIMAGE/{containerName}/", StringComparison.OrdinalIgnoreCase);
    }
}
