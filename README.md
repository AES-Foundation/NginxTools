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

- .NET 10 SDK - **только для сборки**
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
```powershell
dotnet publish NginxTools -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./dist/win
```

### Linux x64
```powershell
dotnet publish NginxTools -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./dist/linux
```

### Linux ARM64
```powershell
dotnet publish NginxTools -c Release -r linux-arm64 --self-contained -p:PublishSingleFile=true -o ./dist/arm64
```

## Пример расположения файлов NGINX
```
my-server/
├── nginxtools.exe      <-- ваш бинарник
└── nginx/              <-- ваш NGINX
    ├── nginx.exe
    ├── conf/
    └── ...
```

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

| Поле                          | Описание                                                           | По умолчанию       |
|-------------------------------|--------------------------------------------------------------------|--------------------|
| `nginxDir`                    | Явный путь к каталогу NGINX. `null` - использовать автоопределение | `null`             |
| `autoDetectNginx`             | Искать NGINX рядом с бинарником                                    | `true`             |
| `shutdownGraceTimeoutSeconds` | Таймаут плавного завершения перед принудительным kill              | `30`               |
| `realIpHeader`                | Заголовок который передаёт настоящий IP                            | `CF-Connecting-IP` |
| `realIpRecursive`             | Рекурсивный поиск (Будет пропускать доверенные сервера в списке)   | `true`             |
| `ipSources`                   | Источник IP диапазонов (Откуда будет брать диапазоны)              | (Пример ниже)      |

## Обновление IP-диапазонов Cloudflare и других сервисов

Утилита скачивает актуальные диапазоны IP-адресов с указанных вами URL
и генерирует набор конфигурационных файлов NGINX для восстановления
реального IP-адреса клиента.

### Быстрый старт

```cmd
nginxtools update-ips
```

Что происходит:
1. Для каждого активного источника скачиваются списки IPv4/IPv6.
2. Генерируется отдельный `.conf`-файл на каждый источник.
3. Генерируется сводный файл `conf/real_ip.conf` с директивой `real_ip_header`
   и `include` на каждый активный источник.
4. Если в `nginx.conf` уже подключён `real_ip.conf` и что-то изменилось -
   NGINX автоматически перезагружается (плавно, без разрыва соединений).

### Что вы увидите в выводе

```
=== Обновление IP-диапазонов ===

[cloudflare] обновление...
[my-proxy] обновление...
Сводный файл real_ip.conf обновлён.

--- Результат ---
[SUCCESS] cloudflare           обновлён        15 IPv4, 7 IPv6
[SUCCESS] my-proxy             без изменений   8 IPv4

В ваш nginx.conf ещё не добавлен include. Откройте файл:
     F:\Servers\nginx\conf\nginx.conf
   и внутри блока http { ... } добавьте строку:
     include conf/real_ip.conf;

Обнаружены изменения. Проверяем конфигурацию и перезагружаем NGINX...

NGINX успешно перезагружен.
```

### Первый запуск - что делать

1. Запустите:
   ```bash
   nginxtools update-ips
   ```
2. Если утилита сообщит, что `include` ещё не добавлен, откройте `nginx.conf`
   и **внутри блока `http { ... }`** добавьте строку:
   ```nginx
   include conf/real_ip.conf;
   ```
3. Запустите применить:
   ```bash
   nginxtools restart
   ```

После этого все дальнейшие запуски `nginxtools update-ips` или
`nginxtools startup` будут автоматически подхватывать изменения,
если они есть, и не трогать NGINX, если диапазоны не изменились.

### Флаги команды `update-ips`

| Флаг           | Описание                                                          |
|----------------|-------------------------------------------------------------------|
| `--no-reload`  | Только записать файлы, **не перезагружать** NGINX                 |

Полезно для деплоя, когда обновление IP и reload - отдельные стадии.

### Настройка источников

Список источников задаётся в `nginxtools.settings.json` в поле `ipSources`.
Каждый источник - это объект:

| Поле         | Обязательно | Описание                                                           | По умолчанию                        |
|--------------|:-----------:|--------------------------------------------------------------------|-------------------------------------|
| `name`       | да          | Имя источника для логов и подсказок                                | `cloudflare`                        |
| `enabled`    | нет         | Отключить без удаления - просто поставить `false`                  | `true`                              |
| `ipv4Url`    | нет         | URL со списком IPv4-диапазонов (по одному в строке)                | `https://www.cloudflare.com/ips-v4` |
| `ipv6Url`    | нет         | URL со списком IPv6-диапазонов                                     | `https://www.cloudflare.com/ips-v6` |
| `outputFile` | да          | Имя выходного `.conf`-файла (относительно `conf/` NGINX)           | `cloudflare_set_real_ip_from.conf`  |

#### Заменить Cloudflare на свой сервис

Удалите дефолтный объект и добавьте свой:

```json
{
  "ipSources": [
    {
      "name": "my-cdn",
      "enabled": true,
      "ipv4Url": "https://cdn.example.com/ips-v4.txt",
      "ipv6Url": null,
      "outputFile": "my_cdn_set_real_ip_from.conf"
    }
  ]
}
```

#### Добавить сервис рядом с Cloudflare

Оставьте дефолтный объект и добавьте новый:

```json
{
  "ipSources": [
    {
      "name": "cloudflare",
      "enabled": true,
      "ipv4Url": "https://www.cloudflare.com/ips-v4",
      "ipv6Url": "https://www.cloudflare.com/ips-v6",
      "outputFile": "cloudflare_set_real_ip_from.conf"
    },
    {
      "name": "my-proxy",
      "enabled": true,
      "ipv4Url": "https://internal.example.com/proxy-ips.txt",
      "ipv6Url": null,
      "outputFile": "my_proxy_set_real_ip_from.conf"
    }
  ]
}
```

#### Отключить источник, не удаляя его

```json
{
  "name": "cloudflare",
  "enabled": false,
  "...": "..."
}
```

### Заголовок `real_ip_header`

Директива `real_ip_header` - уровня `http`, поэтому она одна на все источники.
Задаётся в настройках:

```json
{
  "realIpHeader": "CF-Connecting-IP",
  "realIpRecursive": true
}
```

Если у вас несколько источников с разными заголовками - это ограничение NGINX,
не утилиты. Решается через отдельные `server { real_ip_header ...; }` блоки.

### Автоматизация

**Windows (Планировщик заданий):**
```
Программа:      C:\path\to\nginxtools.exe
Аргументы:      update-ips
Расписание:     ежедневно, 03:00
```

**Linux (systemd timer) (Ещё не тестировалось)**

### Безопасность и идемпотентность

- Если содержимое файла не изменилось - файл не перезаписывается, NGINX не перезагружается.
- Запись атомарная: сначала во временный файл, потом `File.Move`.
- UTF-8 без BOM и LF-окончания строк - NGINX не должен ругаться на Windows/Linux-перенос.
- Проверка `nginx -t` перед reload. Если конфиг сломан - NGINX продолжит работать со старой версией.
