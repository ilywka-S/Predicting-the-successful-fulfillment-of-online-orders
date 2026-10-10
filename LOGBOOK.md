# Лог рішень (Logbook) — Проєкт OrderSense

1. **Проблема:** MAUI-проєкт не бачив згенерованого простору імен OrderSense.Client.Core.
   * **Рішення:** Додано ProjectReference від OrderSense.Maui до OrderSense.Client.Core.

2. **Проблема:** Конфлікт MAUI Resizetizer у тестових проєктах
Під час спроби запустити `dotnet test` для `OrderSense.Maui.Tests` виникала помилка дублювання файлів. Тестовий проєкт транзитивно тягнув правила генерації ресурсів з основного проєкту і намагався згенерувати іконки вдруге.
* **Рішення:** 
  - У тестовому `.csproj` жорстко вимкнено ресурси: додано `<EnableDefaultMauiItems>false</EnableDefaultMauiItems>` та `<SkipResizetizer>true</SkipResizetizer>`.
  - У `<ProjectReference>` для основного MAUI-додатка додано атрибут `<ExcludeAssets>buildTransitive</ExcludeAssets>`, щоб відсікти правила обробки картинок.

#### Приклад:
1. **Проблема:** Щось сталось.
   * **Рішення:** Якось відремонтували.
