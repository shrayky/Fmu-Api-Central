FMU-API-CENTRAL

<img width="1280" height="587" alt="image" src="https://github.com/user-attachments/assets/fef27d54-be3d-49f8-82e7-e9033f3d2e41" />

Система для мониторинга и обновления [fmu-api](https://github.com/shrayky/FMU-API).

Состоит из сервера WebApi и клиента для настройки и мониторинга WebView

Установка: запуск из консоли
```
fmu-api-central.exe --install
```

Удаление: запуск из консоли
```
fmu-api-central.exe --uninstall
```

Хост ставит одну Windows-службу и запускает API и WebApp как дочерние процессы.

Для работы требуется установить [CouchDb](https://couchdb.apache.org).
