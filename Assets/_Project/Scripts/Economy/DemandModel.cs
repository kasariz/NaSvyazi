using UnityEngine;

namespace NaSvyazi.Economy
{
    /// <summary>
    /// Модель ценовой чувствительности покупателей.
    ///
    /// Вероятность покупки зависит от отношения цены салона к рыночной цене
    /// r = цена / рыночная цена и описывается логистической функцией:
    ///
    ///     P(r) = 1 / (1 + e^(k · (r − t)))
    ///
    /// где k — крутизна кривой (насколько резко падает спрос),
    ///     t — порог терпимости покупателя (при r = t вероятность равна 50 %).
    ///
    /// У каждого покупателя свой порог t: экономные уходят уже при небольшой
    /// наценке, обеспеченные готовы платить больше. Поэтому игроку выгодно
    /// искать баланс между наценкой и количеством продаж.
    /// </summary>
    public static class DemandModel
    {
        /// <summary>Крутизна кривой спроса k.</summary>
        public const float Steepness = 12f;

        /// <summary>Порог терпимости «среднего» покупателя.</summary>
        public const float AverageTolerance = 1.25f;

        /// <summary>Диапазон порогов терпимости для случайных покупателей.</summary>
        public const float MinTolerance = 1.1f;
        public const float MaxTolerance = 1.4f;

        /// <summary>Вероятность покупки (0…1) при данной цене.</summary>
        public static float BuyProbability(int price, int marketPrice, float tolerance)
        {
            if (marketPrice <= 0) return 1f;
            float ratio = (float)price / marketPrice;
            return 1f / (1f + Mathf.Exp(Steepness * (ratio - tolerance)));
        }

        /// <summary>Вероятность покупки для среднего покупателя — подсказка игроку.</summary>
        public static float AverageBuyProbability(int price, int marketPrice) =>
            BuyProbability(price, marketPrice, AverageTolerance);

        /// <summary>Случайный порог терпимости нового покупателя.</summary>
        public static float RandomTolerance() => Random.Range(MinTolerance, MaxTolerance);
    }
}
