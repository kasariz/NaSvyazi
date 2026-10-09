using NaSvyazi.Core;
using NaSvyazi.Player;
using NaSvyazi.Store;
using TMPro;
using UnityEngine;

namespace NaSvyazi.Interaction
{
    /// <summary>
    /// Табличка «Открыто / Закрыто» у входа. Утром игрок открывает ею салон
    /// и начинает рабочий день; днём можно закрыть салон досрочно.
    /// Внешний вид таблички следует за событием открытия/закрытия салона.
    /// </summary>
    public class StoreSign : MonoBehaviour, IInteractable
    {
        [SerializeField] private Renderer signRenderer;
        [SerializeField] private Material openMaterial;
        [SerializeField] private Material closedMaterial;

        [Tooltip("Необязательная надпись TextMeshPro на табличке")]
        [SerializeField] private TMP_Text label;

        public bool IsOpen { get; private set; }

        private void OnEnable() => GameEvents.StoreOpenChanged += OnStoreOpenChanged;
        private void OnDisable() => GameEvents.StoreOpenChanged -= OnStoreOpenChanged;

        private void Start() => ApplyVisual();

        private void OnStoreOpenChanged(bool open)
        {
            IsOpen = open;
            ApplyVisual();
        }

        public string GetPrompt(PlayerInteractor interactor)
        {
            DayManager day = DayManager.Instance;
            if (day == null) return IsOpen ? "Закрыть салон" : "Открыть салон";

            switch (day.Phase)
            {
                case DayPhase.Preparing: return $"Открыть салон (начать день {day.Day})";
                case DayPhase.Open: return "Закрыть салон досрочно";
                default: return null;
            }
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            DayManager day = DayManager.Instance;
            return day == null || day.Phase == DayPhase.Preparing || day.Phase == DayPhase.Open;
        }

        public void Interact(PlayerInteractor interactor)
        {
            DayManager day = DayManager.Instance;
            if (day == null)
            {
                // Без менеджера дня — простое переключение (как в первых итерациях).
                GameEvents.RaiseStoreOpenChanged(!IsOpen);
                return;
            }

            if (day.Phase == DayPhase.Preparing) day.OpenStore();
            else if (day.Phase == DayPhase.Open) day.CloseEarly();
        }

        private void ApplyVisual()
        {
            if (signRenderer != null)
            {
                Material m = IsOpen ? openMaterial : closedMaterial;
                if (m != null) signRenderer.sharedMaterial = m;
            }

            if (label != null)
                label.text = IsOpen ? "ОТКРЫТО" : "ЗАКРЫТО";
        }
    }
}
