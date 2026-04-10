# Interception.UI — довідник по поточній логіці проєкту

## Призначення системи

`Interception.UI` — це операторсько-аналітична система для ведення, перевірки, зведення та аналізу перехоплень.

У поточному стані система підтримує такі основні контури:

- введення, редагування та видалення спостережень;
- імпорт / експорт спостережень через Excel;
- ведення довідників і реєстрів;
- аналітику невідомих / проблемних випадків;
- об’єднання кількох підтверджених записів однієї людини в один профіль;
- карту зв’язків і ієрархію груп;
- фіксацію фактів структурного керування між особами;
- read-side звіти;
- portable SQLite-контур для локальної роботи.

---

# 1. Архітектура

## 1.1. Основні шари

### Domain
Містить сутності предметної області:
- `InterceptionMessage`
- `InterceptionMessageParticipant`
- `InterceptionAction`
- `ParticipantRole`
- `ParticipantCandidateGroup`
- `ResolvedParticipant`
- `CanonicalPerson`
- `PersonDirectiveRelation`
- topology snapshot сутності

### Infrastructure
Містить:
- `AppDbContext`
- EF Core configurations
- migrations

### Application
Містить:
- DTO
- builders
- query / write / report / import / export / analytics services

### Components
Blazor UI:
- сторінки
- drawer-компоненти
- toolbar-и
- shared UI

---

# 2. Головні доменні сутності

## 2.1. `InterceptionMessage`
Сирий факт спостереження / перехоплення.

Основні поля:
- `ObservedDate`
- `Frequency`
- `Division`
- `PointSignal`
- `VectorSignal`
- `InterceptionAction`
- `Note`
- `Participants`
- `Labels`

Призначення:
- зберігає вихідний епізод;
- не містить аналітичного висновку “хто це насправді”;
- є базою для імпорту, експорту, звітів і аналітики.

---

## 2.2. `InterceptionMessageParticipant`
Учасник конкретного observation.

Призначення:
- фіксує ім’я/роль саме в межах конкретного повідомлення;
- може бути:
  - known
  - unknown (`IsUnknown = true`)

Це не профіль особи, а учасник конкретного observation.

---

## 2.3. `InterceptionAction`
Довідникова дія observation.

Призначення:
- задає типову дію для observation;
- використовується в ручному вводі, імпорті, експорті, карті зв’язків, звітах і груповій аналітиці.

---

## 2.4. `ParticipantRole`
Довідникова роль учасника.

Призначення:
- задає єдині назви ролей;
- прибирає роздвоєння через різні написання ролі;
- використовується для нормалізації при:
  - ручному створенні / редагуванні observation,
  - імпорті observation,
  - оновленні особи в реєстрі,
  - confirm candidate group.

Поточна модель:
- роль унікальна по назві;
- опис ролі можна редагувати;
- синоніми / alias-ролі поки не реалізовані.

---

## 2.5. `ParticipantCandidateGroup`
Аналітична група-кандидат для невідомих / схожих учасників.

Призначення:
- збирає кілька observation participant refs, які система вважає пов’язаними;
- має:
  - `ConfidenceScore`
  - `PatternMatchReasons`
  - `SuggestedName`
  - `SuggestedRole`
  - `SuggestedDivision`
  - `Status`
  - `ResolvedParticipantId` після підтвердження

---

## 2.6. `ResolvedParticipant`
Підтверджена особа — аналітичний висновок про те, хто стоїть за observation / candidate group.

Призначення:
- confirmed registry row;
- не переписує observation;
- може містити:
  - `Name`
  - `Role`
  - `Division`
  - `Frequency`
  - `ConfirmedBy`
  - `ConfirmedAt`

Ключове правило:
- однакова назва ≠ автоматично одна й та сама людина;
- система дозволяє кілька confirmed rows для однієї назви, якщо контекст відрізняється.

---

## 2.7. `CanonicalPerson`
Доменна технічна сутність, яка в операторському UI подається як **об’єднаний профіль**.

Призначення:
- об’єднати кілька confirmed rows (`ResolvedParticipant`) в один профіль;
- використовується лише там, де це реально потрібно;
- не створюється автоматично для всіх осіб.

Ключова домовленість:
- створення / зведення об’єднаного профілю — це операторська дія;
- automatic merge для всіх випадків не використовується.

---

## 2.8. `PersonDirectiveRelation`
Явний факт структурного керування між двома особами.

Призначення:
- фіксує:
  - хто керує
  - ким керує
  - тип зв’язку
  - впевненість
  - observation-джерело (опційно)
- є окремим фактом, а не “побічною аналітикою”.

Поточна модель endpoints:
- canonical → canonical
- canonical → resolved
- resolved → canonical
- resolved → resolved

Важлива зміна:
- контур керування тепер вважається частиною **спостережень**, а не окремим чисто аналітичним блоком.

---

## 2.9. Topology snapshot сутності
Група read-side сутностей:
- `TopologySnapshotRun`
- `TopologySnapshotGroup`
- `TopologySnapshotGroupMember`
- `TopologySnapshotGroupFrequency`
- `TopologySnapshotGroupAction`
- `TopologySnapshotBridge`
- `TopologySnapshotBridgeAction`

Призначення:
- карта зв’язків працює через snapshot, а не рахується “на льоту” з усіх observation кожного разу.

---

# 3. Контур спостережень

## 3.1. Write-side observation
Сервіс:
- `Application/Interceptions/Services/InterceptionCommandService.cs`

Сценарії:
- `CreateAsync`
- `UpdateAsync`
- `DeleteAsync`

Поточна логіка:
1. знаходить довідникову дію;
2. створює / оновлює `InterceptionMessage`;
3. додає учасників;
4. нормалізує ролі через довідник ролей;
5. додає мітки;
6. після будь-якої зміни observation позначає completed topology snapshots як `stale`;
7. зберігає зміни.

Отже:
- будь-яка зміна observation означає, що карту зв’язків потрібно перебудувати.

---

## 3.2. Read-side реєстру спостережень
Сервіс:
- `Application/Interceptions/Services/InterceptionQueryService.cs`

Призначення:
- реєстр observation;
- фільтри;
- список;
- деталізація одного observation;
- рядки для таблиці й дії row actions.

UI:
- `Components/Pages/Interceptions/InterceptionRegistry/*`

---

## 3.3. Підказки при операторському вводі
Сервіс:
- `Application/Interceptions/Services/InterceptionSuggestionService.cs`

Що дає:
- частота → підрозділ / вектор;
- suggestions по `VectorSignal`;
- suggestions по учасниках;
- suggestion-логіка використовує:
  - raw history
  - confirmed rows
  - контекст observation.

---

## 3.4. Ввід з текстового блоку
Класи:
- `Application/Interceptions/TextBlock/TextBlockParser.cs`
- `Components/Pages/Interceptions/InterceptionRegistry/Drawers/InterceptionTextBlockDrawer.*`

Призначення:
- оператор вставляє текстовий блок;
- парсер формує observation form;
- далі оператор зберігає observation звичайним шляхом.

---

## 3.5. Імпорт / експорт observation через Excel
Сервіси:
- `ExcelExportService`
- `ExcelImportParser`
- `InterceptionImportService`

### Поточний контракт
Старий шаблон імпорту більше не підтримується.

Поточна модель:
- експорт observation формує аркуш **`Спостереження`**
- імпорт читає саме цей аркуш
- отже підтримується цикл:

**експорт → правка в Excel → імпорт назад**

### Що імпортується
- `Дата/час`
- `Частота`
- `Підрозділ`
- `Вектор`
- `Точка`
- `Дія`
- `Примітка`
- `Учасники`
- `Мітки`

### Розбір учасників
Колонка `Учасники` підтримує формат:
- `Ім'я`
- `Ім'я (роль)`
- unknown marker на кшталт `НВ 1`

### Ролі при імпорті
- role value проходить через довідник ролей;
- якщо роль знайдена в каталозі — береться канонічна назва;
- якщо не знайдена — береться нормалізоване введене значення.

### Важливий поточний стан
`InterceptionImportService` уже використовує новий round-trip формат, але зараз **не позначає topology snapshots як stale після імпорту**. Це окремий технічний борг.

---

# 4. Контур довідників і реєстрів

## 4.1. Довідник дій
Сервіс:
- `InterceptionActionService`

UI:
- `Components/Pages/Registry/InterceptionActions/*`

Призначення:
- ведення каталогу типових дій observation.

---

## 4.2. Частоти / підрозділи
Сервіс:
- `FrequencyDivisionService`

UI:
- `Components/Pages/Registry/Divisions/*`

Призначення:
- мапінг частот до підрозділів;
- fallback для observation і деяких звітів.

---

## 4.3. Реєстр осіб
Сервіс:
- `PersonRegistryService`

UI:
- `Components/Pages/Registry/Persons/*`

Призначення:
- показати:
  - confirmed rows (`ResolvedParticipant`)
  - observed known rows, які ще не перейшли в confirmed

Поточна логіка:
- одна назва може існувати в кількох confirmed contexts;
- observed row не ховається автоматично, якщо новий context справді відрізняється;
- оновлення ролі в реєстрі проходить через довідник ролей;
- при першому переведенні observed-known у confirmed створюється `ResolvedParticipant`, але не “автоматичний merged-profile для всіх”.

---

## 4.4. Довідник ролей
Сервіс:
- `ParticipantRoleService`
- `ParticipantRoleCatalogSupport`

UI:
- `Components/Pages/Registry/Roles/*`

Призначення:
- окремий registry ролей, аналогічний довіднику дій;
- ручне ведення каталогу ролей;
- нормалізація ролей у write-side і import flow.

Поточні сценарії:
- список ролей
- створення
- редагування
- seed із plain list

---

# 5. Контур аналітики невідомих / кандидатів

## 5.1. Побудова candidate groups
Сервіс:
- `ParticipantCandidateAnalysisService`

Призначення:
- шукати проблемні / схожі observation participants;
- збагачувати open groups або створювати нові;
- давати підставу для confirm / dismiss.

---

## 5.2. Query по candidate groups
Сервіс:
- `ParticipantCandidateGroupQueryService`

Призначення:
- read-side список і деталі candidate groups.

---

## 5.3. Команди для candidate groups
Сервіс:
- `ParticipantCandidateGroupCommandService`

Сценарії:
- `ConfirmAsync`
- `DismissAsync`

Поточна confirm-логіка:
- якщо є exact context match — використовує existing `ResolvedParticipant`;
- якщо є один context-free row — збагачує його;
- якщо збіг лише по імені, але контекст відрізняється — створює новий `ResolvedParticipant`.

Роль при confirm:
- теж нормалізується через довідник ролей.

---

# 6. Контур об’єднання профілів

## 6.1. Суть
Об’єднаний профіль створюється лише для проблемних випадків:
- множинні записи однієї людини;
- різні частоти / контексти;
- випадки, які реально впливають на звіти, ієрархію, контур керування.

## 6.2. Сервіс
- `CanonicalPersonAnalysisService`

Що робить:
- шукає кандидатів на об’єднання;
- читає details;
- створює об’єднаний профіль;
- додає рядки до профілю;
- оновлює назву / note;
- видаляє профіль.

### Поточна важлива логіка
- якщо вибрані рядки вже сидять у кількох singleton-профілях, оператор може звести їх в один профіль;
- якщо рядок уже входить у сформований профіль з кількох учасників, склад такого профілю через “просте створення” не змінюється.

## 6.3. UI
- `Components/Pages/Analytics/PersonIdentities/*`

Операторська термінологія:
- **Кандидати на об’єднання**
- **Об’єднаний профіль**
- **Об’єднати в один профіль**

---

# 7. Контур карти зв’язків

## 7.1. Побудова topology snapshot
Сервіс:
- `TopologySnapshotBuilder`

Поточна логіка:
- читає observation;
- будує communication groups;
- зберігає read-side snapshot;
- стани snapshot:
  - missing
  - building
  - completed
  - failed
  - stale

## 7.2. Link map read-side
Сервіс:
- `LinkMapService`

Що робить:
- читає latest completed snapshot;
- повертає UI DTO для карти:
  - groups
  - members
  - bridges
  - top actions

### Поточна важлива логіка
- `KeyPerson` лишається display-центром групи;
- для bridge detection використовуються не лише display-centers, а й **bridge representatives** / ядро представників групи;
- це зменшує похибку, коли реальний міжгруповий контакт іде не через display-center.

## 7.3. UI
- `Components/Pages/Analytics/LinkMap/*`

Призначення:
- горизонтальна структура:
  - які групи існують
  - хто з ким взаємодіє
  - через кого і на яких частотах

---

# 8. Контур ієрархії груп

## 8.1. Сервіс
- `GroupHierarchyService`

Вхід:
- `ILinkMapService`
- directive relations
- canonical overlay

## 8.2. Поточна логіка
Ієрархія вже не є чистою евристикою тільки по key person / member intersections.

Вона враховує:
- карту зв’язків;
- canonical overlay;
- explicit `PersonDirectiveRelation` як сильний **direction hint**.

Тобто:
- якщо є явний факт керування між особами, він підсилює напрямок `parent -> child`;
- конфлікт між graph-сигналом і explicit relation переводиться в `NeedsReview`.

## 8.3. UI
- `Components/Pages/Analytics/GroupHierarchy/*`

Призначення:
- вертикальна структура:
  - хто над ким
  - де є сумнівні вузли
  - де є явний контроль / наказ / коригування

---

# 9. Контур структурного керування

## 9.1. Поточний статус
Цей контур більше не є “чисто аналітикою”.

### Поточне положення в системі
Основний робочий маршрут:
- `/observations/directive-relations`

UI:
- `Components/Pages/Interceptions/DirectiveRelations/*`
- `Components/Pages/Interceptions/DirectiveRelations/Drawers/DirectiveRelationFromObservationDrawer.*`

Toolbar:
- `Components/Pages/Interceptions/Toolbar/InterceptionsToolbar.*`

Також у коді ще лишаються legacy-файли в:
- `Components/Pages/Analytics/DirectiveRelations/*`

Але primary contour зараз — саме у блоці **Спостереження**.

## 9.2. Сервіс
- `PersonDirectiveRelationService`

Що робить:
- повертає весь список relations;
- повертає identity options:
  - без фільтра — для глобальної сторінки;
  - з `participantNames` — для drawer від конкретного observation;
- зберігає relation;
- оновлює existing relation, якщо endpoint-и однакові;
- видаляє relation.

## 9.3. UI-сценарії
### Глобальна сторінка
Оператор може:
- переглянути всі факти керування;
- додати relation вручну;
- видалити relation.

### Drawer зі спостереження
У `InterceptionRegistry` є окрема кнопка між редагуванням і видаленням:
- **Зафіксувати команду**

Drawer:
- отримує учасників поточного observation;
- фільтрує тільки релевантні confirmed / merged identities;
- оператор задає:
  - from
  - to
  - relation type
  - confidence
  - comment
- зберігає relation із `SourceObservationId`.

Це і є поточна модель:
- **факт командування фіксується поруч зі спостереженням**
- а потім використовується в груповій аналітиці.

---

# 10. Контур звітів

## 10.1. `DivisionReportService`
Будує простий зведений звіт по підрозділах.

Поточна логіка:
- рахує frequencies;
- unknown mentions;
- unknown groups;
- observed / confirmed people;
- враховує canonical overlay;
- не повинен зливати різні contexts лише по імені без explicit merge.

## 10.2. `FrequencyWeightReportService`
Рахує вагу підрозділів на частоті.

Сенс:
- frequency → unique persons → division groups → weight percent

Поточна логіка:
- canonical overlay враховується;
- safe fallback по same-name обмежений;
- без explicit merge одна назва не повинна надто рано зливатися;
- observation division і confirmed / merged overlays можуть давати відмінності, які треба перевіряти на живих кейсах.

## 10.3. `DayPictureService`
Будує картину дня:
- chronological feed
- grouping by division
- операційний погляд на observation history

---

# 11. Database / portable контур

## 11.1. `DatabaseMaintenanceService`
Призначення:
- статус БД;
- import / export / maintenance;
- clear / backup / checkpoint.

Ключова особливість:
- portable SQLite режим;
- робоча база живе в `data/interception.db`.

---

# 12. Поточна UI-навігація

## 12.1. Головна
`Components/Pages/Home.razor`

Поточні головні секції:
- **Спостереження**
  - Реєстр спостережень
  - Контур керування
- **Аналітика**
  - Кандидати
  - Карта зв’язків
  - Вага підрозділів
- **Звіти**
  - Підрозділи
  - Картина дня
- **Регістри**
  - Частоти / підрозділи
  - Особи
  - Дії
  - Ролі
- **Система**
  - База даних

## 12.2. Toolbar-и
### Спостереження
- `InterceptionsToolbar`
- секції:
  - `/observations`
  - `/observations/directive-relations`

### Аналітика
- `CandidatesToolbar`
- primary sections:
  - candidates
  - link map
  - group hierarchy
  - person identities
  - frequency weights

### Регістри
- `RegistriesToolbar`
- sections:
  - divisions
  - persons
  - roles
  - actions

### Звіти
- `ReportsToolbar`

---

# 13. Повний актуальний бізнес-процес

## Крок 1. Observation потрапляє в систему
Шляхи:
- ручне створення;
- text-block parsing;
- Excel import у форматі аркуша `Спостереження`.

## Крок 2. Observation потрапляє в реєстр
Його можна:
- переглядати;
- фільтрувати;
- редагувати;
- видаляти;
- експортувати;
- імпортувати назад;
- зафіксувати по ньому факт командування.

## Крок 3. Candidate analytics
Невідомі / проблемні учасники:
- групуються;
- дають suggested data;
- переводяться в `ResolvedParticipant`.

## Крок 4. Confirm / registry
Особи переходять у confirmed registry.
Одна назва може мати кілька confirmed contexts.

## Крок 5. Operator merge
За потреби оператор створює об’єднаний профіль.

## Крок 6. Link map / hierarchy
- карта зв’язків = горизонтальна структура;
- ієрархія = вертикальна структура;
- explicit facts of command допомагають ієрархії.

## Крок 7. Reports
Система будує:
- division report
- day picture
- frequency weight report

---

# 14. Поточні ключові домовленості

## 14.1. Одна назва ≠ одна людина
Це не так за замовчуванням.
Explicit merge потрібен окремо.

## 14.2. Об’єднаний профіль створює оператор
Не система і не “автоматично при першому редагуванні”.

## 14.3. Роль проходить через довідник ролей
У write-side і import flow роль нормалізується через каталог ролей.

## 14.4. Імпорт = формат експорту
Старий шаблон імпорту більше не використовується.

## 14.5. Контур керування — факт observation-рівня
Його primary home тепер у **Спостереженнях**.
Аналітика використовує цей факт, але не є єдиним місцем його створення.

## 14.6. Карта зв’язків і ієрархія — це різні зрізи
- карта зв’язків = горизонтальна структура
- ієрархія = вертикальна структура

---

# 15. Що ще потрібно доробити

## 15.1. Прибрати або остаточно deprecate legacy analytics-page для directive relations
Primary contour уже перенесений у `Interceptions`, але старі файли в `Analytics/DirectiveRelations` ще лишаються в дереві.

## 15.2. Додати stale-marking після Excel import
Зараз write-side observation позначає completed snapshots як stale, а import flow — ще ні.

## 15.3. Розвинути довідник ролей
Поточна модель дає одну canonical name.
Наступний крок — alias / synonym support, якщо буде потрібно.

## 15.4. Продовжити стабілізацію UX `PersonIdentities`
Базова логіка вже є, але треба й далі шліфувати:
- внутрішні скроли
- sticky action bar
- повідомлення для складних merge-case

## 15.5. Додати observation-scoped review для directive relations
Корисно показувати в drawer уже наявні факти керування для цього observation, щоб оператор бачив дубль або помилку.

## 15.6. Завершити узгодження merged-profile логіки у всіх аналітичних read-side
Особливо:
- frequency weight report
- division report
- link map / topology snapshot interaction

## 15.7. Вирішити, чи потрібен merged-profile deeper inside topology snapshot builder
Зараз карта зв’язків живе через snapshot і не використовує merged identity як базовий шар побудови.

---

# Підсумок

Поточна версія проєкту вже має сформований і значно більш узгоджений кістяк:

- observation contour
- round-trip Excel import/export
- roles catalog
- confirmed registry
- operator-created merged profile
- topology snapshot link map
- hierarchy with directive hints
- directive control contour moved into observations
- reports
- portable SQLite tools

Найближчі сильні кроки:
1. добити technical debt після переносу `directive-relations`;
2. додати stale-marking після import;
3. завершити узгодження merged-profile логіки по всіх read-side звітах і картах.
