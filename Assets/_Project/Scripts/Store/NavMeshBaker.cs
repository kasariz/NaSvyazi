using NaSvyazi.Core;
using Unity.AI.Navigation;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Строит навигационную сетку (NavMesh) салона при запуске игры.
    /// Сетка описывает, где могут ходить покупатели: пол без стен, стеллажей и прилавка.
    /// Сборка при запуске избавляет от ручного «запекания» после каждой перестановки.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    [DefaultExecutionOrder(-100)]
    public class NavMeshBaker : MonoBehaviour
    {
        private NavMeshSurface surface;

        private void Awake()
        {
            surface = GetComponent<NavMeshSurface>();
            surface.BuildNavMesh();
        }

        private void OnEnable() => GameEvents.LayoutChanged += Rebuild;
        private void OnDisable() => GameEvents.LayoutChanged -= Rebuild;

        /// <summary>Перестроить сетку после перестановки мебели или расширения зала.</summary>
        public void Rebuild()
        {
            if (surface != null) surface.BuildNavMesh();
        }
    }
}
