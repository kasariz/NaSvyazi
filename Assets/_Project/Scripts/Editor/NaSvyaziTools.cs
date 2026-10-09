using System.Collections.Generic;
using System.IO;
using NaSvyazi.Interaction;
using NaSvyazi.Products;
using NaSvyazi.Store;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NaSvyazi.EditorTools
{
    /// <summary>
    /// Инструменты редактора (меню «На связи» в верхней панели Unity).
    /// Автоматизируют рутинную сборку: создают ассеты товаров, префаб коробки,
    /// стеллаж с секциями, тестовые коробки и мусорный бак.
    /// В сборку игры этот код не попадает (папка Editor).
    /// </summary>
    public static class NaSvyaziTools
    {
        private const string ProductsFolder = "Assets/_Project/Data/Products";
        private const string PrefabsFolder = "Assets/_Project/Prefabs";
        private const string MaterialsFolder = "Assets/_Project/Materials";
        private const string BoxPrefabPath = PrefabsFolder + "/ProductBox.prefab";

        // ----------------------------------------------------------------
        // Пункты меню
        // ----------------------------------------------------------------

        [MenuItem("На связи/Подготовить сцену (итерация 2)", priority = 0)]
        private static void PrepareIteration2()
        {
            List<ProductData> products = CreateProducts();
            GameObject boxPrefab = CreateBoxPrefab();

            GameObject shelf = BuildShelf(new Vector3(-3f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f));
            SpawnBoxes(boxPrefab, products, new Vector3(-1.5f, 0.5f, -3f));
            CreateTrashBin(new Vector3(4f, 0f, -7f));

            Selection.activeGameObject = shelf;
            MarkSceneDirty();
            Debug.Log("[На связи] Сцена подготовлена: товары, стеллаж, коробки, мусорный бак. Сохраните сцену (Ctrl+S).");
        }

        [MenuItem("На связи/Создать тестовые товары", priority = 20)]
        private static void MenuCreateProducts()
        {
            List<ProductData> products = CreateProducts();
            Debug.Log($"[На связи] Товаров в папке {ProductsFolder}: {products.Count}");
        }

        [MenuItem("На связи/Добавить стеллаж (в центр окна Scene)", priority = 21)]
        private static void MenuAddShelf()
        {
            Selection.activeGameObject = BuildShelf(GetScenePivot(), Quaternion.identity);
            MarkSceneDirty();
        }

        [MenuItem("На связи/Разложить коробки со всеми товарами", priority = 22)]
        private static void MenuSpawnBoxes()
        {
            SpawnBoxes(CreateBoxPrefab(), CreateProducts(), GetScenePivot() + Vector3.up * 0.5f);
            MarkSceneDirty();
        }

        [MenuItem("На связи/Добавить мусорный бак (в центр окна Scene)", priority = 23)]
        private static void MenuAddTrashBin()
        {
            Selection.activeGameObject = CreateTrashBin(GetScenePivot());
            MarkSceneDirty();
        }

        // ----------------------------------------------------------------
        // Товары
        // ----------------------------------------------------------------

        private static List<ProductData> CreateProducts()
        {
            EnsureFolder(ProductsFolder);

            List<ProductData> list = new List<ProductData>
            {
                CreateProduct("case", "Чехол для смартфона", ProductCategory.Accessories, 250, 590, 12,
                    new Vector3(0.09f, 0.16f, 0.02f), new Color(0.25f, 0.4f, 0.85f)),
                CreateProduct("glass", "Защитное стекло", ProductCategory.Accessories, 120, 390, 20,
                    new Vector3(0.09f, 0.16f, 0.012f), new Color(0.6f, 0.85f, 0.95f)),
                CreateProduct("cable", "Кабель USB-C", ProductCategory.Cables, 150, 450, 16,
                    new Vector3(0.1f, 0.14f, 0.03f), new Color(0.95f, 0.95f, 0.95f)),
                CreateProduct("charger", "Зарядное устройство 20 Вт", ProductCategory.Cables, 450, 1190, 10,
                    new Vector3(0.08f, 0.11f, 0.06f), new Color(0.35f, 0.35f, 0.38f)),
                CreateProduct("sim", "Стартовый пакет SIM", ProductCategory.SimCards, 30, 150, 24,
                    new Vector3(0.06f, 0.09f, 0.01f), new Color(0.85f, 0.2f, 0.55f)),
                CreateProduct("earbuds", "Беспроводные наушники", ProductCategory.Audio, 1200, 2990, 8,
                    new Vector3(0.1f, 0.12f, 0.05f), new Color(0.12f, 0.12f, 0.14f)),
            };

            AssetDatabase.SaveAssets();
            return list;
        }

        private static ProductData CreateProduct(string id, string displayName, ProductCategory category,
            int purchasePrice, int marketPrice, int unitsPerBox, Vector3 size, Color color)
        {
            string path = $"{ProductsFolder}/Product_{id}.asset";
            ProductData existing = AssetDatabase.LoadAssetAtPath<ProductData>(path);
            if (existing != null) return existing; // уже создан — не перезаписываем правки

            ProductData p = ScriptableObject.CreateInstance<ProductData>();
            p.id = id;
            p.displayName = displayName;
            p.category = category;
            p.purchasePrice = purchasePrice;
            p.marketPrice = marketPrice;
            p.unitsPerBox = unitsPerBox;
            p.size = size;
            p.color = color;

            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        // ----------------------------------------------------------------
        // Префаб коробки
        // ----------------------------------------------------------------

        private static GameObject CreateBoxPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPrefabPath);
            if (existing != null) return existing;

            EnsureFolder(PrefabsFolder);
            Vector3 size = new Vector3(0.4f, 0.3f, 0.3f);

            GameObject root = new GameObject("ProductBox");
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = size;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            ProductBox box = root.AddComponent<ProductBox>();

            GameObject mesh = CreateCube("Mesh", root.transform, Vector3.zero, size,
                GetMaterial("M_Box", new Color(0.78f, 0.62f, 0.43f)), keepCollider: false);
            mesh.transform.localPosition = Vector3.zero;

            // Надпись на верхней грани коробки.
            TextMeshPro label = CreateLabel("Label", root.transform,
                new Vector3(0f, size.y * 0.5f + 0.002f, 0f), Quaternion.Euler(90f, 0f, 0f),
                new Vector2(size.x - 0.04f, size.z - 0.04f), new Color(0.15f, 0.1f, 0.05f));

            SerializedObject so = new SerializedObject(box);
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BoxPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SpawnBoxes(GameObject prefab, List<ProductData> products, Vector3 start)
        {
            GameObject parent = new GameObject("Boxes");
            Undo.RegisterCreatedObjectUndo(parent, "Коробки с товаром");

            for (int i = 0; i < products.Count; i++)
            {
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                go.transform.position = start + new Vector3(i * 0.6f, 0f, 0f);
                go.name = $"Box_{products[i].id}";

                SerializedObject so = new SerializedObject(go.GetComponent<ProductBox>());
                so.FindProperty("product").objectReferenceValue = products[i];
                so.FindProperty("count").intValue = -1;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ----------------------------------------------------------------
        // Стеллаж: каркас + 4 полки × 2 секции
        // ----------------------------------------------------------------

        private static GameObject BuildShelf(Vector3 position, Quaternion rotation)
        {
            const float width = 1.8f, depth = 0.45f, height = 1.9f, thickness = 0.03f;
            float[] boardHeights = { 0.08f, 0.53f, 0.98f, 1.43f };

            Material frame = GetMaterial("M_Shelf", new Color(0.88f, 0.88f, 0.9f));

            GameObject root = new GameObject("Shelf");
            root.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(root, "Стеллаж");

            // Каркас (с коллайдерами: сквозь стеллаж не пройти).
            CreateCube("Back", root.transform, new Vector3(0f, height * 0.5f, -depth * 0.5f + thickness * 0.5f),
                new Vector3(width, height, thickness), frame, true);
            CreateCube("Side_L", root.transform, new Vector3(-width * 0.5f + thickness * 0.5f, height * 0.5f, 0f),
                new Vector3(thickness, height, depth), frame, true);
            CreateCube("Side_R", root.transform, new Vector3(width * 0.5f - thickness * 0.5f, height * 0.5f, 0f),
                new Vector3(thickness, height, depth), frame, true);
            CreateCube("Top", root.transform, new Vector3(0f, height - thickness * 0.5f, 0f),
                new Vector3(width, thickness, depth), frame, true);

            float innerWidth = width - thickness * 2f;
            float sectionWidth = innerWidth * 0.5f - 0.02f;
            float sectionDepth = depth - thickness - 0.02f;
            float sectionZ = thickness * 0.5f + 0.005f;

            for (int level = 0; level < boardHeights.Length; level++)
            {
                float y = boardHeights[level];
                CreateCube($"Board_{level + 1}", root.transform, new Vector3(0f, y, 0f),
                    new Vector3(innerWidth, thickness, depth), frame, true);

                for (int side = 0; side < 2; side++)
                {
                    float x = (side == 0 ? -1f : 1f) * innerWidth * 0.25f;
                    string name = $"Section_{level + 1}{(side == 0 ? "A" : "B")}";
                    CreateSection(name, root.transform, new Vector3(x, y + thickness * 0.5f, sectionZ),
                        sectionWidth, sectionDepth, 0.38f, depth * 0.5f - sectionZ, thickness);
                }
            }

            return root;
        }

        private static void CreateSection(string name, Transform parent, Vector3 localPos,
            float width, float depth, float height, float frontOffset, float boardThickness)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            ShelfSection section = go.AddComponent<ShelfSection>();

            // Ценник на переднем крае полки (текст смотрит на покупателя).
            TextMeshPro label = CreateLabel("Label", go.transform,
                new Vector3(0f, -boardThickness * 0.5f, frontOffset + 0.003f), Quaternion.Euler(0f, 180f, 0f),
                new Vector2(width - 0.05f, 0.045f), new Color(0.1f, 0.1f, 0.12f));

            SerializedObject so = new SerializedObject(section);
            so.FindProperty("width").floatValue = width;
            so.FindProperty("depth").floatValue = depth;
            so.FindProperty("height").floatValue = height;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            BoxCollider box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(width, height, depth);
            box.center = new Vector3(0f, height * 0.5f, 0f);

            label.text = "Пусто";
        }

        // ----------------------------------------------------------------
        // Мусорный бак
        // ----------------------------------------------------------------

        private static GameObject CreateTrashBin(Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "TrashBin";
            go.transform.position = position + new Vector3(0f, 0.35f, 0f);
            go.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
            go.GetComponent<Renderer>().sharedMaterial = GetMaterial("M_Trash", new Color(0.2f, 0.25f, 0.22f));
            go.AddComponent<TrashBin>();
            Undo.RegisterCreatedObjectUndo(go, "Мусорный бак");
            return go;
        }

        // ----------------------------------------------------------------
        // Вспомогательные методы
        // ----------------------------------------------------------------

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPos, Vector3 size,
            Material material, bool keepCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider) Object.DestroyImmediate(cube.GetComponent<Collider>());
            return cube;
        }

        private static TextMeshPro CreateLabel(string name, Transform parent, Vector3 localPos,
            Quaternion localRot, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.localPosition = localPos;
            tmp.rectTransform.localRotation = localRot;
            tmp.rectTransform.sizeDelta = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = 0.6f;
            tmp.color = color;
            tmp.text = "";
            return tmp;
        }

        private static Material GetMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureFolder(MaterialsFolder);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material m = new Material(shader);
            m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Vector3 GetScenePivot()
        {
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 p = view != null ? view.pivot : Vector3.zero;
            return new Vector3(p.x, 0f, p.z);
        }

        private static void MarkSceneDirty() =>
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
