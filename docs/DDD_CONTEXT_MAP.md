# DDD Context Map — Interception (R1)

_Дата оновлення: 10 квітня 2026_

## 1) Bounded Contexts

### BC-1: **Interceptions** (Core Domain)
- Відповідальність: фіксація спостережень/перехоплень, учасників, міток, дій.
- Власні агрегати: `InterceptionMessage`, `InterceptionMessageParticipant`, `InterceptionMessageLabel`.
- Upstream для: `Analytics`, `Reports`.

### BC-2: **Registry** (Supporting Domain)
- Відповідальність: каталоги дій, ролей, осіб, частот/підрозділів.
- Upstream для: `Interceptions`, `Analytics`, `Import`.
- Інтеграційне правило: UI споживає лише Application DTO цього контексту.

### BC-3: **Analytics** (Supporting Domain)
- Відповідальність: ідентифікація/групування НВ, граф зв'язків, підказки контексту, topology snapshots.
- Downstream від: `Interceptions`, `Registry`.
- Upstream для: `Reports` (частково), UI-аналітики.

### BC-4: **Reports** (Read-side / Downstream)
- Відповідальність: read models для візуалізацій і зведених звітів.
- Downstream від: `Interceptions`, `Analytics`, `Registry`.

### BC-5: **Import/Export** (Integration Context)
- Відповідальність: імпорт/експорт з Excel та суміжні інтеграційні сценарії.
- Використовує контракти Application контекстів, не формує власну доменну модель.

---

## 2) Integration styles (поточний стан)

- `UI Components -> Application`: **In-Process API** через сервіси/DTO.
- `Application -> Domain`: оркестрація use-case + інваріанти в Domain.
- `Application -> Infrastructure`: EF Core (`AppDbContext`).

---

## 3) Context relationships

- `Registry -> Interceptions`: **Supplier / Customer**
  - Interceptions споживає довідники дій/ролей/частот як lookup/read models.
- `Interceptions -> Analytics`: **Upstream / Downstream**
  - Analytics будує похідні моделі (кандидати, зв'язки, topology) з фактів перехоплень.
- `Interceptions + Analytics -> Reports`: **Upstream / Downstream**
  - Reports не містить command-логіки, лише read-проєкції.

---

## 4) Правила меж (R1 baseline)

1. `Components` не мають посилань на `Interception.UI.Domain*`.
2. Публічні контракти `Application` для UI повертають DTO/Read models.
3. Бізнес-правила ваг/пріоритезації, що формують сенс аналітики, виносяться в domain policy objects.
4. Cross-context інтеграція йде через Application API, без прямого доступу UI до Domain типів.

---

## 5) Next (R1 -> R2)

- Базові VO вже введені як foundation: `PersonName`, `FrequencyCode`, `DivisionName` (далі — поступова інтеграція у write-side use-cases).
- Розширити використання VO (`PersonName`, `FrequencyCode`, `DivisionName`) у core paths створення/оновлення.
- Продовжити декомпозицію analytics-правил у policy/specification об'єкти.
- Зафіксувати правила ownership shared-моделей і migration-policy для контрактів DTO.
