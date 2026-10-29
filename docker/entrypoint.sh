#!/bin/sh
set -eu

mkdir -p /var/lib/FmuApiCentral /var/log/FmuApiCentral

role="${1:-api}"

if [ "$role" = "api" ]; then
    config=/var/lib/FmuApiCentral/config.json
    # Origin — адрес страницы в браузере, то есть порт Web на хосте, не порт API.
    web_port=${WEB_PORT:-2580}
    case $web_port in
        ''|*[!0-9]*)
            echo "WEB_PORT должен быть числом, получено: $web_port" >&2
            exit 1
            ;;
    esac
    origins="http://localhost:${web_port},http://127.0.0.1:${web_port}"
    if [ -n "${CORS_ORIGINS:-}" ]; then
        origins="${origins},${CORS_ORIGINS}"
    fi
    cors="["
    first=1
    oldifs=$IFS
    IFS=','
    for item in $origins; do
        IFS=$oldifs
        item=$(printf '%s' "$item" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//; s/\\/\\\\/g; s/"/\\"/g')
        if [ -n "$item" ]; then
            if [ "$first" -eq 0 ]; then
                cors="$cors,"
            fi
            first=0
            cors="$cors\"$item\""
        fi
        IFS=','
    done
    IFS=$oldifs
    cors="$cors]"

    # Пароль и адрес CouchDB в уже лежащем файле не трогаем.
    # corsOrigins переписываем каждый старт: том переживает новый контейнер и иначе остаётся со старым origin.
    if [ ! -f "$config" ]; then
        if [ -z "${COUCHDB_PASSWORD:-}" ]; then
            echo "COUCHDB_PASSWORD не задан" >&2
            exit 1
        fi
        user=$(printf '%s' "${COUCHDB_USER:-admin}" | sed 's/\\/\\\\/g; s/"/\\"/g')
        pass=$(printf '%s' "$COUCHDB_PASSWORD" | sed 's/\\/\\\\/g; s/"/\\"/g')
        # Имя сервиса compose, не localhost: API и CouchDB в разных контейнерах.
        url=$(printf '%s' "${COUCHDB_URL:-http://couchdb:5984}" | sed 's/\\/\\\\/g; s/"/\\"/g')
        cat > "$config" <<EOF
{
  "databaseConnection": {
    "enable": true,
    "netAddress": "${url}",
    "userName": "${user}",
    "password": "${pass}"
  },
  "serverSettings": {
    "apiIpPort": 2579,
    "corsOrigins": ${cors}
  }
}
EOF
    else
        jq --argjson cors "$cors" '.serverSettings.corsOrigins = $cors' "$config" > "${config}.tmp"
        mv "${config}.tmp" "$config"
    fi

    echo "CORS origins: ${cors}"

    cd /app/fmu-api-central-api
    exec ./fmu-api-central-api
fi

if [ "$role" = "web" ]; then
    cd /app/fmu-api-central-web
    exec ./fmu-api-central-web
fi

echo "Неизвестная команда: $role. Ожидается api или web." >&2
exit 1
