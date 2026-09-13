# NGINXTOOLS | ENGLISH INSTRUCTIONS

A cross-platform utility for managing NGINX: startup, graceful restart, and 
graceful shutdown. Runs on **Windows**, **Linux**, and **Docker** from a 
single .NET 10 codebase.

## Possibilities

- **startup** - Starting NGINX with a preliminary configuration check
- **restart** - A graceful restart (`nginx -s reload`), which prevents 
current connections from being dropped, and if a configuration error 
occurs, NGINX continues to run with the old working configuration.
- **shutdown** - Graceful shutdown (`nginx -s quit`) with a wait
for active requests to finish and a forced kill after a timeout
- **Auto-detection** of the NGINX directory next to the executable
- **External settings file** - no need to recompile the application
- **Single-file publishing** - one binary, no dependencies

## Requirements

- .NET 10 SDK — **for building only**
- NGINX (Windows or Linux)

Released releases are published as self-contained single-file binaries:
no .NET required on the target machine.

## Build

```bash
git clone https://github.com/AES-Foundation/nginxtools.git
cd nginxtools
dotnet build
```

# NGINXTOOLS | RUSSIAN INSTRUCTIONS

Кроссплатформенная утилита для управления NGINX: запуск, плавный перезапуск
и корректное завершение. Работает на **Windows**, **Linux** и в **Docker**
из одной кодовой базы на .NET 10.

## Возможности

- **startup** - запуск NGINX с предварительной проверкой конфигурации
- **restart** - плавный перезапуск (`nginx -s reload`), при котором
текущие соединения не рвутся, а при ошибке в конфиге NGINX продолжает
работать со старой рабочей конфигурацией
- **shutdown** - корректное завершение (`nginx -s quit`) с ожиданием
окончания активных запросов и принудительным kill по истечении таймаута
- **Автоопределение** каталога NGINX рядом с исполняемым файлом
- **Внешний файл настроек** - не нужно пересобирать приложение
- **Single-file публикация** - один бинарник, никаких зависимостей

## Требования

- .NET 10 SDK — **только для сборки**
- NGINX (Windows или Linux)

Готовые релизы публикуются как self-contained single-file бинарники:
на целевой машине .NET не требуется.

## Сборка

```bash
git clone https://github.com/AES-Foundation/nginxtools.git
cd nginxtools
dotnet build
```
