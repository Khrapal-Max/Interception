# Звіт: сторінка `/analytics/candidates` — карта логіки та вплив видалення

## Статус виконання (UI-only)
Блок **3.1 (UI-only removal)** виконано в кодовій базі:
- видалено сторінку `CandidatesPage` та `CandidateGroupDrawer`;
- прибрано DI-реєстрації candidate UI-сервісів з `Program.cs`;
- прибрано навігаційний пункт `/analytics/candidates` з `CandidatesToolbar`.

## 1) Код, пов'язаний з логікою сторінки та драверів

### UI-рівень (сторінка + drawer)
- `src/Interception.UI/Components/Pages/Analytics/Candidates/CandidatesPage.razor`
  - Маршрут `@page "/analytics/candidates"`.
  - Таби за статусами (`Open/Confirmed/Dismissed`), кнопка запуску аналізу, таблиця груп, пагінація.
  - Підключення drawer-компонента `CandidateGroupDrawer`.
- `src/Interception.UI/Components/Pages/Analytics/Candidates/CandidatesPage.razor.cs`
  - Завантаження списку груп (`IParticipantCandidateGroupQueryService.GetGroupsByStatusAsync`).
  - Запуск аналізу (`IParticipantCandidateAnalysisService.RunAsync`).
  - Керування станом табів/пагінації та відкриттям drawer.
- `src/Interception.UI/Components/Pages/Analytics/Candidates/Drawers/CandidateGroupDrawer.razor`
  - Відображення деталей групи, причин збігу, suggestion-блоків.
  - Форми/кнопки підтвердження та відхилення групи.
- `src/Interception.UI/Components/Pages/Analytics/Candidates/Drawers/CandidateGroupDrawer.razor.cs`
  - Завантаження known/context suggestions.
  - Команди `ConfirmAsync` / `DismissAsync` через `IParticipantCandidateGroupCommandService`.

### Application-рівень (основна бізнес-логіка)
- `src/Interception.UI/Application/Analytics/Services/ParticipantCandidateGroupQueryService.cs`
  - Читання груп із БД за статусом, проєкція у `CandidateGroupDto`, enrich із `InterceptionMessages`.
- `src/Interception.UI/Application/Analytics/Services/ParticipantCandidateAnalysisService.cs`
  - Основний алгоритм групування невідомих учасників:
    - добір unknown participants,
    - збагачення відкритих груп,
    - створення нових `ParticipantCandidateGroup`.
- `src/Interception.UI/Application/Analytics/Services/ParticipantCandidateGroupCommandService.cs`
  - Операції підтвердження/відхилення груп.
  - При Confirm — зв'язування з `ResolvedParticipant`.
- `src/Interception.UI/Application/Analytics/Services/KnownParticipantSuggestionService.cs`
  - Top-N suggestions по known participants.
- `src/Interception.UI/Application/Analytics/Services/ContextSuggestionService.cs`
  - Context suggestions (підрозділ/контекст) по історії.

### Domain + Infrastructure + DI
- `src/Interception.UI/Domain/ParticipantCandidateGroup.cs`
  - Aggregate для груп кандидатів: статуси, confirm/dismiss, refs, reasons.
- `src/Interception.UI/Infrastructure/AppDbContext.cs`
  - `DbSet<ParticipantCandidateGroup>` та `DbSet<ResolvedParticipant>`.
- `src/Interception.UI/Infrastructure/Configurations/ParticipantCandidateGroupConfiguration.cs`
  - Мапінг таблиць `participant_candidate_groups` та `participant_candidate_group_refs`.
- `src/Interception.UI/Program.cs`
  - Реєстрація сервісів candidate-аналізу у DI.

## 2) Аналіз впливу: чи потрібна повна заміна БД при видаленні

## Висновок
**Повна заміна БД не потрібна.**

Потрібна **цільова міграція схеми** (або серія міграцій), якщо видаляється весь candidate-модуль:
1. Видалення таблиць/зв'язків:
   - `participant_candidate_groups`
   - `participant_candidate_group_refs`
2. За потреби — ревізія `resolved_participants` (залишити/видалити залежно від того, чи використовується поза candidate flow).
3. Оновлення `AppDbContext` + EF конфігурацій + `ModelSnapshot`.

## Чому не повна заміна
- Модуль кандидатів — лише частина загальної моделі БД (interceptions, registries, reports, topology, roles тощо залишаються валідними).
- Схема побудована як набір таблиць; видалення одного bounded-контексту не вимагає перестворення всієї БД, лише контрольовану міграцію.

## Важливі залежності, які треба врахувати
Навіть якщо прибрати саму сторінку `/analytics/candidates` і drawer, `ParticipantCandidateGroups` використовується в інших сервісах:
- `Application/Interceptions/Services/InterceptionQueryService.cs`
- `Application/Reports/Services/DivisionReportService.cs`
- `Application/Exports/Services/ExcelExportService.cs`
- `Application/Analytics/Services/FrequencyWeightReportService.cs`

Тобто просте видалення UI без рефакторингу цих сервісів призведе до compile/runtime проблем.

## 3) Які класи стають непотрібними при видаленні UI `/analytics/candidates`

Нижче — чітке розділення, щоб було зрозуміло, що можна видалити одразу, а що лише при повному демонтажі candidate-функції.

### 3.1. Якщо видаляємо **лише UI сторінки** (щоб оператори не працювали з нею)
Можна видаляти одразу:

1. `Components/Pages/Analytics/Candidates/CandidatesPage.razor`
2. `Components/Pages/Analytics/Candidates/CandidatesPage.razor.cs`
3. `Components/Pages/Analytics/Candidates/Drawers/CandidateGroupDrawer.razor`
4. `Components/Pages/Analytics/Candidates/Drawers/CandidateGroupDrawer.razor.cs`

Стають непотрібними саме для UI-сценарію:
- `IParticipantCandidateGroupQueryService` + `ParticipantCandidateGroupQueryService`
- `IParticipantCandidateGroupCommandService` + `ParticipantCandidateGroupCommandService`
- `IContextSuggestionService` + `ContextSuggestionService`
- `IKnownParticipantSuggestionService` + `KnownParticipantSuggestionService`
- `IParticipantCandidateAnalysisService` + `ParticipantCandidateAnalysisService`

> Примітка: ці сервіси можна прибрати з DI (`Program.cs`) тільки якщо вони не використовуються в інших маршрутах/джобах.
> Для поточного коду прямі виклики йдуть саме зі сторінки/дравера, але перед фізичним видаленням треба ще раз перевірити usages.

### 3.2. Якщо видаляємо **всю candidate-функцію**, а не лише UI
Додатково стають непотрібними:

- Domain:
  - `Domain/ParticipantCandidateGroup.cs`
  - `Domain/Policies/ParticipantCandidateGroupingPolicy.cs`
  - `Domain/Services/ParticipantCandidateGroupingDomainService.cs`
  - `Domain/Records/ParticipantRef.cs` (якщо не лишається інших використань)
  - `Domain/PatternMatchReasons.cs` (якщо не лишається інших використань)
  - `Domain/PatternRecognitionOptions.cs` (якщо не лишається інших використань)
  - `Domain/Records/UnknownContext.cs`
  - `Domain/Enums/CandidateGroupStatus.cs`

- Application candidate DTO/events/abstractions/services:
  - `Application/Analytics/Dtos/CandidateGroupDto.cs`
  - `Application/Analytics/Dtos/CandidateGroupStatusDto.cs`
  - `Application/Analytics/Dtos/KnownParticipantSuggestionDto.cs`
  - `Application/Analytics/Dtos/CandidateContextSuggestionDto.cs`
  - `Application/Analytics/Abstractions/*Candidate*.cs`
  - `Application/Analytics/Services/ParticipantCandidate*.cs`
  - `Application/Analytics/Services/KnownParticipantSuggestionService.cs`
  - `Application/Analytics/Services/ContextSuggestionService.cs`
  - `Application/Analytics/Events/ParticipantCandidateGroupChangedIntegrationEvent.cs`

- Infrastructure/EF:
  - `Infrastructure/Configurations/ParticipantCandidateGroupConfiguration.cs`
  - `AppDbContext` поле `DbSet<ParticipantCandidateGroup>`
  - Міграції та `ModelSnapshot`, що містять candidate-таблиці.

Але це можливо тільки після рефакторингу сервісів, які зараз читають `ParticipantCandidateGroups` (interceptions/reports/exports/frequency-weights).

## Рекомендований безпечний план видалення
1. Вимкнути маршрут і UI-компоненти (`CandidatesPage`, `CandidateGroupDrawer`).
2. Видалити DI-реєстрації candidate-сервісів у `Program.cs`.
3. Рефакторити залежні сервіси (interceptions/reports/exports/frequency-weights), прибравши запити до `ParticipantCandidateGroups`.
4. Додати EF-міграцію для видалення candidate-таблиць та індексів.
5. Прогнати інтеграційні перевірки звітів/експортів/інтерсепшенів.

## Підсумок по питанню
При видаленні коду логіки сторінки `/analytics/candidates` і її drawer-ів:
- **не потрібна повна заміна БД**;
- **потрібна точкова міграція схеми + рефактор залежних сервісів**.
