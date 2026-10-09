using TMPro;
using UnityEngine;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Подсказка взаимодействия под прицелом («[E] Взять: Коробка»).
    /// </summary>
    public class InteractionUI : MonoBehaviour
    {
        [Tooltip("Объект-контейнер подсказки (включается/выключается)")]
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private TMP_Text promptText;

        private void Awake() => Hide();

        public void Show(string text)
        {
            if (promptRoot != null && !promptRoot.activeSelf)
                promptRoot.SetActive(true);

            // Меняем текст только при изменении, чтобы не перестраивать меш каждый кадр.
            if (promptText != null && promptText.text != text)
                promptText.text = text;
        }

        public void Hide()
        {
            if (promptRoot != null && promptRoot.activeSelf)
                promptRoot.SetActive(false);
        }
    }
}
