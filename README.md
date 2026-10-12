# OrderSense

OrderSense бере онлайн-замовлення і оцінює, наскільки ймовірно, що його доставлять вчасно і не скасують. Поруч із ймовірністю система показує, які саме чинники вплинули на оцінку: відстань між продавцем і покупцем, обіцяний термін доставки, частка вартості доставки тощо. Працюємо на відкритому датасеті бразильського маркетплейсу Olist: це близько 100 тисяч замовлень за 2016–2018 роки.

Клієнт - один застосунок .NET MAUI, який ми збираємо під Android і Windows.

## Команда

| | Зона відповідальності |
|---|---|
| Артем ([@flxnsee](https://github.com/flxnsee)) | Core API, база даних і міграції, Aspire, розгортання (Render, Neon) |
| Ольга ([@Olga-Yezerska](https://github.com/Olga-Yezerska)) | аналіз даних, ознаки, навчання моделі, Prediction Service, імпорт Olist |
| Ілля ([@ilywka-S](https://github.com/ilywka-S)) | архітектура застосунку, `Client.Core`, репозиторій і CI |
| Анна ([@lia945785](https://github.com/lia945785)) | дизайн, екрани, дизайн-система, тестування, звіт |

## Запуск з нуля

Docker не потрібен. Інструкція розрахована на Windows 11

### Що встановити

- .NET SDK 10: `winget install Microsoft.DotNet.SDK.10`
- PostgreSQL 18: `winget install PostgreSQL.PostgreSQL.18 --interactive` (порт 5432, пароль запам'ятайте). Замість локальної бази можна взяти власну гілку в Neon — рядок підключення дає Артем.
- Для застосунку: MAUI workload. Ставиться з PowerShell від адміністратора: `dotnet workload install maui-windows maui-android`. Якщо збірка попросить ще якийсь workload, `dotnet workload restore src/OrderSense.Maui/OrderSense.Maui.csproj` поставить усе потрібне.
- IDE: Rider або Visual Studio 2026. Visual Studio 2022 з .NET 10 не працює.

Один раз довіряємо локальному HTTPS-сертифікату:

```
dotnet dev-certs https --trust
```

### 1. Клонування

```
git clone https://github.com/ilywka-S/Predicting-the-successful-fulfillment-of-online-orders.git
cd Predicting-the-successful-fulfillment-of-online-orders
```

### 2. Секрети

Паролі й ключі в репозиторії не зберігаються. Кожен задає їх у себе через user-secrets проєкту AppHost (команди виконуються з кореня репозиторію):

```
dotnet user-secrets set "ConnectionStrings:ordersense" "Host=localhost;Port=5432;Database=ordersense;Username=postgres;Password=<пароль PostgreSQL>" --project src/OrderSense.AppHost
dotnet user-secrets set "Parameters:jwt-key" "<будь-які 32+ випадкові символи>" --project src/OrderSense.AppHost
dotnet user-secrets set "Parameters:admin-email" "admin@ordersense.local" --project src/OrderSense.AppHost
dotnet user-secrets set "Parameters:admin-password" "<пароль адміна>" --project src/OrderSense.AppHost
```

Ключ JWT зручно згенерувати в Git Bash: `openssl rand -base64 48`.
Пароль адміна має бути не коротшим за 8 символів і містити велику літеру, малу літеру й цифру. Інакше API впаде на старті з повідомленням `Cannot create admin user`.

Кількість демо-замовлень за замовчуванням 500 (`src/OrderSense.AppHost/appsettings.Development.json`). Змінити можна так: `dotnet user-secrets set "Parameters:demo-orders" "1000" --project src/OrderSense.AppHost`.

### 3. Бекенд

```
dotnet run --project src/OrderSense.AppHost
```

У консолі з'явиться посилання на дашборд Aspire (`https://localhost:17298/login?t=...`). Там видно сервіси `api` і `prediction`, їхні логи й трейси.

Під час першого запуску API сам створює базу, застосовує міграції, створює ролі, адміністратора і 500 демо-замовлень. У логах це рядок `Seeded 500 demo orders`. Повідомлення `fail: ...[20004]` на самому першому запуску нормальне: EF перевіряє, чи існує база, і створює її. Демо-дані з'являються тільки в Development і тільки в порожній базі.

API слухає `https://localhost:7250` і `http://localhost:5128`. OpenAPI-документ: `https://localhost:7250/openapi/v1.json`.

Швидка перевірка з PowerShell:

```powershell
$login = Invoke-RestMethod -Method Post http://localhost:5128/api/auth/login -ContentType "application/json" -Body '{"email":"admin@ordersense.local","password":"<пароль адміна>"}'
Invoke-RestMethod "http://localhost:5128/api/orders?pageSize=3" -Headers @{ Authorization = "Bearer $($login.accessToken)" }
```

### 4. Застосунок

Адреса API залежить від збірки:

| Збірка | Куди звертається застосунок | Ким входити |
|---|---|---|
| Debug | локальне API з кроку 3 | адміністратор із user-secrets |
| Release | `https://ordersense-api.onrender.com` | продакшн-адміністратор |

**Windows.** Збираємо і запускаємо (для Debug бекенд з кроку 3 має працювати):

```
dotnet build src/OrderSense.Maui/OrderSense.Maui.csproj -f net10.0-windows10.0.19041.0
.\src\OrderSense.Maui\bin\Debug\net10.0-windows10.0.19041.0\win-x64\OrderSense.Maui.exe
```

Для Release додайте `-c Release` і запускайте файл із `bin\Release\...`.

Зверніть увагу: `dotnet build -t:Run` без попередньої збірки падає з кодом 9009, бо `.exe` ще немає.

**Android.** На телефон ставимо Release-збірку, вона працює з API на Render:

```
dotnet publish src/OrderSense.Maui/OrderSense.Maui.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```

Готовий файл `*-Signed.apk` лежить у `src/OrderSense.Maui/bin/Release/net10.0-android/publish/`. Перед встановленням нової версії стару краще видалити.

### 5. Тести

Тести API ходять у справжню PostgreSQL: кожен прогін створює тимчасову базу `ordersense_test_<guid>` і видаляє її після себе. Тому один раз вказуємо сервер:

```
dotnet user-secrets set "ConnectionStrings:TestPostgres" "Host=localhost;Port=5432;Username=postgres;Password=<пароль PostgreSQL>" --project tests/OrderSense.Api.Tests
```

Далі:

```
dotnet test OrderSense.Backend.slnf
dotnet test tests/OrderSense.Client.Core.Tests
```

Якщо локальної PostgreSQL немає, підійде гілка `tests` у Neon або контейнер `postgres:18`, якщо у вас є Docker. На кожен pull request CI запускає ці ж тести на `postgres:18`, а збірки MAUI для Android і Windows — коли змінено застосунок або `Client.Core`.

## Дані й модель

1. Завантажте датасет [Brazilian E-Commerce Public Dataset by Olist](https://www.kaggle.com/datasets/olistbr/brazilian-ecommerce) з Kaggle і розпакуйте 9 CSV-файлів у `data/olist/`. CSV у git не потрапляють (правило `*.csv` у `.gitignore`).
2. Запустіть навчання з теки проєкту, бо шляхи до даних відносні:

```
cd ml/OrderSense.ML.Training
dotnet run
```

Програма будує «картку» кожного замовлення (`data/processed/order_cards.csv`), рахує статистику за місяцями, навчає FastTree, виводить метрики й зберігає модель у `ml/models/v1/model.zip`.

Що ми вважаємо успіхом: замовлення має статус `delivered`, і дата доставки не пізніша за обіцяну (порівнюємо дати без часу). Проблема: `canceled`, `unavailable` або запізнення. Проміжні статуси в навчання не йдуть.

Прогноз робимо в момент підтвердження оплати (`order_approved_at`), тому модель бачить тільки те, що відомо на цей момент. Фактичні дати доставки, відгуки й фінальний статус виключені, повна таблиця — в [`allow.md`](ml/OrderSense.ML.Training/allow.md). Навчаємо на замовленнях з 01.2017 по 08.2018, чому саме так — у [`working-period.md`](ml/OrderSense.ML.Training/working-period.md).

Модель v1 поки слабка: на тестових даних ROC-AUC 0,60, PR-AUC 0,064 проти 0,040 у базової моделі. Сигнал є, але калібрування, підбір ознак і AutoML ще попереду (до 10.11).

## API

| Метод | Шлях | Хто має доступ |
|---|---|---|
| POST | `/api/auth/login` | усі |
| POST | `/api/auth/refresh` | усі (за refresh-токеном) |
| POST | `/api/auth/logout` | усі |
| GET | `/api/auth/me` | будь-яка роль |
| GET | `/api/orders` | будь-яка роль |
| GET | `/api/orders/{id}` | будь-яка роль |
| GET, POST | `/api/users` | `admin` |
| GET | `/alive` | усі, без токена |

Ролі: `manager`, `analyst`, `admin`. Фільтри списку: `status`, `from`, `to`, `customerState`, `riskLevel`, `search`, `sort`, `page`, `pageSize` (до 100).

Повний контракт лежить у [`src/OrderSense.Api/openapi/OrderSense.Api.json`](src/OrderSense.Api/openapi/OrderSense.Api.json) і оновлюється при кожній збірці API. Клієнт для застосунку генерується з цього файлу: `src/OrderSense.Client.Core/generate-client.bat`.

## Продакшн

- API: `https://ordersense-api.onrender.com`, перевірка: `/alive`.
- Render (безкоштовний план, Франкфурт) збирає Docker-образ з `src/OrderSense.Api/Dockerfile`. Поки що деплой запускаємо вручну, автодеплой після злиття — у планах.
- База: окремий проєкт Neon `ordersense-prod`, PostgreSQL 18, Франкфурт. Рядок підключення, ключ JWT і пароль адміна задані лише в змінних середовища Render.
- Безкоштовний Render засинає після 15 хвилин без запитів, і тоді перший запит іде до хвилини. Щоб цього не було вдень, cron-job.org смикає `/alive` кожні 10 хвилин з 07:00 до 23:59 за Києвом.
- Демо-дані в продакшні не створюються, тож до імпорту Olist список замовлень там порожній.