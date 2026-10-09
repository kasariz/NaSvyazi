using System;
using System.Collections.Generic;
using UnityEngine;

namespace NaSvyazi.Tariffs
{
    /// <summary>Потребность клиента, с которой он пришёл на консультацию.</summary>
    [Serializable]
    public class TariffNeed
    {
        [Tooltip("Что говорит клиент")]
        [TextArea] public string request;

        [Tooltip("Сколько ГБ нужно как минимум. −1 — нужен только безлимит")]
        public int minGigabytes;

        [Tooltip("Сколько минут нужно как минимум")]
        public int minMinutes;
    }

    /// <summary>Оценка выбора игрока.</summary>
    public enum ConsultationResult
    {
        Best,       // самый выгодный для клиента из подходящих
        Acceptable, // подходит, но есть дешевле — клиент переплачивает
        Unsuitable  // не закрывает потребность
    }

    /// <summary>
    /// Линейка тарифов и типовые запросы клиентов. Лучшим считается
    /// самый дешёвый тариф, полностью закрывающий потребность клиента:
    /// так игра поощряет честную консультацию, а не «впаривание» дорогого тарифа.
    /// </summary>
    [CreateAssetMenu(fileName = "TariffCatalog", menuName = "На связи/Каталог тарифов", order = 11)]
    public class TariffCatalog : ScriptableObject
    {
        public List<TariffData> tariffs = new List<TariffData>();
        public List<TariffNeed> needs = new List<TariffNeed>();

        /// <summary>Самый выгодный тариф для клиента (или null, если подходящих нет).</summary>
        public TariffData BestFor(TariffNeed need)
        {
            TariffData best = null;
            foreach (TariffData t in tariffs)
            {
                if (t == null || !t.Satisfies(need)) continue;
                if (best == null || t.monthlyFee < best.monthlyFee) best = t;
            }
            return best;
        }

        public ConsultationResult Evaluate(TariffNeed need, TariffData chosen)
        {
            if (chosen == null || !chosen.Satisfies(need)) return ConsultationResult.Unsuitable;
            TariffData best = BestFor(need);
            return best == null || chosen.monthlyFee <= best.monthlyFee
                ? ConsultationResult.Best
                : ConsultationResult.Acceptable;
        }

        public TariffNeed RandomNeed() =>
            needs.Count > 0 ? needs[UnityEngine.Random.Range(0, needs.Count)] : null;
    }
}
