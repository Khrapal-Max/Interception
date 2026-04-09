# Interception.UI — довідник по поточній логіці проєкту

## Призначення системи

`Interception.UI` — це операторсько-аналітична система для ведення, аналізу та зведення перехоплень.

Система підтримує кілька пов’язаних контурів роботи:

- введення і редагування спостережень;
- імпорт перехоплень із файлу;
- ведення довідників і реєстрів;
- аналітику невідомих / проблемних випадків;
- побудову карти зв’язків і ієрархії груп;
- об’єднання кількох підтверджених записів однієї людини в один профіль;
- ручний контур структурного керування між особами;
- read-side звіти.

---

# 1. Архітектурна схема

## 1.1. Основні шари

### Domain
Містить сутності предметної області:
- `InterceptionMessage`
- `InterceptionMessageParticipant`
- `InterceptionAction`
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
- application services
- query/write/report/analytics logic

### Components
Blazor UI:
- сторінки
- drawer-компоненти
- toolbars
- shared UI

---

# 2. Головні предметні сутності

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

Клас:
- `Domain/Interceptions/InterceptionMessage.cs`

Призначення:
- зберігає вихідний епізод;
- не містить аналітичного висновку про “хто це насправді”;
- саме з нього будуються майже всі read-side та analytics сценарії.

---

## 2.2. `InterceptionMessageParticipant`
Учасник конкретного observation.

Призначення:
- фіксує ім’я/роль у межах конкретного повідомлення;
- може бути:
  - known
  - unknown (`IsUnknown = true`)

Це **не канонічна особа**, а саме “учасник у конкретному observation”.

---

## 2.3. `ParticipantCandidateGroup`
Аналітична група-кандидат для невідомих / схожих учасників.

Призначення:
- збирає кілька observation participant refs, які система вважає пов’язаними;
- має:
  - `ConfidenceScore`
  - `PatternMatchReasons`
  - `SuggestedName`
  - `SuggestedRole`
  - `SuggestedDivision`
  - `Status` (`Open`, `Confirmed`, `Dismissed`)
  - `ResolvedParticipantId` після підтвердження

Це ключова аналітична сутність для переходу від НВ до встановленої особи.

---

## 2.4. `ResolvedParticipant`
Підтверджена особа — аналітичний висновок про те, хто стоїть за observation / candidate group.

Призначення:
- окремий confirmed registry row;
- не змінює сам observation;
- може містити:
  - `Name`
  - `Role`
  - `Division`
  - `Frequency`
  - `ConfirmedBy`
  - `ConfirmedAt`

Важливо:
- зараз система **дозволяє кілька confirmed rows для однієї назви**, якщо це різні контексти;
- однакова назва сама по собі **не означає автоматичне об’єднання**.

---

## 2.5. `CanonicalPerson`
Технічна доменна назва сутності, яка в операторському UI подається як **об’єднаний профіль**.

Призначення:
- об’єднати кілька confirmed rows (`ResolvedParticipant`) в один профіль однієї людини;
- застосовується **не для всіх осіб**, а лише коли реально є сенс у злитті.

Сутності:
- `CanonicalPerson`
- `CanonicalPersonMember`

Операторський зміст:
- кілька назв / кілька контекстних записів → **один об’єднаний профіль**

---

## 2.6. `PersonDirectiveRelation`
Аналітичний явний зв’язок структурного керування між двома особами.

Призначення:
- не описує кожен observation;
- зберігає explicit structural link:
  - хто ким керує
  - з якою впевненістю
  - якого типу цей зв’язок

Особливість поточної моделі:
- з кожного боку relation може бути:
  - або `CanonicalPerson`
  - або standalone `ResolvedParticipant`

Тобто relation більше **не canonical-only**.

---

## 2.7. Topology snapshot сутності
Група сутностей для snapshot-карти зв’язків:
- `TopologySnapshotRun`
- `TopologySnapshotGroup`
- `TopologySnapshotGroupMember`
- `TopologySnapshotGroupFrequency`
- `TopologySnapshotGroupAction`
- `TopologySnapshotBridge`
- `TopologySnapshotBridgeAction`

Призначення:
- будувати карту зв’язків не “на льоту” кожного разу,
- а зберігати read-side snapshot по періоду.

---

# 3. Контур спостережень

## 3.1. Створення / редагування / видалення
Сервіс:
- `Application/Interceptions/Services/InterceptionCommandService.cs`

Основні сценарії:
- `CreateAsync`
- `UpdateAsync`
- `DeleteAsync`

Логіка:
1. знаходить довідникову дію;
2. створює або оновлює `InterceptionMessage`;
3. додає participants та labels;
4. зберігає в БД;
5. після будь-якої зміни позначає всі завершені snapshot-и карти як **stale**.

Отже:
- будь-яка зміна observation означає:
  - карту зв’язків треба перебудувати.

---

## 3.2. Read-side реєстру спостережень
Сервіс:
- `Application/Interceptions/Services/InterceptionQueryService.cs`

Призначення:
- реєстр observation;
- фільтри;
- список;
- деталізація одного observation;
- overlay аналітичних даних на сирий observation.

---

## 3.3. Підказки при операторському вводі
Сервіс:
- `Application/Interceptions/Services/InterceptionSuggestionService.cs`

Що дає:
- частота → підрозділ / вектор;
- suggestions по `VectorSignal`;
- suggestions по учасниках з урахуванням:
  - raw observation history
  - confirmed rows (`ResolvedParticipant`)
  - частоти / підрозділу / контексту

---

## 3.4. Ввід з текстового блоку
Класи:
- `Application/Interceptions/TextBlock/TextBlockParser.cs`
- `Components/Pages/Interceptions/Drawers/InterceptionTextBlockDrawer.*`

Призначення:
- вставити текстовий блок;
- розібрати його;
- заповнити observation form.

---

## 3.5. Імпорт з Excel
Сервіси:
- `InterceptionImportService`
- `ExcelImportParser`

Призначення:
- масово завантажити observation;
- кожен рядок перетворити в `InterceptionMessage`;
- при імпорті також позначати topology snapshot як `stale`.

---

# 4. Контур довідників і реєстрів

## 4.1. Довідник дій
Сервіс:
- `InterceptionActionService`

Призначення:
- керування каталогом типових дій перехоплення.

---

## 4.2. Частота → підрозділ
Сервіс:
- `FrequencyDivisionService`

Призначення:
- підтримувати мапінг частот до підрозділів;
- використовувати це як fallback там, де в observation division відсутній.

---

## 4.3. Реєстр осіб
Сервіс:
- `PersonRegistryService`

Призначення:
- показати:
  - confirmed persons (`ResolvedParticipant`)
  - observed known rows, які ще не перейшли в confirmed
- дозволити оновити / підтвердити запис

Поточна логіка:
- одна назва **може** існувати в кількох confirmed contexts;
- observed row ховається тільки якщо вже є достатньо точний confirmed match;
- новий observation з тією ж назвою, але з іншим контекстом, **не повинен пропадати автоматично**.

---

# 5. Контур аналітики невідомих / кандидатів

## 5.1. Побудова кандидатних груп
Сервіс:
- `ParticipantCandidateAnalysisService`

Призначення:
- брати observation participants;
- шукати схожі unknown / проблемні контексти;
- або збагачувати open group,
- або створювати нову `ParticipantCandidateGroup`.

Ключові будівельні елементи:
- `UnknownContext`
- `ContextProfile`
- `PatternRecognitionMath`
- `PatternMatchReasons`

---

## 5.2. Query по кандидатних групах
Сервіс:
- `ParticipantCandidateGroupQueryService`

Призначення:
- read-side для списку і деталей candidate groups.

---

## 5.3. Команди для кандидатних груп
Сервіс:
- `ParticipantCandidateGroupCommandService`

Призначення:
- `ConfirmAsync`
- `DismissAsync`

Поточна важлива логіка:
- при confirm система більше **не робить сліпий upsert лише по імені**;
- якщо є exact контекстний match → використовує існуючий confirmed row;
- якщо same-name context інший → створює новий confirmed row;
- це узгоджено з новою моделлю “одна назва може мати кілька confirmed contexts”.

---

# 6. Контур карти зв’язків

## 6.1. Побудова snapshot
Сервіс:
- `TopologySnapshotBuilder`

Призначення:
- з observation побудувати topology model;
- зберегти її у snapshot tables;
- тримати стани:
  - missing
  - building
  - completed
  - failed
  - stale

Це read-side контур, який:
- не перераховує карту кожного разу прямо з сирих observation,
- а працює через snapshot.

---

## 6.2. Читання карти зв’язків
Сервіс:
- `LinkMapService`

Призначення:
- читати останній completed snapshot;
- мапити snapshot-таблиці в UI DTO:
  - groups
  - members
  - bridges
  - top actions

Важливо:
- `LinkMapService` зараз працює **через snapshot**;
- canonical overlay поки **не є базовою частиною самої побудови карти**.

---

## 6.3. Сторінка карти
- `Components/Pages/Analytics/LinkMap/LinkMapPage.*`

Функції:
- показ поточного snapshot;
- banner про `stale/missing/failed`;
- ручний rebuild;
- focused view по group/bridge.

---

# 7. Контур ієрархії груп

## 7.1. `GroupHierarchyService`
Будує операторську ієрархію поверх уже зібраної карти.

Вхід:
- `ILinkMapService`

Поточна логіка:
- шукає parent → child через key person і member intersections;
- використовує scores;
- вибирає кращі candidate edges;
- прибирає цикли;
- будує forest кластерів;
- ставить `NeedsReview` для сумнівних випадків.

### Важливий поточний стан
Canonical overlay тут уже **враховується**:
- якщо для імені є однозначний об’єднаний профіль,
- hierarchy може звіряти людину не лише по імені, а й по merged identity.

### Але
`PersonDirectiveRelation` у цю логіку **ще не інтегрований**.

---

# 8. Контур об’єднання профілів

## 8.1. Суть
Це окремий аналітичний крок:
- не всі особи треба об’єднувати;
- об’єднання потрібне лише для проблемних / множинних / важливих випадків.

## 8.2. Сервіс
- `CanonicalPersonAnalysisService`

Призначення:
- знайти кандидатів на об’єднання;
- показати деталі кандидата;
- створити об’єднаний профіль;
- додати рядки до вже існуючого профілю;
- оновити профіль;
- видалити профіль.

## 8.3. Поточний операторський зміст
В коді лишилась доменна назва `CanonicalPerson`,
але в UI це має подаватись як:

- **Кандидати на об’єднання**
- **Об’єднаний профіль**
- **Об’єднати в один профіль**

## 8.4. Сторінка
- `Components/Pages/Analytics/PersonIdentities/PersonIdentitiesPage.*`

Поточна логіка сторінки:
- список лише проблемних кандидатів;
- деталі selected candidate;
- список confirmed rows для об’єднання;
- створити / додати / оновити / видалити об’єднаний профіль.

---

# 9. Контур структурного керування

## 9.1. Призначення
Це окремий analytics layer для explicit зв’язків:

- хто керує
- ким керує
- який тип відношення
- яка впевненість

## 9.2. Сервіс
- `PersonDirectiveRelationService`

Що робить:
- повертає всі current relations;
- повертає endpoint options для вибору особи;
- зберігає relation;
- видаляє relation.

## 9.3. Поточна модель endpoints
Relation може бути:
- canonical → canonical
- canonical → resolved
- resolved → canonical
- resolved → resolved

Тобто сторінка вже не canonical-only.

## 9.4. UI
- `Components/Pages/Analytics/DirectiveRelations/DirectiveRelationsPage.*`

Призначення:
- ручне ведення контуру керування;
- явне structural link між особами.

### Важливо
Цей контур уже існує як окремий шар,
але ще **не підключений як direction hint** у `GroupHierarchyService`.

---

# 10. Контур звітів

## 10.1. `DivisionReportService`
Будує простий зведений звіт по підрозділах.

Що робить:
- знаходить effective division для observation;
- рахує frequencies;
- рахує unknown mentions;
- рахує unknown groups;
- збирає observed people;
- додає confirmed people;
- об’єднує людей у межах division report.

### Поточний стан логіки
- canonical overlay тут уже враховується;
- explicit merged profile може впливати на aggregation;
- але система більше не повинна автоматично зливати різні context rows лише по однаковій назві, якщо explicit об’єднання не робилось.

---

## 10.2. `FrequencyWeightReportService`
Рахує вагу підрозділів на частоті.

Сенс:
- frequency → unique persons → division groups → weight percent

### Поточний стан логіки
- canonical overlay уже враховується;
- confirmed person та candidate-group мапи враховуються;
- fallback по same-name тепер обмежується безпечнішими правилами;
- без explicit об’єднання одна назва в новому контексті не повинна “зливатися” надто агресивно.

---

## 10.3. `DayPictureService`
Будує картину дня:
- division → conversation episodes → chronological entries

Сенс:
- це не стільки структурна аналітика, скільки read-side картина подій;
- observation feed лишається ближчим до сирої історії дня.

---

# 11. Database / portable контур

## 11.1. `DatabaseMaintenanceService`
Призначення:
- статус БД;
- export;
- import;
- clear;
- backup/checkpoint.

Ключова особливість:
- система розрахована на portable SQLite режим;
- робочий стан живе в `data/interception.db`.

---

# 12. UI-контури

## 12.1. Home
Сторінка:
- `Components/Pages/Home.razor`

Має плитки для:
- observation registry
- analytics
- reports
- registries
- database tools

## 12.2. Analytics pages
Поточні маршрути:
- `/analytics/candidates`
- `/analytics/link-map`
- `/analytics/group-hierarchy`
- `/analytics/frequency-weights`
- `/analytics/person-identities`
- `/analytics/directive-relations`

## 12.3. Registry pages
- частоти/підрозділи
- особи
- дії

## 12.4. Reports pages
- divisions
- day picture

---

# 13. Повний бізнес-процес у системі

Нижче — узгоджений фактичний сценарій роботи системи.

## Крок 1. Observation потрапляє в систему
Шляхи:
- ручне створення;
- text-block parsing;
- Excel import.

Результат:
- створюється `InterceptionMessage` з participants.

## Крок 2. Observation потрапляє у read-side реєстр
Його можна:
- переглядати;
- фільтрувати;
- редагувати.

## Крок 3. Аналітика шукає candidate groups
Невідомі / схожі учасники:
- групуються;
- отримують suggested name/role/division;
- стають `ParticipantCandidateGroup`.

## Крок 4. Аналітик підтверджує групу
При confirm:
- створюється або перевикористовується `ResolvedParticipant`;
- group отримує `ResolvedParticipantId`.

## Крок 5. Реєстр осіб показує confirmed та observed-known rows
Аналітик може:
- підтвердити / оновити особу;
- не всі особи автоматично стають merged profile.

## Крок 6. За потреби кілька confirmed rows зводяться в один профіль
Через `PersonIdentitiesPage`:
- створюється `CanonicalPerson`;
- confirmed rows приєднуються як `CanonicalPersonMember`.

## Крок 7. Карта зв’язків будується як snapshot
Observation changes → snapshot becomes stale → rebuild → `LinkMapService` показує карту.

## Крок 8. Ієрархія груп працює поверх link map
Ієрархія:
- шукає parent/child;
- враховує canonical overlay;
- ставить review markers.

## Крок 9. За потреби аналітик вручну фіксує structural control
Через `DirectiveRelationsPage`:
- створює `PersonDirectiveRelation`.

## Крок 10. Read-side звіти
Система дає:
- division report
- day picture
- frequency weights

---

# 14. Поточні важливі домовленості

## 14.1. Одна назва ≠ автоматично одна людина
Це вже не так.

Однакова назва:
- може бути різними confirmed rows;
- може бути новим context row;
- не повинна автоматично вважатися однією людиною, якщо explicit об’єднання не зроблено.

---

## 14.2. Об’єднаний профіль — лише для проблемних випадків
Не всіх осіб треба вести через merged profile.

Об’єднаний профіль потрібен:
- коли є кілька назв / кілька context rows;
- коли є реальна аналітична потреба звести їх в один профіль;
- коли це впливає на hierarchy / directive relations / reports.

---

## 14.3. Карта зв’язків працює через snapshot
Observation changes не перераховують карту “на льоту”.
Після зміни observation:
- snapshot → stale
- аналітик rebuild-ить snapshot
- read-side карта оновлюється

---

## 14.4. Контур керування — окремий analytics layer
Він не повинен переписувати observation.
Він фіксує explicit аналітичний висновок між особами.

---

# 15. Що в проєкті ще потрібно доробити

Нижче — зафіксований список актуальних наступних кроків.

## 15.1. Підключити `PersonDirectiveRelation` до ієрархії груп
Зараз контур керування вже існує окремо, але ще не використовується як сильний `direction hint` у `GroupHierarchyService`.

Потрібно:
- враховувати explicit relation при виборі parent → child;
- не дозволяти слабкому graph-епізоду перевертати керівний вузол у підлеглий, якщо relation каже інше;
- у спірних кейсах переводити вузли в `NeedsReview`.

---

## 15.2. Вирішити, як саме подавати merged profile по всьому UI
У коді ще живе термін `CanonicalPerson`,
але для оператора вже узгоджено, що в UI треба використовувати:

- **Кандидати на об’єднання**
- **Об’єднаний профіль**
- **Об’єднати в один профіль**

Потрібно:
- пройтися по всіх сторінках, toast-ах, текстах, route titles та help text;
- прибрати технічне слово “канонічний” з операторського шару.

---

## 15.3. Завершити інтеграцію merged profile у всю аналітику
Після появи об’єднаного профілю ми вже частково підтягнули його в:
- group hierarchy
- division report
- frequency weight report

Але ще потрібно:
- перевірити всю логіку на живих кейсах з новими частотами / новими контекстами;
- переконатися, що без explicit об’єднання одна назва не зливається надто рано;
- окремо вирішити, чи і коли merged profile потрібно враховувати в `LinkMapService` або в самому topology builder.

---

## 15.4. Вирішити подальшу роль merged profile для карти зв’язків
На зараз:
- link map працює через snapshot topology;
- merged profile не є базовою частиною самого snapshot builder.

Потрібно вирішити:
- чи достатньо canonical overlay лише в hierarchy/reports;
- чи треба додавати merged identity ще й у саму карту зв’язків.

---

## 15.5. Розвинути `DirectiveRelations`
Зараз це мінімальний ручний контур.

Далі можна додати:
- review statuses;
- редагування relation;
- підказки-кандидати зі спостережень;
- автоматичне підсилення relation на основі pattern/action logic;
- прив’язку до hierarchy explanation.

---

## 15.6. Дотиснути UX сторінок `person-identities` і `directive-relations`
Базова логіка вже є, але UX ще потребує стабілізації:
- внутрішні скроли;
- sticky action bar;
- зрозумілі операторські тексти;
- наочне відображення того, які саме rows об’єднуються.

---

## 15.7. Перевірити Home і toolbars на повноту всіх актуальних контурів
Окремо треба пройтись по:
- Home tiles
- analytics toolbar
- reports toolbar
- registry toolbar

щоб усі нові контури були доступні і не губились.

---

## 15.8. Продовжити стабілізацію тестів після змін бізнес-логіки
Ми вже кілька разів бачили, що:
- частина сервісів змінює контракт,
- а тести ще тримають стару домовленість.

Потрібно підтримувати правило:
- якщо змінюється бізнес-логіка —
  мають бути оновлені і сервіс, і тести, і операторські тексти.

---

## 15.9. Окремо перевірити контур “новий context row з тією ж назвою”
Це важливий реальний сценарій:

- особа вже підтверджена;
- приходять observation на новій частоті / в новому контексті;
- назва та сама;
- explicit об’єднання не робилось.

Потрібно, щоб система стабільно поводилась так:
- у registry новий рядок не губиться;
- у reports він не зливається надто рано;
- merged profile створюється лише за рішенням аналітика.

---

## Підсумок

Поточна версія проєкту вже має сформований кістяк:
- observation contour;
- candidate analytics;
- confirmed registry;
- merged profile;
- topology snapshot link map;
- group hierarchy;
- directive control contour;
- reports;
- portable SQLite tools.

Найближчий сильний наступний крок:
**пов’язати explicit structural control (`PersonDirectiveRelation`) з ієрархією груп і завершити узгодження merged-profile логіки по всій аналітиці та звітах.**
