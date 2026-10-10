using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>Помещение, которое можно расширять.</summary>
    public enum LayoutZone
    {
        Salon,  // торговый зал (и второй зал)
        Storage // склад
    }

    /// <summary>Этап расширения помещения: что даёт, сколько стоит, какие участки пола добавляет.</summary>
    public class LayoutStage
    {
        public string name;
        public string description;
        public int price;
        public int minLevel;
        public int extraRent;   // прибавка к ежедневной аренде
        public Rect[] areas;    // новые участки пола (X, Z), где можно ставить мебель
    }

    /// <summary>
    /// План здания. Одни и те же числа использует и игра (где можно ставить мебель,
    /// сколько стоит расширение), и редактор (где строить стены и пол каждого этапа).
    ///
    /// Вид сверху (+Z — от улицы вглубь здания):
    ///
    ///              z=12 ┌────────┐
    ///                   │ 2-й зал│  этап 4
    ///               z=7 ├────────┤
    ///   z=4 ┌─────┐     │ 2-й зал│  этап 3
    ///       │склад│ z=0 ├──┬арка┬┤
    ///       │  +2 ├─────┴──┴────┴┤
    ///  z=-1 │  +1 │ салон │+1│+2 │
    ///  z=-5 │склад│       │  │   │
    /// z=-10 └─ворота─┴──вход──┴──┴──┘  ← витрина на улицу
    ///      x=-14  x=-8      x=0 x=4 x=8
    /// </summary>
    public static class LayoutPlan
    {
        public const float FrontZ = -10f;
        public const float WallHeight = 3f;

        public static Rect Area(float xMin, float zMin, float xMax, float zMax) =>
            Rect.MinMaxRect(xMin, zMin, xMax, zMax);

        /// <summary>Торговый зал: стартовый, два расширения, второй зал и его расширение.</summary>
        public static readonly LayoutStage[] Salon =
        {
            new LayoutStage
            {
                name = "Салон", description = "Небольшой зал 8 × 10 м.",
                areas = new[] { Area(-8f, -10f, 0f, 0f) }
            },
            new LayoutStage
            {
                name = "Расширение зала", description = "Зал становится шире на 4 м — место ещё под несколько стеллажей.",
                price = 8000, minLevel = 2, extraRent = 300,
                areas = new[] { Area(0f, -10f, 4f, 0f) }
            },
            new LayoutStage
            {
                name = "Расширение зала II", description = "Ещё 4 м к востоку: витрина салона растягивается на всю улицу.",
                price = 15000, minLevel = 3, extraRent = 400,
                areas = new[] { Area(4f, -10f, 8f, 0f) }
            },
            new LayoutStage
            {
                name = "Второй зал", description = "Новая комната 8 × 7 м за аркой в задней стене зала.",
                price = 25000, minLevel = 4, extraRent = 700,
                areas = new[] { Area(0f, 0f, 8f, 7f) }
            },
            new LayoutStage
            {
                name = "Расширение второго зала", description = "Второй зал вырастает до 8 × 12 м.",
                price = 30000, minLevel = 5, extraRent = 500,
                areas = new[] { Area(0f, 7f, 8f, 12f) }
            },
        };

        /// <summary>Склад: стартовый и два расширения вглубь здания.</summary>
        public static readonly LayoutStage[] Storage =
        {
            new LayoutStage
            {
                name = "Склад", description = "Небольшой склад 6 × 5 м.",
                areas = new[] { Area(-14f, -10f, -8f, -5f) }
            },
            new LayoutStage
            {
                name = "Расширение склада", description = "Склад вырастает вглубь до 6 × 9 м.",
                price = 4000, minLevel = 2, extraRent = 150,
                areas = new[] { Area(-14f, -5f, -8f, -1f) }
            },
            new LayoutStage
            {
                name = "Расширение склада II", description = "Склад 6 × 14 м — хватит места для больших поставок.",
                price = 9000, minLevel = 3, extraRent = 250,
                areas = new[] { Area(-14f, -1f, -8f, 4f) }
            },
        };

        public static LayoutStage[] Stages(LayoutZone zone) => zone == LayoutZone.Salon ? Salon : Storage;

        // --- Проёмы ---

        /// <summary>Вход в салон (в витрине, z = −10).</summary>
        public const float DoorXMin = -2.9f, DoorXMax = -1.1f;

        /// <summary>Ворота склада на улицу (z = −10).</summary>
        public const float GateXMin = -12.5f, GateXMax = -10f;

        /// <summary>Дверь между складом и залом (стена x = −8).</summary>
        public const float InnerDoorZMin = -7.2f, InnerDoorZMax = -6f;

        /// <summary>Арка во второй зал (задняя стена зала, z = 0).</summary>
        public const float ArchXMin = 2.5f, ArchXMax = 5.5f;

        // --- Точки ---

        public static readonly Vector3 CounterPosition = new Vector3(-4f, 0f, -2f);
        public static readonly Vector3 RegisterPosition = new Vector3(-4.8f, 1f, -2f);
        public static readonly Vector3 TerminalPosition = new Vector3(-3.1f, 1f, -2f);
        public static readonly Vector3 QueuePosition = new Vector3(-4.8f, 0f, -3f);
        public static readonly Vector3 CashierPosition = new Vector3(-4.8f, 0f, -1.25f);
        public static readonly Vector3 SignPosition = new Vector3(-1f, 1.6f, -0.125f);
        public static readonly Vector3 CustomerSpawn = new Vector3(-2f, 0f, -17f);
        public static readonly Vector3 DeliveryPoint = new Vector3(-11.25f, 0f, -12.8f);
        public static readonly Vector3 PlayerStart = new Vector3(-4f, 0f, -7f);

        /// <summary>Тротуар перед зданием — сюда можно вынести рекламный стенд.</summary>
        public static readonly Rect Street = Area(-19.8f, -17.8f, 13.8f, -10.25f);

        /// <summary>Проходы, которые нельзя загораживать мебелью.</summary>
        public static readonly Rect[] KeepClear =
        {
            Area(-3.5f, -10.5f, -0.5f, -8.3f),  // вход в салон изнутри
            Area(-3.5f, -18f, -0.5f, -10f),     // дорожка от улицы к входу
            Area(-13f, -14.2f, -9.5f, -8.6f),   // ворота склада и зона доставки
            Area(-9.2f, -7.6f, -6.8f, -5.6f),   // дверь между складом и залом
            Area(2.2f, -1.4f, 5.8f, 1.4f),      // арка во второй зал
            Area(-5.5f, -8.2f, -4.1f, -2.4f),   // очередь к кассе
            Area(-6f, -2.4f, -2f, -0.1f),       // место кассира за прилавком
        };
    }
}
