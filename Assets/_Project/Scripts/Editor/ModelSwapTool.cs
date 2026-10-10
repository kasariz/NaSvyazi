using System.Collections.Generic;
using System.IO;
using System.Text;
using NaSvyazi.Building;
using NaSvyazi.Core;
using NaSvyazi.Products;
using NaSvyazi.Store;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace NaSvyazi.EditorTools
{
    /// <summary>
    /// Замена простых фигур на скачанные 3D-модели (FBX/OBJ/префабы) — товары, коробка, декор.
    ///
    /// Куда класть файлы (имя файла = идентификатор):
    ///   Assets/_Project/Art/Models/Products/  — phone_basic.fbx, earbuds.fbx, case.fbx …
    ///   Assets/_Project/Art/Models/Furniture/ — plant.fbx, water_cooler.fbx, waiting_chairs.fbx …
    ///   Assets/_Project/Art/Models/Box/       — один файл коробки (любое имя).
    /// Суффикс «@угол» задаёт поворот вручную: phone_basic@180.obj (вокруг вертикали), speaker@x-90.obj, phone_mid@x-90y180.obj.
    ///
    /// Модель автоматически масштабируется в габариты исходной (товар — в ProductData.size,
    /// мебель — в размер своей простой модели), ставится на пол и лишается коллайдеров:
    /// коллайдеры, полки, свет и надписи остаются от простой модели, поэтому логика игры не меняется.
    /// Мебель с полками (стеллажи, витрина, стойка у кассы, складской стеллаж) не заменяется:
    /// места товаров на полках привязаны к их геометрии.
    /// </summary>
    public static class ModelSwapTool
    {
        private const string ModelsRoot = "Assets/_Project/Art/Models";
        private const string ProductsFolder = ModelsRoot + "/Products";
        private const string FurnitureFolder = ModelsRoot + "/Furniture";
        private const string BoxFolder = ModelsRoot + "/Box";
        private const string ProductPrefabsFolder = "Assets/_Project/Prefabs/ProductModels";
        private const string BoxPrefabPath = "Assets/_Project/Prefabs/ProductBox.prefab";

        [MenuItem("На связи/3D-модели/Подключить модели из Art/Models", priority = 15)]
        private static void ApplyModels()
        {
            EnsureFolder(ProductsFolder);
            EnsureFolder(FurnitureFolder);
            EnsureFolder(BoxFolder);

            StringBuilder log = new StringBuilder();
            int products = ApplyProducts(log);
            int furniture = ApplyFurniture(log);
            bool box = ApplyBox(log);

            AssetDatabase.SaveAssets();
            string summary = $"Товары: {products}\nМебель: {furniture}\nКоробка: {(box ? "да" : "нет")}";
            if (products == 0 && furniture == 0 && !box)
                summary = $"Модели не найдены. Положите FBX/OBJ в папки\n{ProductsFolder}\n{FurnitureFolder}\n{BoxFolder}\n" +
                          "с именами по идентификатору (см. SOUNDS_AND_MODELS.md).";
            Debug.Log("[На связи] 3D-модели:\n" + log);
            EditorUtility.DisplayDialog("3D-модели", summary + (log.Length > 0 ? "\n\nПодробности — в консоли." : ""), "OK");
        }

        [MenuItem("На связи/3D-модели/Вернуть простые модели", priority = 16)]
        private static void RevertModels()
        {
            if (!EditorUtility.DisplayDialog("Вернуть простые модели",
                    "Товары, коробка и мебель снова будут из простых фигур (файлы моделей не удаляются). Продолжить?",
                    "Вернуть", "Отмена"))
                return;

            foreach (ProductData p in LoadAll<ProductData>("Assets/_Project/Data/Products"))
            {
                if (p.modelPrefab == null || !AssetDatabase.GetAssetPath(p.modelPrefab).StartsWith(ProductPrefabsFolder)) continue;
                p.modelPrefab = null;
                EditorUtility.SetDirty(p);
            }

            foreach (FurnitureData f in LoadAll<FurnitureData>("Assets/_Project/Data/Furniture"))
                if (f.prefab != null) EditPrefab(AssetDatabase.GetAssetPath(f.prefab), RemoveImported);
            EditPrefab(BoxPrefabPath, RemoveImported);

            AssetDatabase.SaveAssets();
            Debug.Log("[На связи] Простые модели возвращены.");
        }

        // ----------------------------------------------------------------
        // Товары
        // ----------------------------------------------------------------

        private static int ApplyProducts(StringBuilder log)
        {
            Dictionary<string, (GameObject model, Vector3? rotation, string path)> models = FindModels(ProductsFolder);
            if (models.Count == 0) return 0;
            EnsureFolder(ProductPrefabsFolder);

            int count = 0;
            foreach (ProductData p in LoadAll<ProductData>("Assets/_Project/Data/Products"))
            {
                if (string.IsNullOrEmpty(p.id) || !models.TryGetValue(p.id.ToLowerInvariant(), out var m)) continue;

                // Обёртка: опорная точка — центр нижней грани, модель вписана в габариты товара.
                GameObject root = new GameObject("Product_" + p.id);
                Fit(m.model, root.transform, p.size, Vector3.zero, m.rotation, true);
                string path = $"{ProductPrefabsFolder}/Product_{p.id}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);

                p.modelPrefab = prefab;
                EditorUtility.SetDirty(p);
                log.AppendLine($"• товар «{p.displayName}» ← {m.path}");
                count++;
            }

            foreach (string key in models.Keys)
                if (LoadAll<ProductData>("Assets/_Project/Data/Products").TrueForAll(p => p.id?.ToLowerInvariant() != key))
                    log.AppendLine($"• нет товара с id «{key}» — файл пропущен");
            return count;
        }

        // ----------------------------------------------------------------
        // Мебель
        // ----------------------------------------------------------------

        private static int ApplyFurniture(StringBuilder log)
        {
            Dictionary<string, (GameObject model, Vector3? rotation, string path)> models = FindModels(FurnitureFolder);
            if (models.Count == 0) return 0;

            int count = 0;
            foreach (FurnitureData f in LoadAll<FurnitureData>("Assets/_Project/Data/Furniture"))
            {
                if (f.prefab == null || string.IsNullOrEmpty(f.id) ||
                    !models.TryGetValue(f.id.ToLowerInvariant(), out var m)) continue;

                if (f.prefab.GetComponentInChildren<ShelfSection>(true) != null ||
                    f.prefab.GetComponentInChildren<StorageRack>(true) != null)
                {
                    log.AppendLine($"• «{f.displayName}» — мебель с полками не заменяется (места товаров привязаны к полкам)");
                    continue;
                }

                EditPrefab(AssetDatabase.GetAssetPath(f.prefab), root =>
                {
                    RemoveImported(root);
                    List<Renderer> visuals = CollectVisuals(root.transform);
                    if (visuals.Count == 0) return;

                    Bounds b = LocalBounds(root.transform, visuals);
                    ImportedModel marker = root.AddComponent<ImportedModel>();
                    marker.sourcePath = m.path;
                    foreach (Renderer r in visuals)
                    {
                        r.enabled = false;
                        marker.hiddenRenderers.Add(r);
                    }

                    Fit(m.model, root.transform, b.size, new Vector3(b.center.x, b.min.y, b.center.z), m.rotation, false);
                });
                log.AppendLine($"• мебель «{f.displayName}» ← {m.path}");
                count++;
            }
            return count;
        }

        // ----------------------------------------------------------------
        // Коробка
        // ----------------------------------------------------------------

        private static bool ApplyBox(StringBuilder log)
        {
            Dictionary<string, (GameObject model, Vector3? rotation, string path)> models = FindModels(BoxFolder);
            if (models.Count == 0) return false;
            var m = new List<(GameObject model, Vector3? rotation, string path)>(models.Values)[0];

            bool done = false;
            EditPrefab(BoxPrefabPath, root =>
            {
                RemoveImported(root);
                Transform mesh = root.transform.Find("Mesh");
                Renderer r = mesh != null ? mesh.GetComponent<Renderer>() : null;
                if (r == null) return;

                Vector3 size = mesh.localScale;
                Vector3 bottom = mesh.localPosition - new Vector3(0f, size.y * 0.5f, 0f);

                ImportedModel marker = root.AddComponent<ImportedModel>();
                marker.sourcePath = m.path;
                marker.hiddenRenderers.Add(r);
                r.enabled = false; // коллайдер и окраска полосы остаются от простой коробки

                Fit(m.model, root.transform, size, bottom, m.rotation, false, true);
                done = true;
            });
            if (done) log.AppendLine($"• коробка ← {m.path} (цветная полоса товара остаётся поверх модели)");
            return done;
        }

        // ----------------------------------------------------------------
        // Общее
        // ----------------------------------------------------------------

        /// <summary>Рендереры простой модели мебели: без полок, надписей, частиц и уже подключённой модели.</summary>
        private static List<Renderer> CollectVisuals(Transform root)
        {
            List<Renderer> list = new List<Renderer>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (r.GetComponent<TMP_Text>() != null) continue;
                if (IsUnder(r.transform, root.Find(ImportedModel.ChildName))) continue;
                list.Add(r);
            }
            return list;
        }

        private static bool IsUnder(Transform t, Transform parent)
        {
            if (parent == null) return false;
            for (Transform p = t; p != null; p = p.parent)
                if (p == parent) return true;
            return false;
        }

        /// <summary>
        /// Вставить модель в parent и вписать в габариты size так, чтобы центр нижней грани
        /// оказался в точке bottomCenter (локальные координаты parent). Пропорции сохраняются.
        /// rotation — поворот из имени файла («@x90y180»); если его нет, поворот подбирается сам:
        /// из поворотов на 90° выбирается тот, при котором модель лучше всего заполняет габариты
        /// (для товаров — в том числе «поставить на ребро» лежащий смартфон; мебель только поворачивается вокруг вертикали).
        /// </summary>
        private static void Fit(GameObject source, Transform parent, Vector3 size, Vector3 bottomCenter,
            Vector3? rotation, bool allowTilt, bool stretch = false)
        {
            Transform holder = new GameObject(ImportedModel.ChildName).transform;
            holder.SetParent(parent, false);

            GameObject instance = PrefabUtility.InstantiatePrefab(source, holder) as GameObject;
            if (instance == null) instance = Object.Instantiate(source, holder);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;

            Vector3[] candidates = rotation.HasValue
                ? new[] { rotation.Value }
                : allowTilt
                    ? new[] { Vector3.zero, new Vector3(0f, 90f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(-90f, 90f, 0f),
                              new Vector3(0f, 0f, 90f), new Vector3(0f, 90f, 90f) }
                    : new[] { Vector3.zero, new Vector3(0f, 90f, 0f) };

            Vector3 best = candidates[0];
            float bestFill = -1f;
            foreach (Vector3 euler in candidates)
            {
                instance.transform.localRotation = Quaternion.Euler(euler);
                Bounds cb = LocalBounds(holder, instance.GetComponentsInChildren<Renderer>(true));
                float fill = Fill(cb.size, size);
                if (fill > bestFill + 0.02f) { bestFill = fill; best = euler; } // при равенстве — более простой поворот
            }
            instance.transform.localRotation = Quaternion.Euler(best);
            Bounds b = LocalBounds(holder, instance.GetComponentsInChildren<Renderer>(true));

            if (stretch)
            {
                // Растянуть точно в габариты (коробка: модель занимает ровно место простой коробки,
                // поэтому цветная полоса и надпись ложатся на неё, а не висят над ней).
                Vector3 k = new Vector3(SafeRatio(size.x, b.size.x), SafeRatio(size.y, b.size.y), SafeRatio(size.z, b.size.z));
                holder.localScale = k;
                holder.localPosition = bottomCenter - Vector3.Scale(new Vector3(b.center.x, b.min.y, b.center.z), k);
            }
            else
            {
                float scale = ScaleToFit(b.size, size);
                instance.transform.localScale = Vector3.one * scale;

                b = LocalBounds(holder, instance.GetComponentsInChildren<Renderer>(true));
                instance.transform.localPosition = bottomCenter - new Vector3(b.center.x, b.min.y, b.center.z);
            }

            // Коллайдеры модели не нужны: у объекта уже есть свои. Если удалить нельзя — просто выключаем.
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
            {
                try { Object.DestroyImmediate(c, true); }
                catch (System.InvalidOperationException) { c.enabled = false; }
            }
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private static float SafeRatio(float target, float model) => model > 0.0001f && target > 0.0001f ? target / model : 1f;

        private static float ScaleToFit(Vector3 model, Vector3 target)
        {
            float scale = float.MaxValue;
            for (int i = 0; i < 3; i++)
                if (target[i] > 0.001f && model[i] > 0.0001f) scale = Mathf.Min(scale, target[i] / model[i]);
            return scale == float.MaxValue || scale <= 0f ? 1f : scale;
        }

        /// <summary>Какую долю объёма габаритов займёт модель после масштабирования (0…1).</summary>
        private static float Fill(Vector3 model, Vector3 target)
        {
            float s = ScaleToFit(model, target);
            float v = 1f, t = 1f;
            for (int i = 0; i < 3; i++)
            {
                v *= Mathf.Max(0.001f, model[i] * s);
                t *= Mathf.Max(0.001f, target[i]);
            }
            return v / t;
        }

        /// <summary>Габариты рендереров в локальных координатах space (по вершинам ограничивающих коробок мешей).</summary>
        private static Bounds LocalBounds(Transform space, IEnumerable<Renderer> renderers)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (Renderer r in renderers)
            {
                Bounds mb;
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) mb = smr.sharedMesh.bounds;
                else
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    mb = mf.sharedMesh.bounds;
                }

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        /// <summary>Убрать подключённую модель и включить обратно простые фигуры.</summary>
        private static void RemoveImported(GameObject root)
        {
            foreach (ImportedModel marker in root.GetComponents<ImportedModel>())
            {
                foreach (Renderer r in marker.hiddenRenderers)
                    if (r != null) r.enabled = true;
                Object.DestroyImmediate(marker, true);
            }
            Transform old;
            while ((old = root.transform.Find(ImportedModel.ChildName)) != null) Object.DestroyImmediate(old.gameObject, true);
        }

        private static void EditPrefab(string path, System.Action<GameObject> edit)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Модели в папке: ключ — имя файла до «@» (в нижнем регистре), yaw — угол после «@».</summary>
        private static Dictionary<string, (GameObject model, Vector3? rotation, string path)> FindModels(string folder)
        {
            var result = new Dictionary<string, (GameObject, Vector3?, string)>();
            if (!AssetDatabase.IsValidFolder(folder)) return result;

            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponentInChildren<Renderer>(true) == null) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                Vector3? rotation = null;
                int at = name.IndexOf('@');
                if (at >= 0)
                {
                    rotation = ParseRotation(name.Substring(at + 1));
                    name = name.Substring(0, at);
                }
                result[name.Trim().ToLowerInvariant()] = (go, rotation, path);
            }
            return result;
        }

        /// <summary>
        /// Поворот из имени файла: «180» — вокруг вертикали; «x90», «y180», «z-90» и их сочетания («x-90y180»).
        /// </summary>
        private static Vector3 ParseRotation(string text)
        {
            Vector3 euler = Vector3.zero;
            System.Text.RegularExpressions.MatchCollection parts =
                System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(), @"([xyz]?)(-?\d+(?:\.\d+)?)");
            foreach (System.Text.RegularExpressions.Match m in parts)
            {
                float v = float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                switch (m.Groups[1].Value)
                {
                    case "x": euler.x = v; break;
                    case "z": euler.z = v; break;
                    default: euler.y = v; break;
                }
            }
            return euler;
        }

        private static List<T> LoadAll<T>(string folder) where T : Object
        {
            List<T> list = new List<T>();
            if (!AssetDatabase.IsValidFolder(folder)) return list;
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
