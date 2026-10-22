using System.Text;
using Application.Instance.Services;

namespace Application.Tests;

public class CheckerInstallCommandTests
{
    /// <summary>
    /// EncodedCommand не раскрывается ни в cmd, ни в уже открытом PowerShell.
    /// Установка стартует только после полного CopyHere.
    /// </summary>
    [Fact]
    public void Build_возвращает_команду_скачивания_и_установки()
    {
        var result = CheckerInstallCommand.Build("https://central.example:2579/", "inst token");

        Assert.True(result.IsSuccess);
        Assert.Equal(Script(
            "$z=Join-Path $env:TEMP 'fmu-api.zip'; " +
            "$d=Join-Path $env:TEMP 'fmu-api-setup'; " +
            "$e=Join-Path $d 'fmu-api.exe'; " +
            "(New-Object Net.WebClient).DownloadFile('https://central.example:2579/api/fmuApiInstance/inst%20token/checker-distribution', $z); " +
            "if (Test-Path $d) { Remove-Item $d -Recurse -Force }; " +
            "New-Item -ItemType Directory -Path $d | Out-Null; " +
            "$sh=New-Object -ComObject Shell.Application; " +
            "$t=0; $src=$null; $dst=$null; " +
            "while ((-not $src -or -not $dst) -and $t -lt 60) { $src=$sh.NameSpace($z); $dst=$sh.NameSpace($d); if ($src -and $dst) { break }; Start-Sleep 1; $t++ }; " +
            "if (-not $src -or -not $dst) { exit 1 }; " +
            "$dst.CopyHere($src.Items(), 0x14); " +
            "$t=0; $prev=-1; $ready=$false; " +
            "while ($t -lt 60) { " +
            "$bytes=0; $nested=$false; " +
            "foreach ($f in @(Get-ChildItem -LiteralPath $d -Recurse -Force -ErrorAction SilentlyContinue)) { if (-not $f.PSIsContainer) { $bytes+=$f.Length }; if ($f.Name -eq 'fmu-api-check.exe') { $nested=$true } }; " +
            "if ((Test-Path -LiteralPath $e) -and $nested -and $bytes -gt 0 -and $bytes -eq $prev) { $ready=$true; break }; " +
            "$prev=$bytes; Start-Sleep 1; $t++ }; " +
            "if (-not $ready) { exit 1 }; " +
            "Start-Process -FilePath $e -ArgumentList '--install' -Verb RunAs -Wait"),
            result.Value);
    }

    [Fact]
    public void Build_ошибка_если_нет_публичного_адреса()
    {
        var result = CheckerInstallCommand.Build("  ", "token-1");

        Assert.True(result.IsFailure);
        Assert.Contains("адрес", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ошибка_если_нет_токена()
    {
        var result = CheckerInstallCommand.Build("https://central.example:2579", "  ");

        Assert.True(result.IsFailure);
        Assert.Contains("токен", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static string Script(string body)
    {
        var bytes = Encoding.Unicode.GetBytes(body);
        return "powershell -NoProfile -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(bytes);
    }
}
