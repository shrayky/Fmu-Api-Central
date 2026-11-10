namespace EtrnSaby.Tests;

public class EtrnSignatureContentTests
{
    [Fact]
    public void Хеш_Saby_подписывается_алгоритмом_ГОСТ_2012_256()
    {
        Assert.Equal(EtrnSignatureContent.Gost2012Hash256, EtrnSignatureContent.ContentTypeOid(preparedHash: true));
    }

    [Fact]
    public void Байты_титула_подписываются_как_содержимое()
    {
        Assert.Equal("1.2.840.113549.1.7.1", EtrnSignatureContent.ContentTypeOid(preparedHash: false));
    }
}
