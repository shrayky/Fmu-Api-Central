using Authentication.Services;

namespace Application.Tests;

public class BcryptPasswordHasherTests
{
    /// <summary>
    /// Хеш не совпадает с исходным паролем.
    /// </summary>
    [Fact]
    public void Hash_не_хранит_пароль_открытым_текстом()
    {
        var sut = new BcryptPasswordHasher();

        var hash = sut.Hash("secret1");

        Assert.NotEqual("secret1", hash);
        Assert.True(sut.IsHashed(hash));
    }

    /// <summary>
    /// Проверка принимает верный пароль и отклоняет неверный.
    /// </summary>
    [Fact]
    public void Verify_сверяет_хеш()
    {
        var sut = new BcryptPasswordHasher();
        var hash = sut.Hash("secret1");

        Assert.True(sut.Verify("secret1", hash));
        Assert.False(sut.Verify("other", hash));
    }

    /// <summary>
    /// Старые записи с открытым паролем ещё проходят проверку.
    /// </summary>
    [Fact]
    public void Verify_принимает_открытый_пароль()
    {
        var sut = new BcryptPasswordHasher();

        Assert.True(sut.Verify("admin", "admin"));
        Assert.False(sut.IsHashed("admin"));
    }
}
