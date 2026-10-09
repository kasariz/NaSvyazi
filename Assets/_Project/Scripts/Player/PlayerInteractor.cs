using System.Text;
using NaSvyazi.Core;
using NaSvyazi.Interaction;
using NaSvyazi.UI;
using UnityEngine;

namespace NaSvyazi.Player
{
    /// <summary>
    /// Поиск объекта, на который смотрит игрок (луч из центра экрана),
    /// вывод подсказок и запуск действий:
    /// E — взаимодействие, ЛКМ/ПКМ — действия с объектом (с автоповтором при удержании),
    /// G — бросить предмет из рук.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerCarry))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InteractionUI interactionUI;

        [Tooltip("Дальность взаимодействия в метрах")]
        [SerializeField] private float reach = 2.5f;

        [Tooltip("Слои, по которым ищутся объекты (снимите галочки Player и Ignore Raycast)")]
        [SerializeField] private LayerMask interactionMask = ~0;

        [Header("Автоповтор ЛКМ/ПКМ при удержании")]
        [SerializeField] private float repeatDelay = 0.35f;
        [SerializeField] private float repeatInterval = 0.1f;

        private readonly StringBuilder promptBuilder = new StringBuilder();
        private float repeatTimer;

        public PlayerController Controller { get; private set; }
        public PlayerCarry Carry { get; private set; }

        /// <summary>Объект под прицелом, поддерживающий клавишу E (или null).</summary>
        public IInteractable Current { get; private set; }

        /// <summary>Объект под прицелом, поддерживающий ЛКМ/ПКМ (или null).</summary>
        public IActionTarget CurrentAction { get; private set; }

        /// <summary>Данные последнего попадания луча.</summary>
        public RaycastHit LastHit { get; private set; }

        private void Awake()
        {
            Controller = GetComponent<PlayerController>();
            Carry = GetComponent<PlayerCarry>();
            if (playerCamera == null) playerCamera = Camera.main;
        }

        private void Update()
        {
            if (Controller.InputLocked)
            {
                Current = null;
                CurrentAction = null;
                if (interactionUI != null) interactionUI.Hide();
                return;
            }

            FindTarget();

            if (GameInput.InteractPressed && Current != null && Current.CanInteract(this))
                Current.Interact(this);
            else if (GameInput.DropPressed && Carry.IsCarrying)
                Carry.Drop();

            HandleMouseActions();
            UpdatePrompt();
        }

        private void FindTarget()
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, reach, interactionMask, QueryTriggerInteraction.Collide))
            {
                LastHit = hit;
                Current = hit.collider.GetComponentInParent<IInteractable>();
                CurrentAction = hit.collider.GetComponentInParent<IActionTarget>();
            }
            else
            {
                Current = null;
                CurrentAction = null;
            }
        }

        /// <summary>
        /// ЛКМ/ПКМ: первое нажатие срабатывает сразу, при удержании — повтор
        /// через repeatDelay, затем каждые repeatInterval секунд.
        /// </summary>
        private void HandleMouseActions()
        {
            if (CurrentAction == null)
            {
                repeatTimer = 0f;
                return;
            }

            if (GameInput.PrimaryPressed)
            {
                TryPrimary();
                repeatTimer = repeatDelay;
            }
            else if (GameInput.SecondaryPressed)
            {
                TrySecondary();
                repeatTimer = repeatDelay;
            }
            else if (GameInput.PrimaryHeld || GameInput.SecondaryHeld)
            {
                repeatTimer -= Time.deltaTime;
                if (repeatTimer <= 0f)
                {
                    if (GameInput.PrimaryHeld) TryPrimary();
                    else TrySecondary();
                    repeatTimer = repeatInterval;
                }
            }
        }

        private void TryPrimary()
        {
            if (!string.IsNullOrEmpty(CurrentAction.GetPrimaryPrompt(this)))
                CurrentAction.DoPrimary(this);
        }

        private void TrySecondary()
        {
            if (!string.IsNullOrEmpty(CurrentAction.GetSecondaryPrompt(this)))
                CurrentAction.DoSecondary(this);
        }

        private void UpdatePrompt()
        {
            if (interactionUI == null) return;

            promptBuilder.Clear();

            if (Current != null && Current.CanInteract(this))
                AppendLine("E", Current.GetPrompt(this));

            if (CurrentAction != null)
            {
                AppendLine("ЛКМ", CurrentAction.GetPrimaryPrompt(this));
                AppendLine("ПКМ", CurrentAction.GetSecondaryPrompt(this));
            }

            if (Carry.IsCarrying)
                AppendLine("G", $"Бросить: {Carry.Held.DisplayName}");

            if (promptBuilder.Length == 0) interactionUI.Hide();
            else interactionUI.Show(promptBuilder.ToString());
        }

        private void AppendLine(string key, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (promptBuilder.Length > 0) promptBuilder.Append('\n');
            promptBuilder.Append('[').Append(key).Append("] ").Append(text);
        }
    }
}
