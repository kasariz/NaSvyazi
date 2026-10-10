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

        /// <summary>Оплаты картой и наличными.</summary>
        public static int CardPayments;
        public static int CashPayments;

        /// <summary>Сколько раз игрок выдал лишнюю сдачу и на какую сумму.</summary>
        public static int ChangeErrors;
        public static int ChangeLoss;

        /// <summary>Консультации по тарифам: лучший вариант / подходящий, но дороже / неподходящий.</summary>
        public static int ConsultationsBest;
        public static int ConsultationsAcceptable;
        public static int ConsultationsFailed;

        /// <summary>Импульсные покупки со стойки у кассы.</summary>
        public static int ImpulseBuys;

        /// <summary>Кражи: сколько товаров и на какую сумму вынесли, сколько воров задержано.</summary>
        public static int TheftItems;
        public static int TheftLoss;
        public static int ThievesCaught;

        public static void ResetDay()
        {
            CustomersServed = 0;
            ItemsSold = 0;
            OutOfStockMisses = 0;
            PriceRefusals = 0;
            LeftQueue = 0;
            LeftEmptyHanded = 0;
            CardPayments = 0;
            CashPayments = 0;
            ChangeErrors = 0;
            ChangeLoss = 0;
            ConsultationsBest = 0;
            ConsultationsAcceptable = 0;
            ConsultationsFailed = 0;
            ImpulseBuys = 0;
            TheftItems = 0;
            TheftLoss = 0;
            ThievesCaught = 0;
        }
    }
}
