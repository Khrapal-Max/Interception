# Interception.UI — portable SQLite збірка на флеш-носій

## Призначення

Цей документ описує **покрокову збірку portable-версії** проекту `Interception.UI` у **Visual Studio** для запуску з **флеш-носія**.

Поточний portable-режим цієї гілки:
- UI: **ASP.NET Core / Blazor Server**
- База даних: **SQLite**
- Файл БД: `./data/interception.db`
- Ключі Data Protection: `./data/keys/`
- Локальна адреса запуску: `http://127.0.0.1:5099`

> Цей режим призначений для **одного користувача** і локального запуску на одному ПК.

---

# 1. Що має бути в проекті перед збіркою

Перед publish перевірити, що в SQLite-гілці вже внесені такі зміни:

## 1.1. Провайдер БД
У `Program.cs` повинен використовуватися **SQLite**, а не PostgreSQL:

```csharp
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});
```

## 1.2. App-relative шляхи
У `Program.cs` або допоміжному коді повинні створюватися:
- `./data`
- `./data/keys`

Приклад:

```csharp
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "data");
var keysDir = Path.Combine(dataDir, "keys");
var dbPath = Path.Combine(dataDir, "interception.db");

Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(keysDir);
```

## 1.3. Data Protection
Ключі повинні зберігатися локально в папці проекту:

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir));
```

## 1.4. Bootstrap БД
У SQLite-гілці старт БД повинен працювати через:
- `EnsureCreatedAsync()` для швидкого bootstrap,
- або через коректні SQLite migrations, якщо вони вже окремо згенеровані.

## 1.5. NuGet package
У `.csproj` має бути:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.5" />
```

І не повинно бути активного runtime-використання `UseNpgsql(...)`.

---

# 2. Рекомендована структура portable-папки

Після publish папка повинна виглядати приблизно так:

```text
InterceptionPortable\
│
├─ Interception.UI.exe
├─ Interception.UI.dll
├─ Interception.UI.deps.json
├─ Interception.UI.runtimeconfig.json
├─ appsettings.json
├─ appsettings.Development.json
├─ start.cmd
├─ publish-portable.cmd
│
├─ wwwroot\
│
└─ data\
   ├─ interception.db
   └─ keys\
```

> Якщо `interception.db` ще немає, він буде створений під час першого запуску.

---

# 3. Підготовка проекту в Visual Studio

## 3.1. Відкрити solution
1. Запустити **Visual Studio**.
2. Відкрити solution проекту.
3. Переконатися, що startup project — **`Interception.UI`**.

## 3.2. Очистити старі артефакти
Перед publish рекомендовано:
1. У **Solution Explorer** натиснути правою кнопкою по solution.
2. Обрати **Clean Solution**.
3. Після цього, якщо були проблеми з провайдерами або старими збірками, вручну видалити папки:
   - `bin`
   - `obj`

для startup-проекту `Interception.UI`.

## 3.3. Перевірити Debug запуск
Перед publish бажано один раз перевірити локальний запуск:
1. Натиснути **F5** або **Ctrl+F5**.
2. Переконатися, що проект стартує на:
   - `http://127.0.0.1:5099`
3. Переконатися, що створюються:
   - `data/`
   - `data/keys/`
   - `data/interception.db`

---

# 4. Publish у Visual Studio

## Варіант A — через GUI Visual Studio

### 4.1. Відкрити Publish
1. У **Solution Explorer** натиснути правою кнопкою по проекту **`Interception.UI`**.
2. Обрати **Publish...**

### 4.2. Створити новий publish profile
1. Обрати **Folder**.
2. Натиснути **Next**.
3. Вказати папку, наприклад:

```text
D:\Build\InterceptionPortable
```

4. Натиснути **Finish**.

### 4.3. Налаштувати profile
У publish profile виставити:

- **Configuration**: `Release`
- **Target Runtime**: `win-x64`
- **Deployment Mode**: `Self-contained`
- **Produce single file**: за бажанням
  - рекомендовано **вимкнути** на першому етапі, щоб простіше дебажити
- **Enable ReadyToRun**: можна вимкнути
- **Trim unused code**: **не рекомендується** для цієї апки

### 4.4. Запустити publish
Натиснути **Publish**.

### 4.5. Перевірити результат
У publish-папці повинні бути:
- `Interception.UI.exe`
- `appsettings.json`
- `wwwroot`
- інші runtime-файли

Після цього вручну переконатися, що є або будуть створені:
- `data/`
- `data/keys/`

---

## Варіант B — через командний рядок у Visual Studio

Можна використовувати **Terminal** всередині Visual Studio або **Developer PowerShell**.

Команда:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o .\publish\portable
```

Результат з’явиться в:

```text
.\publish\portable
```

---

# 5. Скрипт формування portable-папки

Рекомендовано додати у проект файл:

## `publish-portable.cmd`

```bat
@echo off
setlocal

cd /d %~dp0

set OUTDIR=%~dp0publish\portable

if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"

 dotnet publish -c Release -r win-x64 --self-contained true -o "%OUTDIR%"

if not exist "%OUTDIR%\data" mkdir "%OUTDIR%\data"
if not exist "%OUTDIR%\data\keys" mkdir "%OUTDIR%\data\keys"

copy /Y "%~dp0start.cmd" "%OUTDIR%\start.cmd" >nul

echo.
echo Portable build created in:
echo %OUTDIR%
echo.
pause

endlocal
```

## Що робить цей скрипт
- очищає стару папку publish;
- виконує `dotnet publish`;
- створює `data` і `data\keys`;
- копіює `start.cmd` у publish-папку.

---

# 6. Скрипт запуску portable-версії

У publish-папці має лежати:

## `start.cmd`

```bat
@echo off
setlocal

cd /d %~dp0

if not exist data mkdir data
if not exist data\keys mkdir data\keys

start http://127.0.0.1:5099
start "" "Interception.UI.exe"

endlocal
```

## Що робить цей скрипт
- переходить у папку portable-застосунку;
- створює `data`, якщо її немає;
- створює `data\keys`, якщо її немає;
- відкриває браузер;
- запускає застосунок.

> Якщо браузер відкриється раніше, ніж сервер повністю стартує, просто оновити сторінку через кілька секунд.

---

# 7. Перенос на флеш-носій

## 7.1. Після publish
Після успішного publish:
1. Відкрити папку publish, наприклад:

```text
D:\Build\InterceptionPortable
```

2. Скопіювати **весь її вміст** на флеш-носій, наприклад:

```text
E:\InterceptionPortable
```

## 7.2. Що переносити обов’язково
Потрібно перенести:
- `Interception.UI.exe`
- усі `.dll`, `.json`, runtime-файли
- `wwwroot`
- `start.cmd`
- папку `data`
- папку `data\keys`

---

# 8. Перший запуск з флешки

## Послідовність
1. Вставити флеш-носій у ПК.
2. Відкрити папку:

```text
E:\InterceptionPortable
```

3. Запустити:
- `start.cmd`

## Що має відбутися
- відкриється браузер;
- стартує локальний сервер;
- якщо БД ще немає — буде створено `data\interception.db`;
- якщо ключів ще немає — буде створено `data\keys\`.

---

# 9. Оновлення portable-версії

## Без втрати даних
Перед оновленням:
1. Закрити застосунок.
2. Зробити копію папки:

```text
data\
```

3. Замінити файли застосунку новим publish.
4. **Не видаляти** папку `data`.
5. Знову запустити `start.cmd`.

## Що переносить стан
Уся локальна історія лежить у:
- `data\interception.db`
- `data\keys\`

---

# 10. Backup

## Мінімальний backup
Достатньо зберегти:

```text
data\interception.db
```

## Повний backup
Краще зберігати:

```text
data\
```

тобто:
- `interception.db`
- `keys\`

---

# 11. Повний reset portable-стану

Якщо потрібно повністю скинути локальну БД:
1. Закрити застосунок.
2. Видалити:
   - `data\interception.db`
3. За потреби також видалити:
   - `data\keys\`

Після цього при наступному запуску:
- база буде створена заново;
- стартовий bootstrap відпрацює з нуля.

> Усі локальні дані при цьому будуть втрачені.

---

# 12. Типові проблеми і рішення

## 12.1. Браузер відкрився, але сторінка недоступна
Причина:
- застосунок ще стартує.

Що робити:
- почекати кілька секунд;
- натиснути Refresh.

## 12.2. Не створюється БД
Перевірити:
- чи флешка доступна на запис;
- чи існує папка `data`;
- чи не блокує антивірус запуск `.exe`;
- чи не захищений носій від запису.

## 12.3. Порт 5099 зайнятий
Якщо `127.0.0.1:5099` уже використовується:
1. змінити порт у `launchSettings.json` або конфігурації запуску;
2. змінити URL у `start.cmd`.

## 12.4. Після збірки запускається не той провайдер БД
Перевірити:
- чи в `Program.cs` стоїть `UseSqlite(...)`;
- чи прибрано `UseNpgsql(...)`;
- чи очищені `bin` і `obj`.

## 12.5. Таблиці не створюються
Перевірити:
- чи стартова логіка викликає `EnsureCreatedAsync()` або коректні SQLite migrations;
- чи не лишилися старі PostgreSQL migrations / snapshot;
- чи не використовується старий `interception.db`, створений у зламаному стані.

---

# 13. Рекомендації з експлуатації

## Рекомендовано
- запускати з USB 3.0 носія;
- регулярно копіювати `data\` у backup;
- коректно закривати застосунок перед витягуванням флешки;
- не запускати дві копії апки над однією БД.

## Не рекомендовано
- працювати одночасно з однією portable-БД на кількох ПК;
- виривати флешку під час активної роботи програми;
- використовувати цей режим як багатокористувацький сервер.

---

# 14. Короткий чекліст збірки

## Перед publish
- [ ] у проекті увімкнений SQLite provider
- [ ] `UseNpgsql(...)` відсутній
- [ ] `data/keys` створюються app-relative
- [ ] проект локально стартує

## Publish
- [ ] `Release`
- [ ] `win-x64`
- [ ] `Self-contained`
- [ ] `wwwroot` присутній
- [ ] `start.cmd` скопійований
- [ ] `data/keys` присутні

## Перед копіюванням на флешку
- [ ] зроблено тестовий локальний запуск
- [ ] перевірено створення `interception.db`
- [ ] перевірено відкриття `http://127.0.0.1:5099`

---

# 15. Підсумок

Portable SQLite-режим для цієї гілки збирається так:
1. відкрити solution у Visual Studio;
2. очистити старі `bin/obj`;
3. перевірити локальний старт;
4. виконати `Publish` у `Release / win-x64 / Self-contained`;
5. покласти поруч `start.cmd`;
6. перенести publish-папку на флеш-носій;
7. запускати з флешки через `start.cmd`.

Головні каталоги portable-стану:
- `data\interception.db`
- `data\keys\`

Саме їх потрібно зберігати й переносити між збірками.
