using System;
using NaSvyazi.Products;

namespace NaSvyazi.Core
{
    /// <summary>
    /// Шина игровых событий. Системы не ссылаются друг на друга напрямую,
    /// а подписываются на события: так табличка «Открыто» не знает
    /// о покупателях, а интерфейс — о том, кто именно потратил деньги.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Салон открыт (true) или закрыт (false).</summary>
        public static event Action<bool> StoreOpenChanged;

        /// <summary>Изменился баланс: (новый баланс, изменение).</summary>
        public static event Action<int, int> MoneyChanged;

        /// <summary>Короткое уведомление для игрока («Доставка прибыла»).</summary>
        public static event Action<string> Notification;

        /// <summary>Изменилась цена продажи товара: (товар, новая цена).</summary>
        public static event Action<ProductData, int> PriceChanged;

        /// <summary>Изменилась репутация салона (0–100).</summary>
        public static event Action<int> ReputationChanged;

        /// <summary>Игровое время: (день, минуты от полуночи, фаза дня).</summary>
        public static event Action<int, int, DayPhase> DayTimeChanged;

        /// <summary>Открыта новая категория товаров.</summary>
        public static event Action<ProductCategory> CategoryUnlocked;

        // --- События для обучения и прогрессии ---

        /// <summary>Оформлен заказ у поставщика.</summary>
        public static event Action OrderPlaced;

        /// <summary>Игрок взял коробку.</summary>
        public static event Action BoxPickedUp;

        /// <summary>Игрок выложил товар на полку.</summary>
        public static event Action ItemShelved;

        /// <summary>Открыто окно цены.</summary>
        public static event Action PriceWindowOpened;

        /// <summary>Покупатель обслужен (оплата или подключение тарифа).</summary>
        public static event Action SaleCompleted;

        /// <summary>Уровень салона: (уровень, опыт, опыт до следующего уровня).</summary>
        public static event Action<int, int, int> LevelChanged;

        /// <summary>Мебель куплена, переставлена или зал расширен — нужна перестройка навигации.</summary>
        public static event Action LayoutChanged;

        public static void RaiseStoreOpenChanged(bool isOpen) => StoreOpenChanged?.Invoke(isOpen);
        public static void RaiseMoneyChanged(int balance, int delta) => MoneyChanged?.Invoke(balance, delta);
        public static void Notify(string message) => Notification?.Invoke(message);
        public static void RaisePriceChanged(ProductData product, int price) => PriceChanged?.Invoke(product, price);
        public static void RaiseReputationChanged(int value) => ReputationChanged?.Invoke(value);
        public static void RaiseDayTimeChanged(int day, int minutes, DayPhase phase) => DayTimeChanged?.Invoke(day, minutes, phase);
        public static void RaiseCategoryUnlocked(ProductCategory category) => CategoryUnlocked?.Invoke(category);
        public static void RaiseOrderPlaced() => OrderPlaced?.Invoke();
        public static void RaiseBoxPickedUp() => BoxPickedUp?.Invoke();
        public static void RaiseItemShelved() => ItemShelved?.Invoke();
        public static void RaisePriceWindowOpened() => PriceWindowOpened?.Invoke();
        public static void RaiseSaleCompleted() => SaleCompleted?.Invoke();
        public static void RaiseLevelChanged(int level, int xp, int next) => LevelChanged?.Invoke(level, xp, next);
        public static void RaiseLayoutChanged() => LayoutChanged?.Invoke();
    }
}
