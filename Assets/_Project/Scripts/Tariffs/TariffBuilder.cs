using System;
using System.Collections.Generic;
using UnityEngine;

namespace NaSvyazi.Tariffs
{
    /// <summary>Дополнительные опции тарифа (флаги).</summary>
    [Flags]
    public enum TariffAddon
    {
        None = 0,
        Social = 1,   // соцсети и мессенджеры без трафика
        Video = 2,    // видео и музыка без трафика
        Hotspot = 4,  // раздача интернета на другие устройства
        Roaming = 8   // поездки по России без доплат
    }

    /// <summary>Собранный в конструкторе тариф: шаг интернета, шаг минут и опции.</summary>
    [Serializable]
    public struct TariffPlan
    {
        public int gbStep;
        public int minutesStep;
        public TariffAddon addons;

        public int Gigabytes => TariffBuilder.GbSteps[gbStep];
        public int Minutes => TariffBuilder.MinuteSteps[minutesStep];
        public int MonthlyFee => TariffBuilder.PriceOf(this);
        public bool Has(TariffAddon a) => (addons & a) != 0;
    }

    /// <summary>Оценка тарифа для клиента: довольство 0–100 % и что не так.</summary>
    public struct TariffEvaluation
    {
        public int satisfaction;
        public ConsultationResult result;
        public List<string> remarks;
    }

    /// <summary>
    /// Конструктор тарифов. Игрок собирает тариф из пакета интернета, пакета минут и опций,
    /// клиент оценивает его в процентах довольства:
    ///   • нехватка интернета, минут или нужной опции резко снижает довольство;
    ///   • переплата за лишнее снижает его плавно.
    /// Лучший результат — минимальный тариф, закрывающий все потребности (100 %).
    /// Можно «доупаковать» дороже — выручка выше, но довольство и репутация ниже.
    /// </summary>
    public static class TariffBuilder
    {
        public const int BaseFee = 120;

        /// <summary>Пакеты интернета, ГБ (−1 — безлимит) и их цена.</summary>
        public static readonly int[] GbSteps = { 0, 5, 10, 20, 30, 50, -1 };
        public static readonly int[] GbPrices = { 0, 90, 150, 230, 300, 400, 550 };

        /// <summary>Пакеты минут (−1 — безлимит) и их цена.</summary>
        public static readonly int[] MinuteSteps = { 0, 100, 300, 600, 1000, -1 };
        public static readonly int[] MinutePrices = { 0, 40, 90, 150, 220, 320 };

        public static readonly TariffAddon[] Addons = { TariffAddon.Social, TariffAddon.Video, TariffAddon.Hotspot, TariffAddon.Roaming };

        public static int AddonPrice(TariffAddon a)
        {
            switch (a)
            {
                case TariffAddon.Social: return 60;
                case TariffAddon.Video: return 110;
                case TariffAddon.Hotspot: return 70;
                case TariffAddon.Roaming: return 150;
                default: return 0;
            }
        }

        public static string AddonName(TariffAddon a)
        {
            switch (a)
            {
                case TariffAddon.Social: return "Соцсети без трафика";
                case TariffAddon.Video: return "Видео и музыка без трафика";
                case TariffAddon.Hotspot: return "Раздача интернета";
                case TariffAddon.Roaming: return "Поездки по России";
                default: return "";
            }
        }

        public static string GbText(int gb) => gb < 0 ? "Безлимит" : gb == 0 ? "Без интернета" : $"{gb} ГБ";
        public static string MinutesText(int min) => min < 0 ? "Безлимит" : min == 0 ? "Без звонков" : $"{min} мин";

        public static int PriceOf(TariffPlan plan)
        {
            int price = BaseFee + GbPrices[plan.gbStep] + MinutePrices[plan.minutesStep];
            foreach (TariffAddon a in Addons)
                if (plan.Has(a)) price += AddonPrice(a);
            return price;
        }

        /// <summary>Минимальный тариф, полностью закрывающий потребность клиента.</summary>
        public static TariffPlan IdealFor(TariffNeed need)
        {
            TariffPlan plan = new TariffPlan { addons = need.addons };
            plan.gbStep = MinStep(GbSteps, need.minGigabytes);
            plan.minutesStep = MinStep(MinuteSteps, need.minMinutes);
            return plan;
        }

        private static int MinStep(int[] steps, int needed)
        {
            if (needed < 0) return steps.Length - 1;           // нужен безлимит
            for (int i = 0; i < steps.Length; i++)
                if (steps[i] < 0 || steps[i] >= needed) return i;
            return steps.Length - 1;
        }

        /// <summary>Насколько тариф покрывает потребность (0–1): 1 — полностью.</summary>
        private static float Coverage(int offered, int needed, float unlimitedEquivalent)
        {
            if (needed == 0) return 1f;
            if (offered < 0) return 1f;                                      // предложен безлимит
            if (needed < 0) return Mathf.Clamp01(offered / unlimitedEquivalent) * 0.7f; // нужен безлимит, а дали пакет
            return Mathf.Clamp01((float)offered / needed);
        }

        public static TariffEvaluation Evaluate(TariffNeed need, TariffPlan plan)
        {
            List<string> remarks = new List<string>();

            float internet = Coverage(plan.Gigabytes, need.minGigabytes, 60f);
            float minutes = Coverage(plan.Minutes, need.minMinutes, 1500f);
            if (internet < 1f) remarks.Add(need.minGigabytes < 0 ? "Клиенту нужен безлимитный интернет" : "Не хватает интернета");
            if (minutes < 1f) remarks.Add(need.minMinutes < 0 ? "Клиенту нужны безлимитные звонки" : "Не хватает минут");

            // Нехватка — критична: квадрат доли покрытия.
            float score = internet * internet * minutes * minutes;

            foreach (TariffAddon a in Addons)
            {
                if ((need.addons & a) != 0 && !plan.Has(a))
                {
                    score *= 0.7f;
                    remarks.Add($"Нужна опция «{AddonName(a)}»");
                }
            }

            // Переплата: сравниваем с минимальным подходящим тарифом.
            int ideal = PriceOf(IdealFor(need));
            float ratio = (float)plan.MonthlyFee / Mathf.Max(1, ideal);
            if (ratio > 1.04f)
            {
                score *= Mathf.Clamp(1f - (ratio - 1.04f) * 1.1f, 0.25f, 1f);
                remarks.Add(ratio > 1.3f ? "Клиент сильно переплачивает за лишнее" : "Немного дороже, чем нужно");
            }

            int percent = Mathf.RoundToInt(Mathf.Clamp01(score) * 100f);
            ConsultationResult result = percent >= 90 ? ConsultationResult.Best
                : percent >= 65 ? ConsultationResult.Acceptable
                : ConsultationResult.Unsuitable;
            if (remarks.Count == 0) remarks.Add("Ровно то, что нужно клиенту");

            return new TariffEvaluation { satisfaction = percent, result = result, remarks = remarks };
        }

        /// <summary>Тариф, который соберёт кассир: с вероятностью accuracy — идеальный, иначе с ошибкой.</summary>
        public static TariffPlan AutoBuild(TariffNeed need, float accuracy)
        {
            TariffPlan plan = IdealFor(need);
            if (UnityEngine.Random.value <= accuracy) return plan;

            switch (UnityEngine.Random.Range(0, 3))
            {
                case 0: plan.gbStep = Mathf.Clamp(plan.gbStep + (UnityEngine.Random.value < 0.5f ? -1 : 1), 0, GbSteps.Length - 1); break;
                case 1: plan.minutesStep = Mathf.Clamp(plan.minutesStep + (UnityEngine.Random.value < 0.5f ? -1 : 1), 0, MinuteSteps.Length - 1); break;
                default: plan.addons ^= Addons[UnityEngine.Random.Range(0, Addons.Length)]; break;
            }
            return plan;
        }

        // ----------------------------------------------------------------
        // Запросы клиентов
        // ----------------------------------------------------------------

        /// <summary>
        /// Типовые клиенты. Подсказки в тексте: «соцсети/мессенджеры» → опция соцсетей,
        /// «видео/музыка/сериалы» → видео, «раздаю интернет» → раздача, «езжу по России» → поездки.
        /// </summary>
        public static readonly List<TariffNeed> Needs = new List<TariffNeed>
        {
            N("Весь день сижу в соцсетях и мессенджерах, звоню редко", 10, 100, TariffAddon.Social),
            N("Смотрю сериалы в метро и слушаю музыку, звоню мало", 20, 100, TariffAddon.Video),
            N("Много разговариваю с клиентами, интернет почти не нужен", 5, 1000),
            N("Мне самый дешёвый: позвонить родным и иногда проверить почту", 5, 100),
            N("Работаю в дороге: нужен безлимитный интернет, и я раздаю его на ноутбук", -1, 300, TariffAddon.Hotspot),
            N("Часто езжу в командировки по России, звоню и сижу в интернете", 20, 600, TariffAddon.Roaming),
            N("SIM-карта для планшета, звонить с неё не буду", 30, 0),
            N("Ребёнку в школу: немного интернета и звонки родителям", 5, 300),
            N("Бабушке: только звонки, интернет ей не нужен", 0, 300),
            N("Я стример: постоянно в видео и соцсетях, звонки неважны", 50, 100, TariffAddon.Video | TariffAddon.Social),
            N("Звоню без остановки, а вечером смотрю видео", 30, -1, TariffAddon.Video),
            N("Блогер в разъездах по России: безлимитный интернет и соцсети", -1, 600, TariffAddon.Social | TariffAddon.Roaming),
            N("Дома нет кабельного интернета — буду раздавать с телефона на компьютер", 50, 100, TariffAddon.Hotspot),
            N("Работаю в такси: навигатор и очень много звонков", 10, 1000),
            N("Студент: соцсети, музыка в дороге, звоню редко", 20, 100, TariffAddon.Social | TariffAddon.Video),
        };

        private static TariffNeed N(string request, int gb, int minutes, TariffAddon addons = TariffAddon.None) =>
            new TariffNeed { request = request, minGigabytes = gb, minMinutes = minutes, addons = addons };

        public static TariffNeed RandomNeed() => Needs[UnityEngine.Random.Range(0, Needs.Count)];

        /// <summary>Точные потребности клиента (подсказка навыка «Знаток тарифов»).</summary>
        public static string Describe(TariffNeed need)
        {
            string s = $"Интернет: {(need.minGigabytes < 0 ? "безлимит" : need.minGigabytes == 0 ? "не нужен" : $"от {need.minGigabytes} ГБ")}" +
                       $"   ·   Минуты: {(need.minMinutes < 0 ? "безлимит" : need.minMinutes == 0 ? "не нужны" : $"от {need.minMinutes}")}";
            foreach (TariffAddon a in Addons)
                if ((need.addons & a) != 0) s += $"   ·   {AddonName(a)}";
            return s;
        }
    }
}
