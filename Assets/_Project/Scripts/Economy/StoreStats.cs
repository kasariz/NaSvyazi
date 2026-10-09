namespace NaSvyazi.Economy
{
    /// <summary>
    /// Статистика работы салона за текущий день — для итогового отчёта
    /// и для раздела о тестировании (сколько покупателей ушло и почему).
    /// </summary>
    public static class StoreStats
    {
        /// <summary>Обслужено покупателей (оплатили покупку).</summary>
        public static int CustomersServed;

        /// <summary>Продано единиц товара.</summary>
        public static int ItemsSold;

        /// <summary>Сколько раз покупатель не нашёл нужный товар.</summary>
        public static int OutOfStockMisses;

        /// <summary>Сколько раз покупатель отказался от товара из-за цены.</summary>
        public static int PriceRefusals;

        /// <summary>Ушли из очереди, не дождавшись кассы.</summary>
        public static int LeftQueue;

        /// <summary>Ушли без покупок.</summary>
        public static int LeftEmptyHanded;

        public static void ResetDay()
        {
            CustomersServed = 0;
            ItemsSold = 0;
            OutOfStockMisses = 0;
            PriceRefusals = 0;
            LeftQueue = 0;
            LeftEmptyHanded = 0;
        }
    }
}
