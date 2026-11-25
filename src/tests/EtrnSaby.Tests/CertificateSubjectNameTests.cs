using CertificateSigning;
using CSharpFunctionalExtensions;

namespace EtrnSaby.Tests;

public class CertificateSubjectNameTests
{
    [Fact]
    public void Фамилия_из_SN_имя_и_отчество_из_G()
    {
        var read = CertificateSubjectName.Read(
            "CN=Большаков Сергей Александрович, SN=Большаков, G=Сергей Александрович, C=RU");

        Assert.True(read.IsSuccess, read.IsFailure ? read.Error : "");
        Assert.Equal("Большаков", read.Value.Surname);
        Assert.Equal("Сергей", read.Value.Name);
        Assert.Equal("Александрович", read.Value.Patronymic);
    }

    [Fact]
    public void Имя_без_отчества_оставляет_отчество_пустым()
    {
        var read = CertificateSubjectName.Read("SN=Большаков, G=Сергей");

        Assert.True(read.IsSuccess, read.IsFailure ? read.Error : "");
        Assert.Equal("Сергей", read.Value.Name);
        Assert.Equal("", read.Value.Patronymic);
    }

    [Theory]
    [InlineData("G=Сергей Александрович")]
    [InlineData("SN=Большаков")]
    [InlineData("")]
    [InlineData("CN=Большаков Сергей Александрович")]
    public void Без_фамилии_или_имени_отклоняется(string subject)
    {
        var read = CertificateSubjectName.Read(subject);

        Assert.True(read.IsFailure);
        Assert.Equal(CertificateSubjectName.Missing, read.Error);
    }

    [Fact]
    public void OID_и_запятая_в_кавычках_не_ломают_ФИО()
    {
        var read = CertificateSubjectName.Read(
            "STREET=\"ул. Примерная, д. 1\", 2.5.4.4=Большаков, OID.2.5.4.42=\"Сергей  Александрович\"");

        Assert.True(read.IsSuccess, read.IsFailure ? read.Error : "");
        Assert.Equal("Большаков", read.Value.Surname);
        Assert.Equal("Сергей", read.Value.Name);
        Assert.Equal("Александрович", read.Value.Patronymic);
    }
}
