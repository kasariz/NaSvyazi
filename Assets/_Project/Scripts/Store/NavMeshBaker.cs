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

        /// <summary>Область навигации «только персонал» (Project Settings → Navigation → Areas).</summary>
        public const string StaffOnlyArea = "StaffOnly";

        private void Awake()
        {
            surface = GetComponent<NavMeshSurface>();
            OpenStorageDoorForStaff();
            surface.BuildNavMesh();
        }

        /// <summary>
        /// Дверь склада раньше была закрыта для всех агентов (NavMeshObstacle с вырезанием).
        /// Теперь проём помечается областью «StaffOnly»: сотрудники ходят через него на склад,
        /// а покупатели (у них эта область исключена из маски) по-прежнему не заходят.
        /// </summary>
        private static void OpenStorageDoorForStaff()
        {
            int area = UnityEngine.AI.NavMesh.GetAreaFromName(StaffOnlyArea);
            GameObject blocker = GameObject.Find("InnerDoor_NavBlock");
            if (area < 0 || blocker == null) return;

            UnityEngine.AI.NavMeshObstacle obstacle = blocker.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            Vector3 size = obstacle != null ? obstacle.size : new Vector3(0.8f, 2f, 1.4f);
            if (obstacle != null) obstacle.enabled = false;

            NavMeshModifierVolume volume = blocker.GetComponent<NavMeshModifierVolume>();
            if (volume == null) volume = blocker.AddComponent<NavMeshModifierVolume>();
            volume.size = new Vector3(size.x + 0.6f, size.y + 1f, size.z);
            volume.center = Vector3.zero;
            volume.area = area;
        }

        /// <summary>Маска областей для покупателей: всё, кроме «StaffOnly».</summary>
        public static int CustomerAreaMask
        {
            get
            {
                int area = UnityEngine.AI.NavMesh.GetAreaFromName(StaffOnlyArea);
                return area < 0 ? UnityEngine.AI.NavMesh.AllAreas : UnityEngine.AI.NavMesh.AllAreas & ~(1 << area);
            }
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
