# PROJECT STRUCTURE

## Ціль

Уніфікована структура репозиторію для проєкту **Interception** відповідно до поточного підходу (feature-first всередині шарів, чітке розділення `src`/`tests`).

## Базова структура

```text
/
├─ src/
│  └─ Interception.UI/
│     ├─ Application/
│     │  ├─ Analytics/
│     │  ├─ Import/
│     │  ├─ Interceptions/
│     │  ├─ Registry/
│     │  ├─ Reports/
│     │  └─ Toasts/
│     ├─ Components/
│     │  ├─ Layout/
│     │  ├─ Pages/
│     │  └─ Shared/
│     ├─ Domain/
│     ├─ Infrastructure/
│     ├─ Extensions/
│     ├─ Migrations/
│     └─ wwwroot/
├─ tests/
│  └─ Interception.Tests/
│     ├─ Application/
│     │  ├─ Analytics/
│     │  ├─ Import/
│     │  ├─ Interceptions/
│     │  ├─ Registry/
│     │  └─ Reports/
│     ├─ Components/
│     └─ Domain/
└─ infra/config на корені
   ├─ docker-compose*.yml
   ├─ docker-compose.dcproj
   └─ CODING_STANDARDS.md
```

## Правила групування

1. **Дзеркальність тестів до production-коду**:
   - тести `Application/*` мають лежати в `tests/Interception.Tests/Application/*` з тією ж feature-папкою.
2. **Feature-first**:
   - нові сервіси/DTO/моделі додаються у відповідну фічу (`Interceptions`, `Registry`, `Reports` тощо), а не в загальні «misc» каталоги.
3. **Вкладені підрозділи фіч**:
   - для складних частин (наприклад, `TextBlock`, `Builders`, `Drawers`) використовувати окремі підпапки всередині фічі.
4. **Namespace відповідає шляху**:
   - після переміщення файлів простір імен має збігатися з новим шляхом.

## Перевірка, виконана в цьому оновленні

- Виявлено та виправлено невідповідність групування тестів для `TextBlockParser`:
  - було: `tests/Interception.Tests/Application/TextBlock/TextBlockParserTests.cs`
  - стало: `tests/Interception.Tests/Application/Interceptions/TextBlock/TextBlockParserTests.cs`
- Namespace тесту вирівняно під нову ієрархію папок.
