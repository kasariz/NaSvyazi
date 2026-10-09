using NaSvyazi.Interaction;
using NaSvyazi.Player;
using NaSvyazi.UI;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>Компьютер на прилавке: открывает окно заказа товара у поставщика.</summary>
    public class OrderTerminal : MonoBehaviour, IInteractable
    {
        [SerializeField] private OrderUI orderUI;

        public string GetPrompt(PlayerInteractor interactor) => "Открыть терминал заказа";

        public bool CanInteract(PlayerInteractor interactor) => orderUI != null && !ModalWindow.IsAnyOpen;

        public void Interact(PlayerInteractor interactor) => orderUI.Open(interactor.Controller);
    }
}
