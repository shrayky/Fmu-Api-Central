using System.Text;
using CSharpFunctionalExtensions;

namespace Application.Instance.Services;

/// <summary>
/// EncodedCommand не раскрывает $ и % при вставке в cmd и в уже открытый PowerShell.
/// CopyHere асинхронный: установка стартует, когда размер каталога перестал расти и на месте оба exe.
/// </summary>
public static class CheckerInstallCommand
{
    public static Result<string> Build(string? publicAddress, string? token)
    {
        var address = (publicAddress ?? string.Empty).Trim().TrimEnd('/');
        if (address.Length == 0)
            return Result.Failure<string>("Не задан публичный адрес сервера");

        var id = (token ?? string.Empty).Trim();
        if (id.Length == 0)
            return Result.Failure<string>("Не указан токен инстанса");

        id = Uri.EscapeDataString(id);
        var url = $"{address}/api/fmuApiInstance/{id}/checker-distribution".Replace("'", "''");

        var script = new StringBuilder();
        script.Append("$z=Join-Path $env:TEMP 'fmu-api.zip'; ");
        script.Append("$d=Join-Path $env:TEMP 'fmu-api-setup'; ");
        script.Append("$e=Join-Path $d 'fmu-api.exe'; ");
        script.Append("(New-Object Net.WebClient).DownloadFile('");
        script.Append(url);
        script.Append("', $z); ");
        script.Append("if (Test-Path $d) { Remove-Item $d -Recurse -Force }; ");
        script.Append("New-Item -ItemType Directory -Path $d | Out-Null; ");
        script.Append("$sh=New-Object -ComObject Shell.Application; ");
        script.Append("$t=0; $src=$null; $dst=$null; ");
        script.Append("while ((-not $src -or -not $dst) -and $t -lt 60) { $src=$sh.NameSpace($z); $dst=$sh.NameSpace($d); if ($src -and $dst) { break }; Start-Sleep 1; $t++ }; ");
        script.Append("if (-not $src -or -not $dst) { exit 1 }; ");
        script.Append("$dst.CopyHere($src.Items(), 0x14); ");
        script.Append("$t=0; $prev=-1; $ready=$false; ");
        script.Append("while ($t -lt 300) { ");
        script.Append("$bytes=0; $nested=$false; ");
        script.Append("foreach ($f in @(Get-ChildItem -LiteralPath $d -Recurse -Force -ErrorAction SilentlyContinue)) { if (-not $f.PSIsContainer) { $bytes+=$f.Length }; if ($f.Name -eq 'fmu-api-check.exe') { $nested=$true } }; ");
        script.Append("if ((Test-Path -LiteralPath $e) -and $nested -and $bytes -gt 0 -and $bytes -eq $prev) { $ready=$true; break }; ");
        script.Append("$prev=$bytes; Start-Sleep 1; $t++ }; ");
        script.Append("if (-not $ready) { exit 1 }; ");
        script.Append("Start-Process -FilePath $e -ArgumentList '--install' -Verb RunAs -Wait");

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script.ToString()));
        return Result.Success("powershell -NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded);
    }
}
