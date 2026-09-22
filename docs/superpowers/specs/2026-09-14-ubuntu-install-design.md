# Установка FMU-API-Central на Ubuntu по SSH

Дата: 2026-09-14

## Цель

С рабочей Windows-машины по SSH поставить на Ubuntu: Docker, CouchDB в Docker, КриптоПро CSP без серийника, linux-сборку API и Web, systemd-юниты.

## Слой

`deploy/ubuntu/` — деплой, не код приложения.

## Границы

Входит:

- `.env.example` и чтение `.env` локальным скриптом
- `install.ps1` (Windows, Posh-SSH)
- `remote-install.sh` (Ubuntu, root через sudo)
- `docker-compose.couchdb.yml`
- первичный `/var/lib/FmuApiCentral/config.json`, если файла нет
- systemd: `fmu-api-central-api.service`, `fmu-api-central-web.service`

Не входит:

- HostApp / Windows-служба
- firewall/ufw
- HTTPS / nginx
- автотесты с живым SSH
- перезапись уже существующего `config.json`

Серийник КриптоПро ставится отдельным скриптом `license-cryptopro`, не `install`.

## Файлы

| Файл | Назначение |
| --- | --- |
| `deploy/ubuntu/files/` | дистрибутивы и compose |
| `deploy/ubuntu/files/docker-compose.couchdb.yml` | CouchDB + Dozzle |
| `deploy/ubuntu/.env.example` | шаблон SSH и параметров установки |
| `deploy/ubuntu/.env` | общий файл подключения, уже покрыт корневым `.gitignore` |
| `deploy/ubuntu/install/install.ps1` | оркестратор с Windows |
| `deploy/ubuntu/install/remote-install.sh` | установка на сервере |
| `deploy/ubuntu/update/update.ps1` | обновление zip и compose |
| `deploy/ubuntu/license-cryptopro/license-cryptopro.ps1` | серийник КриптоПро |
| `deploy/ubuntu/import-cert/.sert-env.example` | шаблон контейнера КриптоПро |
| `deploy/ubuntu/import-cert/.sert-env` | контейнер и PIN, покрыт `.gitignore` |

В `.env` достаточно имени файла: `Resolve-EnvPath` ищет его в `deploy/ubuntu/files/`.

## `.env`

```
SSH_HOST=
SSH_PORT=22
SSH_USER=
SSH_PASSWORD=
LINUX_ZIP=2-1-x64-linux.zip
CRYPTOPRO_ARCHIVE=cp-linux-amd64_deb.tgz
COUCHDB_USER=admin
COUCHDB_PASSWORD=
COUCHDB_PORT=5984
DOZZLE_PORT=9999
CRYPTOPRO_LICENSE=
```

`LINUX_ZIP` — имя или путь к `*-x64-linux.zip` (раскладка `PackLinux`: `fmu-api-central-api/`, `fmu-api-central-web/` + `wwwroot`).  
`CRYPTOPRO_ARCHIVE` — официальный архив КриптоПро с `install.sh`.  
`CRYPTOPRO_LICENSE` — серийник для `license-cryptopro.ps1`.  
`SSH_HOST` — адрес SSH. CouchDB слушает `0.0.0.0:${COUCHDB_PORT}` (Fauxton снаружи). Dozzle слушает `0.0.0.0:${DOZZLE_PORT}` (по умолчанию 9999). API в `config.json` ходит на `http://127.0.0.1:${COUCHDB_PORT}`.

Пустой обязательный ключ — стоп до SSH.

Пароль SSH и CouchDB в stdout/лог не писать. `SSH_PASSWORD` же передаётся в `sudo -S` на сервере (`SSH_USER` в sudoers).

## `install.ps1`

1. Читает `deploy/ubuntu/.env` (`KEY=value`, `#` — комментарий, кавычки снимать).
2. Проверяет наличие `LINUX_ZIP`, `CRYPTOPRO_ARCHIVE`, `remote-install.sh`, `files/docker-compose.couchdb.yml`.
3. Ставит `Posh-SSH` в CurrentUser, если модуля нет.
4. SFTP в `/tmp/fmu-central-setup/`: zip, архив КриптоПро, `remote-install.sh`, compose.
5. SSH: `sudo -S bash /tmp/fmu-central-setup/remote-install.sh` с env `COUCHDB_USER`, `COUCHDB_PASSWORD`, `COUCHDB_PORT`.
6. Код выхода remote-скрипта = код `install.ps1`.
7. Сессию SSH закрыть в `finally`.

## `remote-install.sh`

`set -euo pipefail`. Запуск только под root (после sudo).

1. Docker Engine + plugin `docker compose`; `systemctl enable --now docker`.
2. Compose → `/opt/fmu-api-central/couchdb/docker-compose.yml`, `docker compose up -d`. Ждать `http://127.0.0.1:${COUCHDB_PORT}/_up` до 60 с.
3. КриптоПро: если нет `/opt/cprocsp/bin/amd64/csptest` — распаковать архив, найти и запустить `install.sh` без серийника. Если `csptest` есть — пропуск.
4. Распаковать linux zip в `/opt/fmu-api-central/` (поверх). `chmod +x` бинарникам `fmu-api-central-api` и `fmu-api-central-web`.
5. Каталоги `/var/lib/FmuApiCentral` и `/var/log/FmuApiCentral`. `config.json` создать только если нет: `databaseConnection.enable=true`, `netAddress=http://127.0.0.1:${COUCHDB_PORT}`, user/password из env, `serverSettings.apiIpPort=2579`.
6. Юниты в `/etc/systemd/system/`: `ExecStart` с `--service`, `WorkingDirectory` = каталог бинарника, `Restart=always`, `User=root`, `After=network.target docker.service`, `Wants=docker.service`. `daemon-reload`, enable, restart.
7. Проверка: оба юнита `active`, `curl -fsS http://127.0.0.1:${COUCHDB_PORT}/_up`.

Повторный запуск идемпотентен: Docker/compose и zip поверх, КриптоПро и `config.json` не дублировать, службы restart.

## Compose

Образ `couchdb:3`. Env `COUCHDB_USER` / `COUCHDB_PASSWORD`. Порт `0.0.0.0:${COUCHDB_PORT}:5984`. Volume `couchdb-data` → `/opt/couchdb/data`.

Dozzle `amir20/dozzle:latest`. Порт `0.0.0.0:${DOZZLE_PORT}:8080` (снаружи, по умолчанию 9999). Сокет `/var/run/docker.sock`.

`update.ps1` заливает тот же compose и делает `docker compose up -d`.

## Порты

| Сервис | Порт |
| --- | --- |
| API | 2579 |
| Web | 2580 |
| CouchDB | `COUCHDB_PORT` (по умолчанию 5984) |
| Dozzle | `DOZZLE_PORT` (по умолчанию 9999) |

## Ошибки

Любая команда с ненулевым кодом — сразу `exit 1`, следующие шаги не выполнять. Нет вложенных try/catch: в `install.ps1` один `try` на верхнем уровне + `finally` для закрытия SSH.

## Проверка

Ручная: заполнить `deploy/ubuntu/.env`, `.\install.ps1` из `deploy/ubuntu/install`. Автотестов нет.
