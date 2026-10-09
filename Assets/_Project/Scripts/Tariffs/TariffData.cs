using UnityEngine;

namespace NaSvyazi.Tariffs
{
    /// <summary>
    /// Тарифный план (вымышленный, для игры). Описывает пакет интернета и минут
    /// и абонентскую плату. Создаётся как ассет: Assets → Create → На связи → Тариф.
    /// </summary>
    [CreateAssetMenu(fileName = "Tariff_", menuName = "На связи/Тариф", order = 10)]
    public class TariffData : ScriptableObject
    {
        public string id;
        public string displayName;

        [Tooltip("Абонентская плата в месяц, руб.")]
        [Min(0)] public int monthlyFee = 400;

        [Tooltip("Интернет, ГБ в месяц. −1 — безлимитный")]
        public int gigabytes = 10;

        [Tooltip("Минуты звонков в месяц")]
        [Min(0)] public int minutes = 300;

        [TextArea] public string description;

        public bool IsUnlimited => gigabytes < 0;

        public string InternetText => IsUnlimited ? "Безлимитный интернет" : $"{gigabytes} ГБ интернета";
        public string MinutesText => minutes > 0 ? $"{minutes} минут" : "Без звонков";

        /// <summary>Удовлетворяет ли тариф потребности клиента.</summary>
        public bool Satisfies(TariffNeed need)
        {
            bool internetOk = need.minGigabytes < 0
                ? IsUnlimited
                : IsUnlimited || gigabytes >= need.minGigabytes;
            bool minutesOk = minutes >= need.minMinutes;
            return internetOk && minutesOk;
        }
    }
}
