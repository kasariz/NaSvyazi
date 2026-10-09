using NaSvyazi.Core;
using NaSvyazi.Player;
using TMPro;
using UnityEngine;

namespace NaSvyazi.Interaction
{
    /// <summary>
    /// Табличка «Открыто / Закрыто» у входа. Открывая салон, игрок начинает
    /// рабочий день: позже на это событие подпишется генератор покупателей.
    /// </summary>
    public class StoreSign : MonoBehaviour, IInteractable
    {
        [SerializeField] private Renderer signRenderer;
        [SerializeField] private Material openMaterial;
        [SerializeField] private Material closedMaterial;

        [Tooltip("Необязательная надпись TextMeshPro на табличке")]
        [SerializeField] private TMP_Text label;

        public bool IsOpen { get; private set; }

        private void Start() => ApplyVisual();

        public string GetPrompt(PlayerInteractor interactor) => IsOpen ? "Закрыть салон" : "Открыть салон";

        public bool CanInteract(PlayerInteractor interactor) => true;

        public void Interact(PlayerInteractor interactor)
        {
            IsOpen = !IsOpen;
            ApplyVisual();
            GameEvents.RaiseStoreOpenChanged(IsOpen);
            Debug.Log(IsOpen ? "Салон открыт" : "Салон закрыт");
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
