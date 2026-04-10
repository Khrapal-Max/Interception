# DDD Contract Map — Interception (R2)

_Дата: 10 квітня 2026_

## Мета
Документ фіксує ownership і правила versioning для міжконтекстних контрактів (DTO/Application API), щоб зменшити implicit coupling між bounded contexts.

## Контекстні контракти

| Upstream BC | Downstream BC | Контракт (вхід/вихід) | Owner | Versioning policy |
|---|---|---|---|---|
| Registry | Interceptions | `ParticipantRoleListItemDto`, role map для нормалізації ролей | Registry | Additive-first: нові поля тільки optional; перейменування через deprecate-період |
| Registry | Analytics | Частоти/підрозділи, ролі (lookup/read data) | Registry | Стабільні semantic keys; зміна ключів лише з migration script |
| Interceptions | Analytics | Повідомлення, учасники, мітки (read access через App layer/Db projections) | Interceptions | Ідентифікатори immutable; зміна форми даних тільки через сумісні проєкції |
| Interceptions | Reports | `InterceptionListItemDto`, фільтри, агреговані read-моделі | Interceptions | Контракти для UI-представлення: backward-compatible зміни в межах мінорної ревізії |
| Analytics | Reports | `DayPictureDto`, `DivisionReportDto`, аналітичні зрізи/зв'язки | Analytics | Non-breaking extension DTO; видалення поля тільки після 1 релізу deprecation |
| Import | Interceptions/Registry | `ImportRowDto`, `ImportParticipantDto`, `InterceptionFormDto` mapping | Import (mapping), Domain owner — цільовий BC | Anti-corruption mapping обов'язковий; пряме використання Domain-типів заборонено |

## Загальні правила еволюції контрактів

1. **Owner-only changes**: змінювати контракт може лише owner BC.
2. **Additive over breaking**: спочатку додавання optional поля, потім міграція споживачів.
3. **Semantic compatibility**: ключові бізнес-поля (`Id`, `Name`, `Frequency`, `Division`) не змінюють сенс без окремої migration policy.
4. **Deprecation window**: breaking-зміни лише після фази сумісності (мінімум один реліз).
5. **Contract tests**: для критичних контрактів додавати integration/approval тести на форму DTO.

## Що вважати порушенням

- UI/Components читає Domain-сутності напряму, минаючи Application DTO.
- Downstream BC диктує структуру upstream DTO.
- Зміна семантики поля без версіонування та migration guidance.
