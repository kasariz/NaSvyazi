using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Interaction;
using NaSvyazi.Player;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Тележка для коробок. E по тележке — взяться за ручку и катить её перед собой,
    /// ещё раз E — отпустить. Коробки ставятся на тележку ЛКМ (как на складской стеллаж),
    /// снимаются E. Тележка не проезжает сквозь стены и мебель: упёрлась — отпускается.
    /// </summary>
    [RequireComponent(typeof(StorageRack))]
    public class HandCart : MonoBehaviour, IInteractable
    {
        /// <summary>Тележка, которую сейчас катит игрок (или null).</summary>
        public static HandCart Pushed { get; private set; }

        [Tooltip("Расстояние от игрока до центра тележки, м")]
        [SerializeField] private float pushDistance = 1.05f;

        [Tooltip("Половина габаритов тележки для проверки столкновений")]
        [SerializeField] private Vector3 halfExtents = new Vector3(0.47f, 0.45f, 0.36f);

        [Tooltip("Концы ручки (локально) — за них держатся руки героя")]
        [SerializeField] private Vector3 leftGrip = new Vector3(-0.32f, 0.95f, -0.42f);
        [SerializeField] private Vector3 rightGrip = new Vector3(0.32f, 0.95f, -0.42f);

        private Transform player;
        private readonly Collider[] hits = new Collider[16];

        public bool IsPushed => Pushed == this;
        public Vector3 LeftGripWorld => transform.TransformPoint(leftGrip);
        public Vector3 RightGripWorld => transform.TransformPoint(rightGrip);

        // --- IInteractable ---

        public string GetPrompt(PlayerInteractor interactor) => IsPushed ? "Отпустить тележку" : "Катить тележку";

        public bool CanInteract(PlayerInteractor interactor) =>
            IsPushed || (Pushed == null && !interactor.Carry.IsCarrying);

        public void Interact(PlayerInteractor interactor)
        {
            if (IsPushed) Release(null);
            else Grab(interactor.transform);
        }

        private void Grab(Transform by)
        {
            player = by;
            Pushed = this;
            SoundManager.Play(Sfx.Pickup, 0.5f);
        }

        /// <summary>Отпустить тележку (message — уведомление игроку, например «упёрлась»).</summary>
        public void Release(string message)
        {
            if (Pushed == this) Pushed = null;
            player = null;
            if (!string.IsNullOrEmpty(message)) GameEvents.Notify(message);
        }

        private void OnDisable()
        {
            if (Pushed == this) Pushed = null;
        }

        private void LateUpdate()
        {
            if (!IsPushed || player == null) return;

            // Тележка — перед игроком, развёрнута туда же, куда он смотрит.
            Vector3 forward = player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return;
            forward.Normalize();

            Vector3 target = player.position + forward * pushDistance;
            target.y = transform.position.y;
            Quaternion rotation = Quaternion.LookRotation(forward);

            if (IsFree(target, rotation))
            {
                transform.SetPositionAndRotation(target, rotation);
            }
            else if (Vector3.Distance(player.position, transform.position) > pushDistance + 1.2f)
            {
                // Игрок ушёл, а тележка застряла — отпускаем.
                Release("Тележка упёрлась — подкатите её с другой стороны");
            }
        }

        /// <summary>Свободно ли место: не задевает стены, мебель и другие предметы (кроме своих коробок).</summary>
        private bool IsFree(Vector3 position, Quaternion rotation)
        {
            Vector3 center = position + Vector3.up * (halfExtents.y + 0.08f);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, hits, rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform t = hits[i].transform;
                if (t.IsChildOf(transform) || (player != null && t.IsChildOf(player))) continue;
                return false;
            }
            return true;
        }
    }
}
