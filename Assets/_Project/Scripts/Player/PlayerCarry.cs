using NaSvyazi.Audio;
using NaSvyazi.Interaction;
using UnityEngine;

namespace NaSvyazi.Player
{
    /// <summary>
    /// Переноска предметов: игрок может держать в руках один предмет.
    /// </summary>
    public class PlayerCarry : MonoBehaviour
    {
        [Tooltip("Точка перед камерой, где находится предмет в руках")]
        [SerializeField] private Transform holdPoint;

        [Tooltip("Скорость, с которой предмет отлетает вперёд при броске")]
        [SerializeField] private float dropForwardSpeed = 1.5f;

        public Carryable Held { get; private set; }

        private void Awake()
        {
            // Руки героя (видны, когда он что-то несёт или катит тележку).
            if (GetComponent<PlayerHands>() == null) gameObject.AddComponent<PlayerHands>();
        }
        public bool IsCarrying => Held != null;

        /// <summary>Попытаться взять предмет. Возвращает false, если руки заняты.</summary>
        public bool TryPickUp(Carryable item)
        {
            if (item == null || IsCarrying || holdPoint == null) return false;

            Held = item;
            item.OnPickedUp(holdPoint);
            SoundManager.Play(Sfx.Pickup, 0.7f);
            Core.GameEvents.RaiseBoxPickedUp();
            return true;
        }

        /// <summary>Бросить предмет перед собой.</summary>
        public void Drop()
        {
            if (!IsCarrying) return;

            Carryable item = Held;
            Held = null;
            item.OnDropped(holdPoint.forward * dropForwardSpeed);
            SoundManager.Play(Sfx.Pickup, 0.4f);
        }

        /// <summary>
        /// Отдать предмет другой системе (стеллажу, складу) без физического броска.
        /// Возвращает предмет, который был в руках, или null.
        /// </summary>
        public Carryable Release()
        {
            Carryable item = Held;
            Held = null;
            return item;
        }
    }
}
