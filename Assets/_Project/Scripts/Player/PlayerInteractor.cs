using NaSvyazi.Core;
using NaSvyazi.Interaction;
using NaSvyazi.UI;
using UnityEngine;

namespace NaSvyazi.Player
{
    /// <summary>
    /// Поиск объекта, на который смотрит игрок (луч из центра экрана),
    /// вывод подсказки и запуск взаимодействия по клавише E.
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

        public PlayerController Controller { get; private set; }
        public PlayerCarry Carry { get; private set; }

        /// <summary>Объект под прицелом (или null).</summary>
        public IInteractable Current { get; private set; }

        /// <summary>Данные последнего попадания луча — пригодятся для выкладки товара на полку.</summary>
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
                if (interactionUI != null) interactionUI.Hide();
                return;
            }

            FindTarget();

            if (GameInput.InteractPressed && Current != null && Current.CanInteract(this))
                Current.Interact(this);
            else if (GameInput.DropPressed && Carry.IsCarrying)
                Carry.Drop();

            UpdatePrompt();
        }

        private void FindTarget()
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, reach, interactionMask, QueryTriggerInteraction.Collide))
            {
                LastHit = hit;
                Current = hit.collider.GetComponentInParent<IInteractable>();
            }
            else
            {
                Current = null;
            }
        }

        private void UpdatePrompt()
        {
            if (interactionUI == null) return;

            string prompt = null;

            if (Current != null && Current.CanInteract(this))
            {
                string text = Current.GetPrompt(this);
                if (!string.IsNullOrEmpty(text))
                    prompt = $"[E] {text}";
            }

            if (Carry.IsCarrying)
            {
                string drop = $"[G] Бросить: {Carry.Held.DisplayName}";
                prompt = prompt == null ? drop : prompt + "\n" + drop;
            }

            if (prompt == null) interactionUI.Hide();
            else interactionUI.Show(prompt);
        }
    }
}
