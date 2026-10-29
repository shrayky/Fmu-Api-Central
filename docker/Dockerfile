# Single-file self-contained: рантайм внутри бинарника, здесь только ICU и OpenSSL.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends unzip ca-certificates jq \
    && rm -rf /var/lib/apt/lists/*

ARG LINUX_ZIP=2-2-x64-linux.zip
ARG CRYPTOPRO_ARCHIVE=cp-linux-amd64_deb.tgz

COPY ${LINUX_ZIP} /tmp/linux.zip
COPY ${CRYPTOPRO_ARCHIVE} /tmp/cryptopro.tgz
COPY entrypoint.sh /entrypoint.sh

# Zip с Windows не сохраняет +x. CRLF в shebang даёт «/bin/sh: not found».
# CSP нужен процессу API: csptest и cpconfig. Серийник сюда не кладём.
RUN apt-get update \
    && unzip -q /tmp/linux.zip -d /app \
    && rm /tmp/linux.zip \
    && tar -xzf /tmp/cryptopro.tgz -C /tmp \
    && rm /tmp/cryptopro.tgz \
    && DEBIAN_FRONTEND=noninteractive /tmp/linux-amd64_deb/install.sh kc1 --yes \
    && rm -rf /tmp/linux-amd64_deb /var/lib/apt/lists/* \
    && test -x /opt/cprocsp/bin/amd64/csptest \
    && test -x /opt/cprocsp/sbin/amd64/cpconfig \
    && sed -i 's/\r$//' /entrypoint.sh \
    && chmod +x /entrypoint.sh \
        /app/fmu-api-central-api/fmu-api-central-api \
        /app/fmu-api-central-web/fmu-api-central-web \
    && mkdir -p /var/lib/FmuApiCentral /var/log/FmuApiCentral /var/opt/cprocsp/keys

# Single-file распаковывает native-библиотеки при старте — каталог должен быть доступен на запись.
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/dotnet-bundle

EXPOSE 2579 2580

ENTRYPOINT ["/entrypoint.sh"]
CMD ["api"]
