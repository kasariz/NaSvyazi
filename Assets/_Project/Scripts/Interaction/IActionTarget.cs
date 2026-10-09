using NaSvyazi.Player;

namespace NaSvyazi.Interaction
{
    /// <summary>
    /// Объект, поддерживающий действия мышью: ЛКМ (основное) и ПКМ (дополнительное).
    /// Например, полка: ЛКМ — выложить товар из коробки, ПКМ — убрать обратно.
    /// Если метод подсказки возвращает null, действие сейчас недоступно.
    /// </summary>
    public interface IActionTarget
    {
        string GetPrimaryPrompt(PlayerInteractor interactor);
        void DoPrimary(PlayerInteractor interactor);

        string GetSecondaryPrompt(PlayerInteractor interactor);
        void DoSecondary(PlayerInteractor interactor);
    }
}
