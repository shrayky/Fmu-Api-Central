# Windows: процесс падает с `0x80131506` / CET

Симптом у службы: host пишет код `-2146233082` (`0x80131506`), Event Viewer — `KERNELBASE.dll`, источник `.NET Runtime`.

Это не VC++. .NET 9/10 требует CET (аппаратный shadow stack). На CPU с CET и непропатченном Windows процесс умирает на старте.

## Как подтвердить

Запуск вручную (не через службу):

```powershell
& "C:\Program Files\Automation\FmuApiCentral\fmu-api-central-api\2.1\fmu-api-central-api.exe" --service
```

Если в консоли:

```text
Fatal error.
Your Windows doesn't fully support CET. Please install all available Windows updates.
```

дальше либо скрипт на этой машине, либо обновление Windows.

Проверка ОС:

```powershell
winver
(Get-Item C:\Windows\System32\ntdll.dll).VersionInfo.ProductVersion
```

Нужен `ntdll` **10.0.19041.5007** или новее (KB5044273+, октябрь 2024). Ниже — CET в ОС неполный.

## Вариант 1. Только эта машина, без пересборки

PowerShell **от администратора**, один раз (пишется в реестр, переживает reboot и смену папки версии, пока имена exe те же).

```powershell
$ErrorActionPreference = 'Stop'
Get-Command Set-ProcessMitigation | Out-Null

$exes = @(
  'fmu-api-central-api.exe',
  'fmu-api-central-web.exe',
  'fmu-api-central.exe'
)

foreach ($name in $exes) {
  Set-ProcessMitigation -Name $name -Disable UserShadowStack, UserShadowStackStrictMode
  Get-ProcessMitigation -Name $name | Select-Object -ExpandProperty UserShadowStack
}

Restart-Service fmu-api-central
```

`UserShadowStack` должен быть **OFF** / `Enable: False`.

Повторная проверка:

```powershell
& "C:\Program Files\Automation\FmuApiCentral\fmu-api-central-api\2.1\fmu-api-central-api.exe" --service
```

Если `Cannot validate argument on parameter 'Disable'` / нет `UserShadowStack` — сборка Windows слишком старая, этой политики нет. Только вариант 2.

Сброс (если понадобится):

```powershell
Set-ProcessMitigation -Name fmu-api-central-api.exe -Remove
Set-ProcessMitigation -Name fmu-api-central-web.exe -Remove
Set-ProcessMitigation -Name fmu-api-central.exe -Remove
```

## Вариант 2. Обновить Windows

Поставить накопительные обновления и перезагрузить. Для Windows 10 21H2 / LTSC 2021 — CU из [каталога Microsoft](https://www.catalog.update.microsoft.com/) (x64, 21H2), не один LCU поверх очень старого билда: сначала SSU, иначе установщик падает.

После обновления `ntdll` ≥ `10.0.19041.5007`, текущие бинарники 2.1 стартуют без скрипта.

На IoT LTSC, если CU не встаёт штатно, нужен in-place upgrade с образом, куда уже интегрированы SSU + enablement + LCU (цель вроде `19044.5854`). Один LCU без остального не ставить.
