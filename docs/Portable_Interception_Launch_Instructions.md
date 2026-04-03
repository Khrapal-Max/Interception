# Інструкція: запуск Interception з флешки без термінала

Нижче описаний найпростіший варіант для поточного стану проекту.

## Що я бачу у вашому поточному стані

Зараз у вас є:
- `Interception.UI.csproj`
- `publish-portable.cmd`
- `start.cmd`

У `csproj` поки **немає** правил, які автоматично кладуть `start.cmd` у publish-папку.
Тому після `dotnet publish` exe з'являється, а `start.cmd` усередину publish **не копіюється** автоматично.

Саме тому ви побачили в publish тільки `Interception.UI.exe` і запускали його напряму.

---

## Як правильно зібрати portable-версію

### Крок 1. Запуск публікації
У корені проекту виконайте:

```bat
publish-portable.cmd
```

Скрипт створить папку:

```text
publish\portable-win-x64
```

---

## Які файли мають лежати в publish-папці

У папці `publish\portable-win-x64` повинні бути:

- `Interception.UI.exe`
- `start.cmd`
- `stop.cmd`
- `start.vbs`
- `stop.vbs`

### Важливо
Після publish зараз потрібно **вручну скопіювати** туди:
- `start.cmd`
- `stop.cmd`
- `start.vbs`
- `stop.vbs`

Бо `csproj` поки не налаштований на автоматичне копіювання цих файлів.

---

## Для чого потрібен кожен файл

### `Interception.UI.exe`
Сам застосунок.

### `start.cmd`
Запускає сервер на:

```text
http://127.0.0.1:5099
```

і відкриває браузер.

### `stop.cmd`
Зупиняє процес `Interception.UI.exe`.

### `start.vbs`
Тихо запускає `start.cmd` **без чорного вікна консолі**.

### `stop.vbs`
Тихо запускає `stop.cmd` **без чорного вікна консолі**.

---

## Як запускати без термінала

### Запуск
Подвійний клік по:

```text
start.vbs
```

Що відбудеться:
- створяться папки `data` і `data\keys`, якщо їх ще нема;
- стартує `Interception.UI.exe`;
- відкриється браузер на `http://127.0.0.1:5099`.

### Зупинка
Подвійний клік по:

```text
stop.vbs
```

Що відбудеться:
- буде знайдено процес `Interception.UI.exe`;
- сервер буде зупинено без відкриття термінала.

---

## Як зробити ярлики

### Варіант 1 — найпростіший
У папці `publish\portable-win-x64`:

1. Натисніть правою кнопкою на `start.vbs`
2. Оберіть **Створити ярлик**
3. Перейменуйте ярлик у:

```text
Запустити Interception
```

4. Натисніть правою кнопкою на `stop.vbs`
5. Оберіть **Створити ярлик**
6. Перейменуйте ярлик у:

```text
Зупинити Interception
```

---

## Як поставити іконку ярлику

### Для ярлика запуску
1. Правою кнопкою на ярлику `Запустити Interception`
2. **Властивості**
3. Вкладка **Ярлик**
4. **Змінити значок**
5. Вибрати:
   - або `Interception.UI.exe`
   - або окремий `.ico` файл, якщо зробите його пізніше

### Для ярлика зупинки
Можна:
- або теж вказати `Interception.UI.exe`
- або лишити стандартну іконку
- або окремо зробити `.ico` для зупинки

---

## Як працювати з флешки

На флешці структура може бути такою:

```text
Interception Portable\
  Interception.UI.exe
  start.cmd
  stop.cmd
  start.vbs
  stop.vbs
  data\
```

Тоді порядок роботи такий:

1. Вставили флешку
2. Відкрили папку
3. Подвійний клік `start.vbs`
4. Працюєте в браузері
5. Після завершення — подвійний клік `stop.vbs`

---

## Чому раніше запускалось на 5000

Бо ви запускали `Interception.UI.exe` напряму.

У цьому випадку застосунок не отримував параметр:

```text
--urls http://127.0.0.1:5099
```

Тому пішов на стандартний порт Kestrel, наприклад `5000`.

`start.cmd` якраз і потрібен для того, щоб щоразу стартувати на потрібному порту.

---

## Що можна поліпшити пізніше

Щоб не копіювати скрипти вручну після кожного publish, потім можна додати в `Interception.UI.csproj` автоматичне копіювання:

```xml
<ItemGroup>
  <None Update="start.cmd">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>Always</CopyToPublishDirectory>
  </None>
  <None Update="stop.cmd">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>Always</CopyToPublishDirectory>
  </None>
  <None Update="start.vbs">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>Always</CopyToPublishDirectory>
  </None>
  <None Update="stop.vbs">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>Always</CopyToPublishDirectory>
  </None>
</ItemGroup>
```

Але для поточного практичного сценарію можна працювати і без цього — просто копіювати 4 файли в publish-папку.
