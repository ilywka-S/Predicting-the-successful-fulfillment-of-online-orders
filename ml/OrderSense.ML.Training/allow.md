# OrderSense 
Момент прогнозу — `order_approved_at`. Дозволено лише те, що відомо на цей момент.

| Файл | Колонка | Статус | Чому |
|---|---|---|---|
| orders | `order_id` | не потрібно | ідентифікатор, не ознака |
| orders | `customer_id` | не потрібно | ідентифікатор, не ознака (хіба що для зв'язку з customers) |
| orders | `order_status` | **заборонено** | фінальний статус, невідомий на момент прогнозу; крім того — джерело цільової змінної |
| orders | `order_purchase_timestamp` | **дозволено** | відомо на момент прогнозу; джерело для день тижня/години/місяця |
| orders | `order_approved_at` | **дозволено** | це сам момент прогнозу |
| orders | `order_delivered_carrier_date` | **заборонено** | станеться в майбутньому відносно моменту прогнозу |
| orders | `order_delivered_customer_date` | **заборонено** | те саме + джерело цільової змінної |
| orders | `order_estimated_delivery_date` | **дозволено** | обіцяна дата відома заздалегідь |
| order_items | `order_id` | не потрібно | ключ зв'язку |
| order_items | `order_item_id` | не потрібно | номер позиції, не ознака |
| order_items | `product_id` | не потрібно | ідентифікатор (але через нього береться категорія/вага з products) |
| order_items | `seller_id` | не потрібно | ідентифікатор (через нього — seller_state) |
| order_items | `shipping_limit_date` | **дозволено** | встановлюється одразу, відомо на момент прогнозу |
| order_items | `price` | **дозволено** | відомо на момент прогнозу |
| order_items | `freight_value` | **дозволено** | відомо на момент прогнозу |
| products | `product_category_name` | **дозволено** | властивість товару, відома заздалегідь |
| products | `product_weight_g` | **дозволено** | — |
| products | `product_length_cm` / `height_cm` / `width_cm` | **дозволено** | габарити, для об'єму |
| products | `product_photos_qty` | не потрібно | не входить у контракт ознак (розділ 7) |
| sellers | `seller_zip_code_prefix` | не потрібно | проміжне — через нього рахується відстань |
| sellers | `seller_state` | **дозволено** | — |
| customers | `customer_zip_code_prefix` | не потрібно | проміжне — для відстані |
| customers | `customer_state` | **дозволено** | — |
| payments | `payment_type` | **дозволено** | — |
| payments | `payment_installments` | **дозволено** | — |
| payments | `payment_value` | не потрібно | дублює по суті total_price з order_items; в контракті його немає |
| reviews | усі колонки | **заборонено** | явно виключено в розділі 3 ("не використовуємо") — відгук з'являється вже після доставки |
| geolocation | `geolocation_lat` / `geolocation_lng` | **дозволено** | для розрахунку відстані продавець↔клієнт |