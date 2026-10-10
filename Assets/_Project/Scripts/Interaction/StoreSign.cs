using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Player;
using NaSvyazi.Store;
using TMPro;
using UnityEngine;

namespace NaSvyazi.Interaction
{
    /// <summary>
    /// Табличка «Открыто / Закрыто» на кронштейне у входа. Утром игрок открывает ею салон
    /// и начинает рабочий день; днём можно закрыть салон досрочно.
    /// При смене состояния табличка переворачивается вокруг вертикальной оси:
    /// в середине поворота (ребром к зрителю) надписи и цвет меняются.
    /// </summary>
    public class StoreSign : MonoBehaviour, IInteractable
    {
        [Header("Вращающаяся часть")]
        [SerializeField] private Transform plate;
        [SerializeField] private float flipDuration = 0.45f;

        [Header("Оформление")]
        [SerializeField] private Renderer signRenderer;
        [SerializeField] private Material openMaterial;
        [SerializeField] private Material closedMaterial;

        [Tooltip("Надпись со стороны улицы")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Надпись со стороны зала")]
        [SerializeField] private TMP_Text backLabel;

        public bool IsOpen { get; private set; }

        private bool shownOpen;     // какое состояние сейчас нарисовано
        private float flipTime = -1f;
        private bool swapped;

        private void OnEnable() => GameEvents.StoreOpenChanged += OnStoreOpenChanged;
        private void OnDisable() => GameEvents.StoreOpenChanged -= OnStoreOpenChanged;

        private void Start()
        {
            shownOpen = IsOpen;
            ApplyVisual(shownOpen);
        }

        private void OnStoreOpenChanged(bool open)
        {
            IsOpen = open;
            if (open == shownOpen && flipTime < 0f) return;

            if (plate == null)
            {
                shownOpen = open;
                ApplyVisual(open);
                return;
            }

            // Запускаем переворот (если он уже идёт — продолжится и покажет новое состояние).
            if (flipTime < 0f)
            {
                flipTime = 0f;
                swapped = false;
                SoundManager.Play(Sfx.Pickup, 0.4f);
            }
        }

        private void Update()
        {
            if (flipTime < 0f || plate == null) return;

            flipTime += Time.deltaTime;
            float t = Mathf.Clamp01(flipTime / flipDuration);
            float eased = t * t * (3f - 2f * t); // плавный старт и остановка
            plate.localRotation = Quaternion.Euler(0f, 180f * eased, 0f);

            // Ребром к зрителю — меняем надписи: после поворота с обеих сторон видно новое состояние.
            if (!swapped && eased >= 0.5f)
            {
                swapped = true;
                shownOpen = IsOpen;
                ApplyVisual(shownOpen);
            }

            if (t >= 1f)
            {
                // Табличка симметрична: возвращаем поворот в ноль, внешне ничего не меняется.
                plate.localRotation = Quaternion.identity;
                flipTime = -1f;
                if (shownOpen != IsOpen) OnStoreOpenChanged(IsOpen); // успели переключить ещё раз
            }
        }

        public string GetPrompt(PlayerInteractor interactor)
        {
            DayManager day = DayManager.Instance;
            if (day == null) return IsOpen ? "Перевернуть табличку: закрыть" : "Перевернуть табличку: открыть";

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

        private void ApplyVisual(bool open)
        {
            if (signRenderer != null)
            {
                Material m = open ? openMaterial : closedMaterial;
                if (m != null) signRenderer.sharedMaterial = m;
            }

            string text = open
                ? "ОТКРЫТО\n<size=42%>добро пожаловать</size>"
                : "ЗАКРЫТО\n<size=42%>откроемся утром</size>";
            if (label != null) label.text = text;
            if (backLabel != null) backLabel.text = text;
        }
    }
}
