using NaSvyazi.Core;
using UnityEngine;

namespace NaSvyazi.Economy
{
    /// <summary>
    /// Репутация салона (0–100). Растёт, когда покупателей быстро обслуживают
    /// и правильно консультируют; падает, когда товара нет, очередь стоит
    /// или клиенту подобрали неподходящий тариф. От репутации зависит,
    /// как часто приходят новые покупатели, — получается обратная связь
    /// между качеством обслуживания и выручкой.
    /// </summary>
    public class ReputationManager : MonoBehaviour
    {
        public static ReputationManager Instance { get; private set; }

        [SerializeField] private int startReputation = 50;

        public int Value { get; private set; }

        /// <summary>Нормированное значение 0…1.</summary>
        public float Normalized => Value / 100f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
            Value = Mathf.Clamp(startReputation, 0, 100);
        }

        private void Start() => GameEvents.RaiseReputationChanged(Value);

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Change(int delta, string reason)
        {
            int old = Value;
            Value = Mathf.Clamp(Value + delta, 0, 100);
            if (Value == old) return;

            Debug.Log($"[Репутация] {(delta > 0 ? "+" : "")}{delta} ({reason}). Сейчас: {Value}");
            GameEvents.RaiseReputationChanged(Value);
        }

        /// <summary>Изменить репутацию, если менеджер есть на сцене.</summary>
        public static void Add(int delta, string reason)
        {
            if (Instance != null) Instance.Change(delta, reason);
        }

        /// <summary>Множитель интервала появления покупателей: высокая репутация — чаще.</summary>
        public static float SpawnIntervalMultiplier =>
            Instance != null ? Mathf.Lerp(1.6f, 0.6f, Instance.Normalized) : 1f;
    }
}
