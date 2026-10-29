#!/bin/sh
set -eu

cd "$(dirname "$0")"

fail() {
    echo "FAIL: $1" >&2
    exit 1
}

# Копия entrypoint с каталогами в /tmp: тест не пишет в /var/lib хоста.
run_api() {
    script=$1
    tmp=$(mktemp -d)
    trap 'rm -rf "$tmp"' EXIT INT HUP
    sed "s/\\r\$//; s|/var/lib/FmuApiCentral|$tmp|g; s|/var/log/FmuApiCentral|$tmp|g" "$script" > "$tmp/ep.sh"
    set +e
    out=$(env -u COUCHDB_PASSWORD WEB_PORT=2580 sh "$tmp/ep.sh" api 2>&1)
    status=$?
    set -e
    printf '%s\n' "$status"
    printf '%s\n' "$out"
    if [ -f "$tmp/config.json" ]; then
        printf 'HAS_CONFIG\n'
        cat "$tmp/config.json"
    fi
    rm -rf "$tmp"
    trap - EXIT INT HUP
}

assert_missing_password_exits() {
    script=$1
    result=$(run_api "$script")
    status=$(printf '%s\n' "$result" | head -n 1)
    if [ "$status" -ne 1 ]; then
        fail "$script: пустой COUCHDB_PASSWORD должен завершать процесс с кодом 1, получено $status. $result"
    fi
    printf '%s\n' "$result" | grep -q "COUCHDB_PASSWORD не задан" \
        || fail "$script: нет сообщения «COUCHDB_PASSWORD не задан». $result"
    printf '%s\n' "$result" | grep -q "HAS_CONFIG" \
        && fail "$script: config.json записан без пароля. $result"
    echo "ok $script"
}

assert_missing_password_exits ./entrypoint.sh

assert_given_password_is_written() {
    tmp=$(mktemp -d)
    sed "s/\\r\$//; s|/var/lib/FmuApiCentral|$tmp|g; s|/var/log/FmuApiCentral|$tmp|g" ./entrypoint.sh > "$tmp/ep.sh"
    set +e
    env -u COUCHDB_USER COUCHDB_PASSWORD='secret-1' COUCHDB_URL='http://couchdb:5984' WEB_PORT=2580 \
        sh "$tmp/ep.sh" api >"$tmp/out" 2>&1
    set -e
    if [ ! -f "$tmp/config.json" ]; then
        fail "заданный пароль не записан в config.json. $(cat "$tmp/out")"
    fi
    grep -q '"userName": "admin"' "$tmp/config.json" || fail "имя учётки не admin по умолчанию"
    grep -q '"password": "secret-1"' "$tmp/config.json" || fail "в config.json не попал переданный пароль"
    grep -q '"netAddress": "http://couchdb:5984"' "$tmp/config.json" || fail "адрес CouchDB не http://couchdb:5984"
    rm -rf "$tmp"
    echo "ok password written"
}

assert_given_password_is_written

assert_compose_requires_password_and_hides_port() {
    file=$1
    if grep -q 'COUCHDB_PASSWORD:-admin' "$file"; then
        fail "$file: пароль admin подставлен по умолчанию"
    fi
    count=$(grep -c 'COUCHDB_PASSWORD:?COUCHDB_PASSWORD не задан' "$file" || true)
    if [ "$count" -ne 2 ]; then
        fail "$file: обязательный пароль должен быть у couchdb и api, найдено $count"
    fi
    if grep -E '^[[:space:]]*-[[:space:]]*"\$\{COUCHDB_PORT:-5984\}:5984"' "$file"; then
        fail "$file: порт 5984 опубликован на всех интерфейсах"
    fi
    if grep -E '^[[:space:]]*-[[:space:]]*"127\.0\.0\.1:\$\{COUCHDB_PORT:-5984\}:5984"' "$file"; then
        fail "$file: отладочный порт не должен быть включён по умолчанию"
    fi
    if ! grep -q '#[[:space:]]*-[[:space:]]*"127.0.0.1:\${COUCHDB_PORT:-5984}:5984"' "$file"; then
        fail "$file: нет закомментированной привязки 127.0.0.1"
    fi
    echo "ok $file"
}

assert_compose_requires_password_and_hides_port ./docker-compose.yml
