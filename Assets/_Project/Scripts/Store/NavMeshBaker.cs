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
        private void Awake()
        {
            NavMeshSurface surface = GetComponent<NavMeshSurface>();
            surface.BuildNavMesh();
        }
    }
}
