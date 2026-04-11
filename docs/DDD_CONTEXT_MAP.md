# DDD Context Map — Interception (R2)

_Дата повторного аналізу: 10 квітня 2026_

## 1) Bounded Contexts (фактична структура коду)

### BC-1: **Interceptions** (Core Domain)
- Призначення: облік фактів перехоплень, учасників, міток, дій, службових атрибутів.
- Ключові доменні об'єкти: `InterceptionMessage`, `InterceptionMessageParticipant`, `InterceptionMessageLabel`.
- Ядро інваріантів уже в Domain:
  - унікальність «відомого» учасника в межах одного перехоплення;
  - відсутність дублю мітки в межах одного перехоплення;
  - нормалізація/очищення значень під час створення та оновлення.
- Основний write-side orchestrator: `InterceptionCommandService`.

### BC-2: **Registry** (Supporting Domain)
- Призначення: довідники, які є джерелом правил для інших контекстів (`InterceptionAction`, `ParticipantRole`, реєстри частот/підрозділів/осіб).
- Фактично працює як upstream-контекст для `Interceptions`, `Import`, `Analytics`.

### BC-3: **Analytics** (Supporting/Insight Domain)
- Призначення: формування похідних моделей (link map, групи кандидатів, ієрархії, вагові оцінки, topology snapshots).
- Характер: read-heavy + обчислювальні сценарії з результатом у DTO/станах snapshot.
- Основні залежності: факти з `Interceptions` + довідники з `Registry`.

### BC-4: **Reports** (Downstream Read-side)
- Призначення: presentation-ready read-моделі (`DayPicture`, `DivisionReport`) без власних командних інваріантів.
- Залежність: downstream від `Interceptions` + частково `Analytics`.

### BC-5: **Import/Export** (Integration Context)
- Призначення: обмін даними з Excel, перетворення у формати Application DTO.
- Висновок DDD: не є окремим доменом, а інтеграційним контекстом з anti-corruption mapping на рівні Application.

---

## 2) Context Map (зв'язки)

- `Registry -> Interceptions`: **Supplier / Customer**.
- `Interceptions -> Analytics`: **Upstream / Downstream** (події/факти як вхід для обчислень).
- `Interceptions + Analytics -> Reports`: **Upstream / Downstream**.
- `Import -> Interceptions/Registry`: **Conformist** через Application API (без прямого UI->Domain доступу).

---

## 3) Оцінка відповідності DDD (повторний зріз)

### Сильні сторони
1. **Чітке шарування**: `Components -> Application -> Domain -> Infrastructure` стабільно простежується по структурі рішення.
2. **Архітектурний guardrail тестом**: UI-шар контролюється тестом на відсутність прямого посилання на `Interception.UI.Domain`.
3. **Інваріанти у Domain**: правила унікальності учасника/мітки винесені в сутність `InterceptionMessage`, а не розпорошені в UI.
4. **Поступове впровадження Value Objects** (`PersonName`, `FrequencyCode`, `DivisionName`) у write-side сценаріях.

### Ризики/борг
1. **Анемічність окремих частин домену**: частина рішень залишається в Application-сервісах замість доменних policy/specification об'єктів.
2. **Змішання read/write concerns в одному проєкті**: для середньострокового росту може ускладнювати еволюцію bounded contexts.
3. **Неповна уніфікація VO по всіх сценаріях**: місцями ще проходять `string`-значення без єдиного доменного контракту.
4. **Cross-context coupling через shared DbContext**: швидко для MVP, але ризиковано для незалежної еволюції контекстів.

### Підсумкова оцінка
- **Поточний рівень відповідності DDD: 7.5/10 (добрий практичний рівень).**
- Проєкт уже має коректне ядро та межі контекстів, але потребує системного посилення domain policies і контрактів між контекстами.

---

## 4) Рекомендований backlog (R2 -> R3)

### P1 (найвищий пріоритет)
1. Винести бізнес-правила пріоритезації/класифікації в `Analytics` у domain policy/specification класи.
2. Зафіксувати контрактну карту між контекстами (вхід/вихід DTO, owner, правила versioning).
3. Довести використання VO до наскрізного write-side шляху для `Interceptions` і суміжних реєстрів.

### P2
1. Визначити aggregate boundaries для `ParticipantCandidateGroup`, `CanonicalPerson`, `TopologySnapshotRun` (хто root, де транзакційна межа).
2. Відокремити read-model правила від command-side там, де вони починають формувати самостійну бізнес-логіку.

### P3
1. Підготувати lightweight integration events (навіть in-process), щоб послабити зв'язність між контекстами.
2. Додати "architecture fitness tests" для заборони небажаних залежностей між підпапками `Application/*` різних контекстів.

---

## 5) Короткий висновок

Повторний аналіз підтверджує: у проєкті вже є робоча DDD-основа (bounded contexts, інваріанти в domain, контроль шарування), а основний наступний крок — менше логіки в application orchestration і більше явно вираженої доменної політики, особливо в аналітичному контексті.

## 6) Статус виконання P1 (станом на 10.04.2026)

1. **Domain policy для класифікації/пріоритезації** — виконано частково: порогові рішення групування винесені в окрему доменну policy (`ParticipantCandidateGroupingPolicy`) і підключені в analytics flow.
2. **Контрактна карта між контекстами** — виконано: додано `docs/DDD_CONTRACT_MAP.md` з owner/model/versioning правилами.
3. **VO у наскрізних write-side шляхах** — виконано частково: VO (`PersonName`, `FrequencyCode`, `DivisionName`) застосовані в write-side сценаріях `PersonRegistryService.UpdateAsync` та `FrequencyDivisionService.CorrectDivisionAsync`.

## 7) Статус виконання P2 (станом на 10.04.2026)

1. **Aggregate boundaries** — виконано: виділено окремий документ `docs/DDD_AGGREGATE_BOUNDARIES.md` з root/інваріантами/транзакційними межами для `ParticipantCandidateGroup`, `CanonicalPerson`, `TopologySnapshotRun`.
2. **Read-model vs command-side розділення** — виконано частково: контракт `IInterceptionCommandService.CreateAsync` переведено з повернення Domain-сутності на технічний результат (`Guid`), щоб UI/Components працювали через DTO/read-model запити.

## 8) Статус виконання P3 (станом на 10.04.2026)

1. **Lightweight integration events (in-process)** — виконано: введено `IIntegrationEventPublisher` + `IIntegrationEventHandler<T>`, подію `InterceptionChangedIntegrationEvent` і downstream-обробник в analytics-контексті для позначення snapshot runs як stale.
2. **Architecture fitness tests** — виконано: додано тести, що блокують небажані залежності між `Application/*/Services` різних контекстів та перевіряють, що `Application/*/Abstractions` не експонують `Interception.UI.Domain` напряму.

## 9) Продовження R3/P1 (станом на 11.04.2026)

1. **Domain service/specification для grouping unknown participants** — виконано частково: введено доменний сервіс `ParticipantCandidateGroupingDomainService`, який інкапсулює scoring, group-fit, recalculate та доменні рішення join/create, і підключено його у `ParticipantCandidateAnalysisService`.
2. **Доменні події для `ParticipantCandidateGroup`** — виконано частково: додано integration event `ParticipantCandidateGroupChangedIntegrationEvent` (`Created/Enriched/Confirmed/Dismissed`) з публікацією в `ParticipantCandidateAnalysisService` і `ParticipantCandidateGroupCommandService`.
3. **Contract tests для ключових DTO** — виконано: додано `DtoContractSnapshotTests`, що фіксує JSON shape (camelCase) для `InterceptionListItemDto`, `CandidateGroupDto`, `DayPictureDto`.

## 10) Продовження R3/P2 (станом на 11.04.2026)

1. **Логічне розділення persistence-меж** — відкладено: після рев’ю зміни з окремими read/write фабриками відкочено; повернуто єдиний `IDbContextFactory<AppDbContext>` до підготовки наступної ітерації з чіткішою міграційною стратегією.
2. **Посилення VO у write use-cases** — виконано частково: додано `RoleName` VO і застосовано разом з `PersonName`/`DivisionName` у write-path сервісах перехоплень та candidate group confirm flow.
3. **Typed-domain-exceptions** — виконано частково: введено базовий `DomainException` і спеціалізовані винятки (`DuplicateParticipantException`, `DuplicateLabelException`, `EntityNotFoundDomainException`, `AggregateStateViolationException`) з підключенням у ключові aggregate методи.

## 11) Фізичне групування коду за BC (станом на 11.04.2026)

Для вирівнювання структури з Context Map проведено повну реорганізацію `Domain` за контекстами:

- `Domain/Contexts/Interceptions` — моделі перехоплень, директивних зв'язків і пов'язані enum/policy.
- `Domain/Contexts/Registry` — довідникові моделі (`ParticipantRole`).
- `Domain/Contexts/Analytics` — canonical/grouping/topology моделі + доменні policy/service/records/enums аналітики.
- `Domain/Contexts/Reports` — доменні read-side моделі звітів + records/enums для звітності.
- `Domain/Exceptions` і `Domain/ValueObjects` залишені як Shared Kernel.

Додатково виправлено namespace/folder-узгодженість у UI-сторінці інструментів БД:

- `Components/Pages/Persistense` -> `Components/Pages/Persistence`
- namespace `Interception.UI.Components.Pages.Persistence`.

Примітка: цей етап фокусується на узгодженні рівнів і фізичних меж bounded contexts без зміни поведінки домену.
