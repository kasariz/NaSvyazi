using NaSvyazi.Core;
using UnityEngine;

namespace NaSvyazi.Player
{
    /// <summary>
    /// Управление персонажем от первого лица: ходьба, бег, прыжок,
    /// обзор мышью и гравитация. Основан на компоненте CharacterController.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Камера игрока (дочерний объект на уровне глаз)")]
        [SerializeField] private Transform cameraPivot;

        [Header("Движение")]
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float jumpHeight = 0.9f;
        [SerializeField] private float gravity = -20f;

        [Header("Обзор")]
        [Tooltip("Градусов поворота на пиксель движения мыши")]
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        private CharacterController controller;
        private float pitch;
        private float verticalVelocity;

        /// <summary>
        /// Ввод заблокирован (открыто меню, терминал, пауза).
        /// В этом состоянии персонаж не двигается, а курсор свободен.
        /// </summary>
        public bool InputLocked { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            SetInputLocked(false);
        }

        private void Update()
        {
            if (GameInput.EscapePressed)
                SetInputLocked(!InputLocked);

            HandleLook();
            HandleMovement();
        }

        /// <summary>Заблокировать или разблокировать управление и курсор.</summary>
        public void SetInputLocked(bool locked)
        {
            InputLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        private void HandleLook()
        {
            if (InputLocked || cameraPivot == null) return;

            Vector2 delta = GameInput.LookDelta * mouseSensitivity;

            // Поворот тела по горизонтали, камеры — по вертикали.
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            Vector2 input = InputLocked ? Vector2.zero : GameInput.Move;
            float speed = GameInput.Sprint ? sprintSpeed : walkSpeed;

            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

            // Небольшая прижимающая сила, чтобы isGrounded работал стабильно.
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (!InputLocked && controller.isGrounded && GameInput.JumpPressed)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);
        }
    }
}
