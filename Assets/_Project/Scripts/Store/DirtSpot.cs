using NaSvyazi.Audio;
using NaSvyazi.Interaction;
using NaSvyazi.Player;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Грязное пятно на полу. Со шваброй в руках: ЛКМ (удерживать) — отмыть.
    /// Пятно бледнеет по мере уборки и исчезает. Неубранные пятна к концу дня снижают репутацию.
    /// </summary>
    public class DirtSpot : MonoBehaviour, IActionTarget
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Сколько «ударов» шваброй (повторов ЛКМ) нужно, чтобы отмыть пятно")]
        [SerializeField] private int strokes = 8;

        private Renderer spotRenderer;
        private Color baseColor;
        private int left;

        public void Init(Renderer renderer, Color color)
        {
            spotRenderer = renderer;
            baseColor = color;
            left = strokes;
            Apply();
        }

        public string GetPrimaryPrompt(PlayerInteractor interactor) =>
            interactor.Carry.Held is Mop ? "Мыть пол (удерживайте)" : null;

        public void DoPrimary(PlayerInteractor interactor)
        {
            if (!(interactor.Carry.Held is Mop)) return;

            left--;
            ((Mop)interactor.Carry.Held).Scrub(transform.position);
            SoundManager.Play(Sfx.Scrub, 0.35f);
            if (left <= 0)
            {
                if (CleaningManager.Instance != null) CleaningManager.Instance.OnCleaned(this);
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        public string GetSecondaryPrompt(PlayerInteractor interactor) => null;
        public void DoSecondary(PlayerInteractor interactor) { }

        private void Apply()
        {
            if (spotRenderer == null) return;
            Color c = baseColor;
            c.a *= Mathf.Clamp01((float)left / strokes);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, c);
            spotRenderer.SetPropertyBlock(block);
        }
    }
}
