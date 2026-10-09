using NaSvyazi.Player;

namespace NaSvyazi.Interaction
{
    /// <summary>
    /// Контракт для любого объекта, с которым игрок может взаимодействовать:
    /// коробка, табличка, касса, терминал заказа, полка и т.д.
    /// Игрок не знает, что за объект перед ним, — он лишь вызывает эти методы.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Текст подсказки, например «Взять: Коробка с чехлами».</summary>
        string GetPrompt(PlayerInteractor interactor);

        /// <summary>Можно ли взаимодействовать прямо сейчас.</summary>
        bool CanInteract(PlayerInteractor interactor);

        /// <summary>Выполнить взаимодействие (клавиша E).</summary>
        void Interact(PlayerInteractor interactor);
    }
}
