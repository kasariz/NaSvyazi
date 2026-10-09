using NaSvyazi.Interaction;
using NaSvyazi.Player;
using NaSvyazi.Products;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>Мусорный бак: сюда выбрасываются пустые коробки.</summary>
    public class TrashBin : MonoBehaviour, IInteractable
    {
        public string GetPrompt(PlayerInteractor interactor) => "Выбросить пустую коробку";

        public bool CanInteract(PlayerInteractor interactor) =>
            interactor.Carry.Held is ProductBox box && box.IsEmpty;

        public void Interact(PlayerInteractor interactor)
        {
            Carryable item = interactor.Carry.Release();
            if (item != null) Destroy(item.gameObject);
        }
    }
}
