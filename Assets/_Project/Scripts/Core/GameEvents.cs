using System;

namespace NaSvyazi.Core
{
    /// <summary>
    /// Шина игровых событий. Системы не ссылаются друг на друга напрямую,
    /// а подписываются на события: так табличка «Открыто» не знает
    /// о покупателях, а покупатели не знают о табличке.
    /// Новые события будут добавляться сюда по мере разработки.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Салон открыт (true) или закрыт (false).</summary>
        public static event Action<bool> StoreOpenChanged;

        public static void RaiseStoreOpenChanged(bool isOpen) => StoreOpenChanged?.Invoke(isOpen);
    }
}
