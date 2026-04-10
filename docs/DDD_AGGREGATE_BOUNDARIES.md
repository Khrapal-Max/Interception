# DDD Aggregate Boundaries — P2

_Дата: 10 квітня 2026_

## 1) `ParticipantCandidateGroup`

- **Aggregate Root**: `ParticipantCandidateGroup`.
- **Внутрішні сутності/VO**: `ParticipantRef` (owned collection), score/reasons, suggested role/division, status.
- **Інваріанти в межах агрегату**:
  - група містить валідні refs без дублювання;
  - перехід статусу `Open -> Confirmed/Rejected` допускається тільки через доменні методи root;
  - score/reasons оновлюються консистентно під час add/remove refs.
- **Транзакційна межа**:
  - одна транзакція на зміну одного `ParticipantCandidateGroup`;
  - масові перебудови виконуються як серія незалежних транзакцій по групах.

## 2) `CanonicalPerson`

- **Aggregate Root**: `CanonicalPerson`.
- **Внутрішні сутності/VO**: `CanonicalPersonMember` (зв'язок на `ResolvedParticipantId`), display name, note.
- **Інваріанти в межах агрегату**:
  - один `ResolvedParticipantId` не дублюється в members одного root;
  - display name обов'язкове та нормалізоване;
  - редагування note/name не порушує склад members.
- **Транзакційна межа**:
  - create/attach/detach у межах одного root — одна транзакція;
  - переноси між різними roots — двофазна операція на рівні application service з контролем конфліктів.

## 3) `TopologySnapshotRun`

- **Aggregate Root**: `TopologySnapshotRun`.
- **Внутрішні сутності/VO**: групи, частоти, bridge/action зв'язки snapshot-побудови.
- **Інваріанти в межах агрегату**:
  - status lifecycle (`Pending/Running/Completed/Failed`) змінюється лише дозволеними переходами;
  - stale-маркер для completed run виставляється доменним методом;
  - snapshot-дані належать конкретному run і не діляться між runs.
- **Транзакційна межа**:
  - rebuild run виконується chunk-операціями, але commit-границя — один конкретний run;
  - паралельні runs мають ізольовані набори snapshot-даних.

## 4) Практичне правило P2

- UI працює тільки з DTO/read-model через Application API.
- Domain сутності не повертаються назовні з public Application контрактів.
- Read-side правила (сортування, фільтрація, presentation mapping) виділяються в query/read сервіси, command side лишається для інваріантів і state transition.
