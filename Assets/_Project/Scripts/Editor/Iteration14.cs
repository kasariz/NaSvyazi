using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NaSvyazi.EditorTools
{
    /// <summary>
    /// Итерация 14 (версия 0.2.1): восстановление пола и улицы.
    ///
    /// Причина ошибки: полы с плиткой (тротуар, дорога, полы залов и соседей) — это меши,
    /// сохранённые файлами LayoutBox_N. Счётчик N после перекомпиляции скриптов начинался с нуля,
    /// и «Подготовить сцену (итерация 13)» перезаписала LayoutBox_0 и LayoutBox_1 (тротуар и дорогу)
    /// маленькими полами кофейни и цветочного. Теперь имена всегда уникальные, а эта команда
    /// пересоздаёт меши всех полов по их коллайдерам.
    /// </summary>
    public static partial class NaSvyaziTools
    {
        [MenuItem("На связи/Исправить пол и улицу (итерация 14)", priority = 12)]
        private static void RepairTiledFloors()
        {
            GameObject layout = FindIncludingInactive(LayoutRootName);
            if (layout == null)
            {
                Debug.LogError("[На связи] Карта не найдена. Выполните «Перестроить карту (итерация 11)».");
                return;
            }

            // Старые файлы мешей запоминаем, чтобы удалить их после замены.
            HashSet<string> oldPaths = new HashSet<string>();
            int repaired = 0, skipped = 0;

            foreach (MeshFilter mf in layout.GetComponentsInChildren<MeshFilter>(true))
            {
                string oldPath = mf.sharedMesh != null ? AssetDatabase.GetAssetPath(mf.sharedMesh) : "";
                bool tiled = oldPath.StartsWith(LayoutMeshesFolder) ||
                             (mf.sharedMesh == null && IsTiledName(mf.name));
                if (!tiled) continue;

                if (!TryGetTiledBox(mf, out Vector3 size, out float tile))
                {
                    skipped++;
                    Debug.LogWarning($"[На связи] Не удалось восстановить размер «{GetPath(mf.transform)}».", mf);
                    continue;
                }

                Undo.RecordObject(mf, "Исправить пол");
                mf.sharedMesh = TiledBoxMesh(size, mf.transform.position, tile);
                EditorUtility.SetDirty(mf);
                if (!string.IsNullOrEmpty(oldPath)) oldPaths.Add(oldPath);
                repaired++;
            }

            // Удаляем старые файлы мешей, на которые больше никто не ссылается.
            HashSet<string> used = new HashSet<string>();
            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mf.sharedMesh != null) used.Add(AssetDatabase.GetAssetPath(mf.sharedMesh));
            foreach (string path in oldPaths)
                if (!used.Contains(path)) AssetDatabase.DeleteAsset(path);

            AssetDatabase.SaveAssets();
            MarkSceneDirty();
            Debug.Log($"[На связи] Итерация 14: пересоздано полов и покрытий — {repaired}" +
                      (skipped > 0 ? $", пропущено — {skipped} (см. предупреждения)" : "") +
                      ". Сохраните сцену (Ctrl+S).");
        }

        private static bool IsTiledName(string name) => name == "Floor" || name == "Sidewalk" || name == "Road";

        /// <summary>Размер и шаг плитки покрытия — те же, что задаёт LayoutBuilder / Iteration13.</summary>
        private static bool TryGetTiledBox(MeshFilter mf, out Vector3 size, out float tile)
        {
            size = Vector3.zero;
            tile = 2f;

            if (mf.name == "Road")
            {
                // Дорога без коллайдера: (−20, −0.3, −28)…(14, −0.1, −18.2).
                size = new Vector3(34f, 0.2f, 9.8f);
                tile = 4f;
                return true;
            }

            BoxCollider box = mf.GetComponent<BoxCollider>();
            if (box == null) return false;
            size = box.size;

            if (mf.name == "Sidewalk" || HasAncestor(mf.transform, "Neighbors")) tile = 2f;
            else if (HasAncestor(mf.transform, "Storage")) tile = 4f;
            else if (HasAncestor(mf.transform, "Salon")) tile = 2.4f;
            return true;
        }

        private static bool HasAncestor(Transform t, string name)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
                if (p.name == name) return true;
            return false;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
