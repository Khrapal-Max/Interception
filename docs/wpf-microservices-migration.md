# План переходу Interception на WPF + мікросервіси

## Що зроблено в цьому кроці

1. Додано три окремі API-заготовки для розмежування шарів:
   - `Interception.Services.DomainApi`
   - `Interception.Services.ApplicationApi`
   - `Interception.Services.InfrastructureApi`
2. Додано новий UI-проєкт `Interception.WpfClient` (WPF, `net10.0-windows`).
3. Оновлено `Interception.slnx`, щоб нові проєкти входили в рішення.
4. Додано скрипт публікації WPF-клієнта у self-contained форматі для копіювання на флешку.

## Цільова архітектура

- **Domain API**: доменні правила й моделі (без UI).
- **Application API**: сценарії застосунку, orchestration, DTO.
- **Infrastructure API**: доступ до БД, імпорт/експорт, інтеграції.
- **WPF UI**: тільки презентація + виклики API.

## Рекомендована поетапна міграція

1. **Винести контракти** (DTO, запити/відповіді) у shared contracts-бібліотеку.
2. **Перенести application-сервіси** у `ApplicationApi` з HTTP endpoints.
3. **Інкапсулювати доступ до БД** у `InfrastructureApi`.
4. **Domain правила** тримати у `DomainApi` або domain-бібліотеці, що використовується через API.
5. **WPF UI** перевести на MVVM і замінити прямі виклики Application/Infrastructure класів на HTTP-клієнти.

## Локальний запуск (після встановлення .NET SDK)

```bash
# окремо в різних терміналах
 dotnet run --project src/Interception.Services.DomainApi
 dotnet run --project src/Interception.Services.ApplicationApi
 dotnet run --project src/Interception.Services.InfrastructureApi
 dotnet run --project src/Interception.WpfClient
```

## Публікація WPF для флешки

```bash
bash scripts/publish-wpf-to-usb.sh /media/$USER/USB_DRIVE
```

Скрипт створює self-contained білд під `win-x64` та копіює артефакти на вказаний шлях флешки.
