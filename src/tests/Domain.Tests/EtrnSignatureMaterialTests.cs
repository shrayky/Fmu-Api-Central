using Domain.Etrn;

namespace Domain.Tests;

public class EtrnSignatureMaterialTests
{
    [Fact]
    public void Байты_титула_важнее_хеша_и_ссылки()
    {
        var request = new EtrnSignatureRequest
        {
            Content = [1, 2, 3],
            Hash = Convert.ToBase64String([9, 9]),
            FileUrl = "https://online.sbis.ru/file"
        };

        var material = EtrnSignatureMaterial.Resolve(request);

        Assert.True(material.IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3 }, material.Value.Bytes);
        Assert.Equal("", material.Value.FileUrl);
    }

    [Fact]
    public void Пустые_байты_оставляют_хеш_Saby()
    {
        var request = new EtrnSignatureRequest
        {
            Content = [],
            Hash = "  " + Convert.ToBase64String([4, 5]) + "  "
        };

        var material = EtrnSignatureMaterial.Resolve(request);

        Assert.True(material.IsSuccess);
        Assert.Equal(new byte[] { 4, 5 }, material.Value.Bytes);
    }

    [Fact]
    public void Без_хеша_остаётся_ссылка_на_файл()
    {
        var request = new EtrnSignatureRequest { FileUrl = " https://online.sbis.ru/file " };

        var material = EtrnSignatureMaterial.Resolve(request);

        Assert.True(material.IsSuccess);
        Assert.Null(material.Value.Bytes);
        Assert.Equal("https://online.sbis.ru/file", material.Value.FileUrl);
    }

    [Fact]
    public void Без_байтов_хеша_и_ссылки_подписывать_нечего()
    {
        var material = EtrnSignatureMaterial.Resolve(new EtrnSignatureRequest());

        Assert.True(material.IsFailure);
        Assert.Equal("Saby не вернула хеш или ссылку для подписи.", material.Error);
    }
}
