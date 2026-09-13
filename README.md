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

## Публикация под конкретную платформу
### Windows x64
```bash
dotnet publish src/NginxTools -c Release -r win-x64 --self-contained \
    -p:PublishSingleFile=true -o ./dist/win
```

### Linux x64
```bash
dotnet publish src/NginxTools -c Release -r linux-x64 --self-contained \
    -p:PublishSingleFile=true -o ./dist/linux
```

### Linux ARM64
```bash
dotnet publish src/NginxTools -c Release -r linux-arm64 --self-contained \
    -p:PublishSingleFile=true -o ./dist/arm64
```

## Пример расположения файлов NGINX
`
my-server/
├── nginxtools.exe      <-- ваш бинарник
└── nginx/              <-- ваш NGINX
    ├── nginx.exe
    ├── conf/
    └── ...
`

## Вызов управления (Пример на Windows)
### Запуск NGINX
```cmd
.\nginxtools.exe startup
```

### Перезапуск NGINX
```cmd
.\nginxtools.exe restart
```

### Остановка NGINX
```cmd
.\nginxtools.exe shutdown
```

## Файл конфигурации
Файл конфигурации `nginxtools.settings.json` содержит конфигурацию запуска,
обслуживания и т.п., располагается рядом с исполняемым файлом `nginxtools`.

| Поле | Описание | По умолчанию |
|---|---|---|
| `nginxDir` | Явный путь к каталогу NGINX. `null` - использовать автоопределение | `null` |
| `autoDetectNginx` | Искать NGINX рядом с бинарником | `true` |
| `shutdownGraceTimeoutSeconds` | Таймаут плавного завершения перед принудительным kill | `30` |
