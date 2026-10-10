using System.Collections.Generic;
using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Расширение помещений. Торговый зал растёт в два шага (стены раздвигаются),
    /// затем открывается второй зал, который тоже можно расширить. Склад расширяется отдельно.
    /// Каждый этап добавляет участки пола для мебели и увеличивает аренду.
    /// Стены и пол всех этапов уже построены в сцене (StagePiece) — здесь они только включаются.
    /// </summary>
    [DefaultExecutionOrder(-200)] // раньше построения навигационной сетки
    public class ExpansionManager : MonoBehaviour
    {
        public static ExpansionManager Instance { get; private set; }

        public int SalonStage { get; private set; }
        public int StorageStage { get; private set; }

        private readonly List<StagePiece> pieces = new List<StagePiece>();

        private void Awake()
        {
            Instance = this;
            pieces.AddRange(FindObjectsByType<StagePiece>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int StageOf(LayoutZone zone) => zone == LayoutZone.Salon ? SalonStage : StorageStage;

        /// <summary>Следующий этап расширения или null, если всё уже открыто.</summary>
        public LayoutStage NextStage(LayoutZone zone)
        {
            LayoutStage[] stages = LayoutPlan.Stages(zone);
            int next = StageOf(zone) + 1;
            return next < stages.Length ? stages[next] : null;
        }

        /// <summary>Доплата к аренде за все купленные расширения.</summary>
        public int ExtraRent
        {
            get
            {
                int sum = 0;
                for (int i = 1; i <= SalonStage; i++) sum += LayoutPlan.Salon[i].extraRent;
                for (int i = 1; i <= StorageStage; i++) sum += LayoutPlan.Storage[i].extraRent;
                return sum;
            }
        }

        public bool TryUpgrade(LayoutZone zone)
        {
            LayoutStage next = NextStage(zone);
            if (next == null) return false;

            if (LevelManager.CurrentLevel < next.minLevel)
            {
                GameEvents.Notify($"Нужен уровень салона {next.minLevel}");
                SoundManager.Play(Sfx.Error);
                return false;
            }

            EconomyManager economy = EconomyManager.Instance;
            if (economy == null || !economy.TrySpend(next.price, next.name))
            {
                GameEvents.Notify("Недостаточно денег");
                SoundManager.Play(Sfx.Error);
                return false;
            }

            if (zone == LayoutZone.Salon) SalonStage++;
            else StorageStage++;

            Apply();
            SoundManager.Play(Sfx.Purchase);
            GameEvents.Notify($"{next.name} — готово! Аренда выросла на {EconomyManager.Format(next.extraRent)} в день.");
            GameEvents.RaiseLayoutChanged();
            return true;
        }

        /// <summary>Восстановить этапы (при загрузке).</summary>
        public void SetStages(int salon, int storage)
        {
            SalonStage = Mathf.Clamp(salon, 0, LayoutPlan.Salon.Length - 1);
            StorageStage = Mathf.Clamp(storage, 0, LayoutPlan.Storage.Length - 1);
            Apply();
            GameEvents.RaiseLayoutChanged();
        }

        /// <summary>Помещается ли прямоугольник (X, Z) целиком в открытую часть помещения.</summary>
        public bool Contains(LayoutZone zone, Rect r)
        {
            // Проверяем углы, середины сторон и центр: этого достаточно для залов из прямоугольников.
            float[] xs = { r.xMin, r.center.x, r.xMax };
            float[] zs = { r.yMin, r.center.y, r.yMax };
            foreach (float x in xs)
                foreach (float z in zs)
                    if (!ContainsPoint(zone, x, z)) return false;
            return true;
        }

        public bool ContainsPoint(LayoutZone zone, float x, float z)
        {
            const float eps = 0.001f;
            LayoutStage[] stages = LayoutPlan.Stages(zone);
            int stage = StageOf(zone);
            for (int i = 0; i <= stage && i < stages.Length; i++)
                foreach (Rect a in stages[i].areas)
                    if (x >= a.xMin - eps && x <= a.xMax + eps && z >= a.yMin - eps && z <= a.yMax + eps)
                        return true;
            return false;
        }

        private void Apply()
        {
            foreach (StagePiece piece in pieces)
                if (piece != null)
                    piece.gameObject.SetActive(piece.VisibleAt(StageOf(piece.Zone)));
        }
    }
}
