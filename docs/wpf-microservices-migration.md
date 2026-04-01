# План переходу Interception на WPF + мікросервіси

## Що зроблено в цьому кроці

1. Фізично рознесено код за змістом у окремі проєкти:
   - `src/Interception.Domain/Domain/*`
   - `src/Interception.Application/Application/*`
   - `src/Interception.Infrastructure/Infrastructure/*`
2. `Interception.UI` залишено як UI-шар (Blazor Components, сторінки, статичні ресурси) з посиланням на нові проєкти.
3. API-проєкти прив'язано до відповідних шарів:
   - `Interception.Services.DomainApi` -> `Interception.Domain`
   - `Interception.Services.ApplicationApi` -> `Interception.Application`
   - `Interception.Services.InfrastructureApi` -> `Interception.Infrastructure`
4. Збережено окремий WPF UI-проєкт `Interception.WpfClient`.
5. Оновлено solution-файл, щоб у рішенні були всі шари та сервіси.

## Цільова архітектура

- **Domain**: бізнес-сутності та правила.
- **Infrastructure**: EF Core, persistence, інтеграції.
- **Application**: use-cases, DTO, координація сценаріїв.
- **UI (Blazor/WPF)**: тільки презентація і виклики API.

## Рекомендована поетапна міграція до повних мікросервісів

1. Винести контракти API у shared contracts-бібліотеку.
2. Перенести вміст `Application` з direct EF доступу на HTTP-виклики до `InfrastructureApi`.
3. У `WPF` реалізувати MVVM + typed HTTP clients.
4. Додати авторизацію між сервісами та між UI і API.
5. Запакувати WPF як self-contained build для офлайн-інсталяції.

## Публікація WPF для флешки

```bash
bash scripts/publish-wpf-to-usb.sh /media/$USER/USB_DRIVE
```
