using UnityEngine;
using UnityEngine.InputSystem;

namespace NaSvyazi.Core
{
    /// <summary>
    /// Единая точка чтения ввода игрока (обёртка над пакетом Input System).
    /// Все игровые скрипты получают ввод только через этот класс, поэтому
    /// переназначить клавиши или добавить геймпад можно в одном месте.
    /// Клавиши читаются по физическому положению, раскладка (RU/EN) не важна.
    /// </summary>
    public static class GameInput
    {
        /// <summary>Перемещение: X — вбок (A/D), Y — вперёд/назад (W/S).</summary>
        public static Vector2 Move
        {
            get
            {
                Keyboard k = Keyboard.current;
                if (k == null) return Vector2.zero;

                float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
                Vector2 v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        /// <summary>Смещение мыши за кадр (в пикселях).</summary>
        public static Vector2 LookDelta =>
            Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        /// <summary>Бег (удерживать Shift).</summary>
        public static bool Sprint =>
            Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        /// <summary>Прыжок (Space).</summary>
        public static bool JumpPressed =>
            Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        /// <summary>Взаимодействие с объектом (E).</summary>
        public static bool InteractPressed =>
            Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;

        /// <summary>Бросить переносимый предмет (G).</summary>
        public static bool DropPressed =>
            Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame;

        /// <summary>Основное действие (ЛКМ) — понадобится для выкладки товара.</summary>
        public static bool PrimaryPressed =>
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        /// <summary>Дополнительное действие (ПКМ) — понадобится, чтобы снимать товар с полки.</summary>
        public static bool SecondaryPressed =>
            Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        /// <summary>ЛКМ удерживается — для непрерывной выкладки товара.</summary>
        public static bool PrimaryHeld =>
            Mouse.current != null && Mouse.current.leftButton.isPressed;

        /// <summary>ПКМ удерживается.</summary>
        public static bool SecondaryHeld =>
            Mouse.current != null && Mouse.current.rightButton.isPressed;

        /// <summary>Пропустить обучение (F1).</summary>
        public static bool TutorialSkipPressed =>
            Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;

        /// <summary>Повернуть мебель при расстановке (R).</summary>
        public static bool RotatePressed =>
            Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;

        /// <summary>Пауза / освобождение курсора (Esc).</summary>
        public static bool EscapePressed =>
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }
}
