using Shared.Strings;

namespace Application.Tests;

public class InstanceHmacTests
{
    /// <summary>
    /// Подпись сходится только с тем же ключом, токеном, временем и nonce.
    /// </summary>
    [Fact]
    public void Verify_принимает_подпись_тем_же_ключом()
    {
        const string secret = "node-secret";
        const string token = "instance-1";
        const long timestamp = 1757251200;
        const string nonce = "abc123";

        var signature = InstanceHmac.Sign(secret, token, timestamp, nonce);

        Assert.True(InstanceHmac.Verify(secret, token, timestamp, nonce, signature));
        Assert.False(InstanceHmac.Verify("other", token, timestamp, nonce, signature));
        Assert.False(InstanceHmac.Verify(secret, token, timestamp + 1, nonce, signature));
        Assert.False(InstanceHmac.Verify(secret, token, timestamp, "other", signature));
    }

    /// <summary>
    /// Пустой ключ нельзя подписать — handshake без SecretKey невозможен.
    /// </summary>
    [Fact]
    public void Sign_без_ключа_бросает()
    {
        Assert.Throws<ArgumentException>(() => InstanceHmac.Sign("", "t", 1, "n"));
    }
}
