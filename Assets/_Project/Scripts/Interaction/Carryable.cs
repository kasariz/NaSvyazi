using NaSvyazi.Player;
using UnityEngine;

namespace NaSvyazi.Interaction
{
    /// <summary>
    /// Предмет, который игрок может взять в руки и перенести (коробка с товаром).
    /// Пока предмет в руках, физика отключена и он следует за камерой.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Carryable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string displayName = "Коробка";

        [Header("Положение в руках")]
        [SerializeField] private Vector3 holdOffset = Vector3.zero;
        [SerializeField] private Vector3 holdRotation = Vector3.zero;

        private Rigidbody body;
        private Collider[] colliders;

        public bool IsHeld { get; private set; }
        public virtual string DisplayName => displayName;

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
        }

        // --- IInteractable ---

        public virtual string GetPrompt(PlayerInteractor interactor) => $"Взять: {DisplayName}";

        public virtual bool CanInteract(PlayerInteractor interactor) =>
            !IsHeld && interactor.Carry != null && !interactor.Carry.IsCarrying;

        public virtual void Interact(PlayerInteractor interactor) => interactor.Carry.TryPickUp(this);

        // --- Вызываются из PlayerCarry ---

        /// <summary>Предмет взят в руки: отключаем физику и крепим к точке удержания.</summary>
        public void OnPickedUp(Transform holdPoint)
        {
            IsHeld = true;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;

            SetCollidersEnabled(false);

            transform.SetParent(holdPoint, false);
            transform.localPosition = holdOffset;
            transform.localRotation = Quaternion.Euler(holdRotation);
        }

        /// <summary>Предмет брошен: возвращаем физику и придаём начальную скорость.</summary>
        public void OnDropped(Vector3 velocity)
        {
            transform.SetParent(null, true);

            SetCollidersEnabled(true);

            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = velocity;

            IsHeld = false;
        }

        /// <summary>
        /// Предмет забирают из рук без физики (например, ставят на стеллаж).
        /// Понадобится в следующих итерациях.
        /// </summary>
        public void OnReleasedTo(Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            transform.SetParent(parent, false);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            SetCollidersEnabled(true);
            IsHeld = false;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (Collider c in colliders)
                c.enabled = enabled;
        }
    }
}
