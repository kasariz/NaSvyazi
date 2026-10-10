using System.Collections.Generic;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>Какое влияние оказывает мебель на салон.</summary>
    public enum FurnitureEffectType
    {
        ImpulseRack, // стойка у кассы: покупатели в очереди докупают мелочь
        WaitingArea, // кресла: в очереди рядом терпение тратится вдвое медленнее
        DemoTable,   // демо-стол: покупатели охотнее берут смартфоны и аудио
        AdStand,     // рекламный стенд: покупатели приходят чаще
        Decor        // декор: каждое утро немного растёт репутация
    }

    /// <summary>
    /// Эффект предмета мебели. Все активные эффекты хранятся в общем списке,
    /// а системы (покупатели, поток посетителей, смена дня) спрашивают у него,
    /// какие бонусы сейчас действуют. Пока мебель переставляют, эффект выключен.
    /// </summary>
    public class FurnitureEffect : MonoBehaviour
    {
        public static readonly List<FurnitureEffect> All = new List<FurnitureEffect>();

        [SerializeField] private FurnitureEffectType type;

        [Tooltip("Радиус действия для эффектов «рядом» (стойка у кассы, зона ожидания), м")]
        [SerializeField] private float radius = 4f;

        public FurnitureEffectType Type => type;
        public float Radius => radius;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public static int Count(FurnitureEffectType type)
        {
            int n = 0;
            foreach (FurnitureEffect e in All)
                if (e.type == type) n++;
            return n;
        }

        /// <summary>Ближайший предмет с эффектом в пределах его радиуса (по горизонтали) или null.</summary>
        public static FurnitureEffect NearestWithin(FurnitureEffectType type, Vector3 position)
        {
            FurnitureEffect best = null;
            float bestDistance = float.MaxValue;

            foreach (FurnitureEffect e in All)
            {
                if (e.type != type) continue;
                Vector3 d = e.transform.position - position;
                d.y = 0f;
                float distance = d.magnitude;
                if (distance <= e.radius && distance < bestDistance)
                {
                    best = e;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // --- Итоговые бонусы (с ограничением, чтобы нельзя было заставить зал одной мебелью) ---

        /// <summary>Множитель интервала появления покупателей: каждый стенд −15 %, учитываются до трёх.</summary>
        public static float SpawnIntervalMultiplier => Mathf.Pow(0.85f, Mathf.Min(Count(FurnitureEffectType.AdStand), 3));

        /// <summary>Прибавка к порогу терпимости цены для смартфонов и аудио (до двух демо-столов).</summary>
        public static float PremiumToleranceBonus => 0.08f * Mathf.Min(Count(FurnitureEffectType.DemoTable), 2);

        /// <summary>Ежедневная прибавка к репутации от декора (до трёх предметов).</summary>
        public static int DailyReputationBonus => Mathf.Min(Count(FurnitureEffectType.Decor), 3);
    }
}
