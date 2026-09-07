namespace Shared.Files;

public static class ZipSignature
{
    public static bool Matches(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4)
            return false;

        if (header[0] != 0x50 || header[1] != 0x4B)
            return false;

        return header[2] == 0x03 && header[3] == 0x04
               || header[2] == 0x05 && header[3] == 0x06
               || header[2] == 0x07 && header[3] == 0x08;
    }
}
