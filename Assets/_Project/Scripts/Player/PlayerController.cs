using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.UI;
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

        [Header("Шаги")]
        [Tooltip("Расстояние между шагами при ходьбе, м")]
        [SerializeField] private float stepDistance = 0.6f;
        private float stepProgress;

        private CharacterController controller;
        private float pitch;
        private float verticalVelocity;

        /// <summary>
        /// Ввод заблокирован (открыто меню, терминал, пауза).
        /// В этом состоянии персонаж не двигается, а курсор свободен.
        /// </summary>
        public bool InputLocked { get; private set; }

        /// <summary>Множитель скорости (например, бодрость от кофе из соседней кофейни).</summary>
        public static float SpeedMultiplier { get; set; } = 1f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            SpeedMultiplier = 1f;

            // Покупатели видят игрока как препятствие и обходят его, а не упираются.
            UnityEngine.AI.NavMeshObstacle obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
            obstacle.radius = 0.35f;
            obstacle.height = 1.8f;
            obstacle.center = new Vector3(0f, 0.9f, 0f);
            obstacle.carving = false;
        }

        private void Start()
        {
            SetInputLocked(false);
        }

        private void Update()
        {
            // Esc обрабатывает меню паузы. Если его нет на сцене — просто освобождаем курсор.
            bool escapeHandledByWindow = ModalWindow.IsAnyOpen || ModalWindow.LastClosedFrame == Time.frameCount;
            if (GameInput.EscapePressed && !escapeHandledByWindow && PauseMenuUI.Instance == null)
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

            Vector2 delta = GameInput.LookDelta * (mouseSensitivity * GameSettings.MouseSensitivity);

            // Поворот тела по горизонтали, камеры — по вертикали.
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            Vector2 input = InputLocked ? Vector2.zero : GameInput.Move;
            float speed = (GameInput.Sprint ? sprintSpeed : walkSpeed) * SpeedMultiplier;

            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

            // Небольшая прижимающая сила, чтобы isGrounded работал стабильно.
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (!InputLocked && controller.isGrounded && GameInput.JumpPressed)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);

            // Звук шагов: по пройденному расстоянию, только на земле.
            if (controller.isGrounded && input.sqrMagnitude > 0.01f)
            {
                // Длина шага растёт со скоростью: на бегу (Shift) шаги реже, а не «барабанная дробь».
                float stride = stepDistance * Mathf.Lerp(1.8f, 3.2f,
                    Mathf.InverseLerp(walkSpeed, sprintSpeed * 1.3f, speed));
                stepProgress += speed * Time.deltaTime;
                if (stepProgress >= stride)
                {
                    stepProgress = 0f;
                    SoundManager.PlayStep();
                }
            }
        }
    }
}
