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

        public static void RaiseStoreOpenChanged(bool isOpen) => StoreOpenChanged?.Invoke(isOpen);
        public static void RaiseMoneyChanged(int balance, int delta) => MoneyChanged?.Invoke(balance, delta);
        public static void Notify(string message) => Notification?.Invoke(message);
        public static void RaisePriceChanged(ProductData product, int price) => PriceChanged?.Invoke(product, price);
        public static void RaiseReputationChanged(int value) => ReputationChanged?.Invoke(value);
    }
}
