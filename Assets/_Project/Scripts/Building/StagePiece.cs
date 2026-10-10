using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Часть здания (стена, пол, потолок, витрина, светильник), которая видна только
    /// на определённых этапах расширения. Например, восточная стена маленького зала
    /// существует на этапе 0 и исчезает после первого расширения.
    /// Включением и выключением частей управляет ExpansionManager.
    /// </summary>
    public class StagePiece : MonoBehaviour
    {
        [SerializeField] private LayoutZone zone;
        [SerializeField] private int minStage;
        [SerializeField] private int maxStage = 99;

        public LayoutZone Zone => zone;

        public bool VisibleAt(int stage) => stage >= minStage && stage <= maxStage;

        /// <summary>Настройка из редактора при постройке здания.</summary>
        public void Setup(LayoutZone pieceZone, int min, int max)
        {
            zone = pieceZone;
            minStage = min;
            maxStage = max;
        }
    }
}
