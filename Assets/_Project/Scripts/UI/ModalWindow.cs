using NaSvyazi.Core;
using NaSvyazi.Player;
using UnityEngine;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Базовый класс окон, которые перехватывают управление (терминал заказа,
    /// касса, отчёт за день). Пока окно открыто, персонаж стоит, курсор свободен.
    /// Одновременно может быть открыто только одно окно. Esc закрывает окно.
    /// </summary>
    public abstract class ModalWindow : MonoBehaviour
    {
        /// <summary>Открытое окно или null.</summary>
        public static ModalWindow Current { get; private set; }

        /// <summary>Номер кадра, в котором закрылось окно, — чтобы тот же Esc не открыл паузу.</summary>
        public static int LastClosedFrame { get; private set; } = -1;

        public static bool IsAnyOpen => Current != null;

        protected PlayerController Player { get; private set; }

        public bool IsOpen => Current == this;

        public void Open(PlayerController player)
        {
            if (Current != null) return;

            Current = this;
            Player = player;
            if (Player != null) Player.SetInputLocked(true);
            SetVisible(true);
            OnOpened();
        }

        public void Close()
        {
            if (Current != this) return;

            Current = null;
            LastClosedFrame = Time.frameCount;
            SetVisible(false);
            if (Player != null) Player.SetInputLocked(false);
            OnClosed();
        }

        protected virtual void Update()
        {
            if (IsOpen && GameInput.EscapePressed) Close();
        }

        protected virtual void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        protected abstract void SetVisible(bool visible);
        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }
    }
}
