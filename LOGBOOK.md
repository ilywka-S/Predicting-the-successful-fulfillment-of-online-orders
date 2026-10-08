# Лог рішень (Logbook) — Проєкт OrderSense

1. **Проблема:** MAUI-проєкт не бачив згенерованого простору імен OrderSense.Client.Core.
   * **Рішення:** Додано ProjectReference від OrderSense.Maui до OrderSense.Client.Core.

2. **Проблема:** Компілятор не бачив згенерований OrderClient у MAUI-проєкті, а згодом виникала помилка відсутності NuGet-пакета Newtonsoft.Json.
   * **Рішення:** Виправлено структуру та розташування тегів <ProjectReference> у OrderSense.Maui.csproj, встановлено пакет Newtonsoft.Json для коректної роботи згенерованого NSwag-клієнта.

#### Приклад:
1. **Проблема:** Щось сталось.
   * **Рішення:** Якось відремонтували.
