using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Короткая анимация появления: объект «впрыгивает» из уменьшенного размера
    /// с лёгким перелётом. Используется при выкладке товара на полку.
    /// </summary>
    public class PopIn : MonoBehaviour
    {
        [SerializeField] private float duration = 0.16f;
        [SerializeField] private float startScale = 0.55f;

        private Vector3 targetScale;
        private float time;

        private void Awake()
        {
            targetScale = transform.localScale;
            transform.localScale = targetScale * startScale;
        }

        private void Update()
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            // Ease-out-back: быстрый рост с небольшим «перелётом» и возвратом к 1.
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float k = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            float scale = Mathf.LerpUnclamped(startScale, 1f, k);
            transform.localScale = targetScale * scale;

            if (t >= 1f)
            {
                transform.localScale = targetScale;
                Destroy(this);
            }
        }
    }
}
