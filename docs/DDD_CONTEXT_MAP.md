# DDD Context Map — Interception (R2)

_Дата оновлення: 10 квітня 2026_

## 1) Bounded Contexts (повторний аналіз)

### BC-1: **Interceptions** (Core Domain)
- Відповідальність: фіксація фактів перехоплень, учасників, міток, службових атрибутів повідомлення.
- Власні агрегати: `InterceptionMessage`, `InterceptionMessageParticipant`, `InterceptionMessageLabel`.
- Upstream для: `Analytics`, `Reports`, частково `Export`.
- Критичні інваріанти: цілісність складу учасників, обмеження дублікатів міток, коректність часових/ідентифікаційних полів.

### BC-2: **Registry** (Supporting Domain)
- Відповідальність: майстер-дані (ролі, дії, підрозділи, частоти, персони).
- Upstream для: `Interceptions`, `Analytics`, `Import`, `Reports`.
- Інтеграційне правило: зовнішні споживачі працюють через Application DTO/Services, без прямої залежності від Domain.

### BC-3: **Analytics** (Supporting Domain)
- Відповідальність: пошук/кластеризація невідомих учасників, enriched snapshots, candidate groups та topology-підказки.
- Downstream від: `Interceptions`, `Registry`.
- Upstream для: `Reports` і аналітичних UI-сценаріїв.
- Domain focus: policy-based rules для scoring/weighting та пояснюваність результатів.

### BC-4: **Reports** (Read-side / Downstream)
- Відповідальність: read-моделі, презентаційні проєкції, зведення для UI.
- Downstream від: `Interceptions`, `Analytics`, `Registry`.
- Правило: без command-логіки та side-effects у моделі звітів.

### BC-5: **Import/Export** (Integration Context)
- Відповідальність: імпорт/експорт табличних форматів, мапінг DTO ↔ transport.
- Downstream/consumer від Application API інших контекстів.
- Не має власної незалежної доменної моделі; логіка валідності делегується upstream-контекстам.

---

## 2) Integration styles (поточний стан)

- `UI Components -> Application`: **In-Process API** (DI services, DTO).
- `Application -> Domain`: use-case orchestration + інваріанти/VO/policies у Domain.
- `Application -> Infrastructure`: EF Core (`AppDbContext`) та конфігурації persistence.
- `Import/Export -> Application`: anti-corruption mapping через DTO, без доступу до Domain entity.

---

## 3) Context relationships

- `Registry -> Interceptions`: **Supplier / Customer**
  - Interceptions споживає довідники як lookup/read contracts.
- `Interceptions -> Analytics`: **Upstream / Downstream**
  - Analytics будує похідні моделі (кандидати, зв'язки, topology) з фактів перехоплень.
- `Interceptions + Analytics + Registry -> Reports`: **Conformist read side**
  - Reports приймає upstream-моделі, не модифікує бізнес-правила.
- `Import/Export -> (Registry, Interceptions, Reports)`: **Open Host Service consumer**
  - Входить через Application-контракти; будь-які трансформації — на межі контексту.

---

## 4) Результати повторного DDD-огляду (R2)

1. **Boundary clarity покращилась**: структура `Application/*` узгоджена з бізнес-піддоменами (`Interceptions`, `Registry`, `Analytics`, `Reports`, `Import`, `Exports`).
2. **VO foundation присутній**, але використання у write-paths ще нерівномірне — частина сценаріїв досі працює через примітиви.
3. **Analytics rules частково централізовані**, проте частина policy-поведінки ще зберігається у великих application services (кандидат на декомпозицію).
4. **Read/write separation загалом витриманий**, але деякі DTO повторюють близькі структури між контекстами — потрібні явні ownership-правила.
5. **Import/Export межа правильна**, однак слід продовжити валідацію на вході через explicit mapping profiles та error contracts.

---

## 5) Guardrails (оновлений baseline)

1. `Components` не посилаються напряму на `Domain` типи.
2. Публічні контракти `Application` повертають DTO/read models.
3. Domain policies/specifications містять бізнес-сенс scoring/weighting, а не UI/Application.
4. Cross-context взаємодія йде через Application API та контрактні DTO.
5. Для shared DTO обов'язково визначається контекст-власник і сумісність версій (migration policy).

---

## 6) R2 -> R3 roadmap

### Статус виконання R2 (оновлено)
- ✅ Впроваджено перший крок P2: для імпорту додано anti-corruption mapper з нормалізацією VO і reasoned error contracts (`IMPORT_MAPPING`).
- 🔄 Наступний крок P2: зафіксувати ownership DTO між `Interceptions`, `Analytics`, `Reports` окремим контрактним документом.
- ✅ Розпочато R3/P3: додано lightweight architecture tests для boundary-правил залежностей шарів.

---

### Пріоритет P1
- Завершити міграцію критичних write-paths на VO (`PersonName`, `FrequencyCode`, `DivisionName`) у сценаріях створення/редагування.
- Виділити з `Analytics` application services окремі policy/specification об'єкти (scoring, merge rules, confidence bands).

### Пріоритет P2
- Формалізувати anti-corruption layer для Import (мапінг + validation + reasoned errors).
- Задокументувати ownership DTO між `Interceptions`, `Analytics`, `Reports`.

### Пріоритет P3
- Додати lightweight architecture tests на boundary-правила (заборона `Components -> Domain`, контроль залежностей контекстів).
- Оновити контекстну документацію після закриття P1/P2 і зафіксувати R3 map.
