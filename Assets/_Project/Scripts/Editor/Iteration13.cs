using NaSvyazi.Building;
using NaSvyazi.Interaction;
using NaSvyazi.Store;
using NaSvyazi.Story;
using NaSvyazi.UI;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace NaSvyazi.EditorTools
{
    /// <summary>
    /// Итерация 13 (версия 0.2.0) — по отзывам тестировщиков: соседние кофейня и цветочный,
    /// в которые можно зайти; тележка для коробок; швабра и грязь на полу; новый декор;
    /// сюжетные письма; улучшение стеллажей.
    /// </summary>
    public static partial class NaSvyaziTools
    {
        [MenuItem("На связи/Подготовить сцену (итерация 13)", priority = 11)]
        private static void PrepareIteration13()
        {
            GameObject systems = GameObject.Find("GameSystems");
            GameObject layout = GameObject.Find(LayoutRootName);
            if (systems == null || layout == null)
            {
                Debug.LogError("[На связи] Сначала выполните «Перестроить карту (итерация 11)».");
                return;
            }

            // Соседи: старые глухие фасады заменяются магазинами, в которые можно зайти.
            foreach (string old in new[] { "Neighbor_Cafe", "Neighbor_Flowers", "Neighbors" })
            {
                GameObject go;
                while ((go = FindIncludingInactive(old)) != null) Undo.DestroyObjectImmediate(go);
            }
            BuildNeighbors(layout.transform);

            // Каталог мебели пополняется новым декором.
            FurnitureCatalog catalog = CreateFurnitureCatalog();
            OrderUI order = Object.FindAnyObjectByType<OrderUI>();
            if (order != null) SetReference(order, "furnitureCatalog", catalog);
            Save.SaveSystem save = Object.FindAnyObjectByType<Save.SaveSystem>();
            if (save != null) SetReference(save, "furnitureCatalog", catalog);

            // Тележка (выключена до покупки) и швабра на складе.
            HandCart cart = BuildHandCart();
            BuildMop();

            UpgradeManager upgrades = GetOrAdd<UpgradeManager>(systems);
            SetReference(upgrades, "cart", cart);

            CleaningManager cleaning = GetOrAdd<CleaningManager>(systems);
            SetReference(cleaning, "dirtMaterial", TransparentMaterial("M_L_Dirt", new Color(0.3f, 0.23f, 0.16f, 0.55f), 0.7f));

            LetterUI letters = GetOrAdd<LetterUI>(systems);
            LoreManager lore = GetOrAdd<LoreManager>(systems);
            SetReference(lore, "letterUI", letters);

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = systems;
            MarkSceneDirty();
            Debug.Log("[На связи] Итерация 13: кофейня и цветочный, тележка, швабра, декор, письма, улучшение полок. " +
                      "Сохраните сцену (Ctrl+S).");
        }

        // ----------------------------------------------------------------
        // Соседние магазины
        // ----------------------------------------------------------------

        /// <summary>Кофейня «Зерно» (x −20…−14) и цветочный «Вера» (x 8…14): фасад с дверью и зал внутри.</summary>
        private static void BuildNeighbors(Transform layout)
        {
            Transform n = Group(layout, "Neighbors");

            Transform cafe = Group(n, "Cafe");
            ShopFront(cafe, -20f, -14f, -16.3f, -15.1f, -19.5f, -16.6f, new Color(0.42f, 0.29f, 0.21f),
                "КОФЕЙНЯ «ЗЕРНО»", new Color(1f, 0.9f, 0.75f));
            ShopRoom(cafe, -20f, -14.2f, LMat("M_L_Wood", new Color(0.55f, 0.38f, 0.24f), 0.35f),
                LMat("M_L_CafeWall", new Color(0.86f, 0.8f, 0.72f), 0.15f));
            BuildCafeInterior(cafe);

            Transform flowers = Group(n, "Flowers");
            ShopFront(flowers, 8f, 14f, 9.0f, 10.2f, 10.5f, 13.5f, new Color(0.27f, 0.45f, 0.33f),
                "ЦВЕТЫ «ВЕРА»", new Color(0.95f, 0.98f, 0.92f));
            ShopRoom(flowers, 8.2f, 14f, LMat("M_L_FlowerFloor", new Color(0.8f, 0.82f, 0.78f), 0.3f),
                LMat("M_L_FlowerWall", new Color(0.93f, 0.95f, 0.9f), 0.15f));
            BuildFlowerInterior(flowers);
        }

        /// <summary>Фасад соседа: стена, витрина, дверной проём и вывеска.</summary>
        private static void ShopFront(Transform parent, float x0, float x1, float door0, float door1, float win0, float win1,
            Color wallColor, string sign, Color signColor)
        {
            Material wall = LMat($"M_L_Facade_{sign.GetHashCode():X}", wallColor, 0.2f);
            Material frame = LMat("M_L_Frame", new Color(0.16f, 0.17f, 0.19f), 0.4f);
            Material glass = GlassMaterial();
            const float z0 = -10.1f, z1 = -9.9f;

            // Стена кусками: до первого проёма, между проёмами и после второго.
            bool doorFirst = door0 < win0;
            float a0 = doorFirst ? door0 : win0, a1 = doorFirst ? door1 : win1;
            float b0 = doorFirst ? win0 : door0, b1 = doorFirst ? win1 : door1;
            Box(parent, "Wall", new Vector3(x0, 0f, z0), new Vector3(a0, FacadeTop, z1), wall, true);
            Box(parent, "Wall", new Vector3(a1, 0f, z0), new Vector3(b0, FacadeTop, z1), wall, true);
            Box(parent, "Wall", new Vector3(b1, 0f, z0), new Vector3(x1, FacadeTop, z1), wall, true);

            // Над дверью и витриной, под витриной.
            Box(parent, "Wall", new Vector3(door0, 2.3f, z0), new Vector3(door1, FacadeTop, z1), wall, true);
            Box(parent, "Wall", new Vector3(win0, 2.6f, z0), new Vector3(win1, FacadeTop, z1), wall, true);
            Box(parent, "Sill", new Vector3(win0, 0f, z0), new Vector3(win1, 0.6f, z1), wall, true);
            Box(parent, "Glass", new Vector3(win0, 0.6f, -10.02f), new Vector3(win1, 2.6f, -9.98f), glass, true, 0f, false);
            Box(parent, "Frame", new Vector3(win0, 0.56f, -10.06f), new Vector3(win1, 0.6f, -9.94f), frame, false);
            Box(parent, "DoorFrame", new Vector3(door0, 2.26f, -10.12f), new Vector3(door1, 2.3f, -9.88f), frame, false);

            WorldLabel(parent, "Sign", new Vector3((x0 + x1) * 0.5f, 3.4f, -10.12f), Quaternion.identity,
                new Vector2(x1 - x0 - 1f, 0.6f), sign, signColor, 4f);
        }

        /// <summary>Зал соседа 5 м вглубь: пол, потолок, три стены и свет.</summary>
        private static void ShopRoom(Transform parent, float x0, float x1, Material floor, Material wall)
        {
            const float z0 = -10f, z1 = -5f;
            Box(parent, "Floor", new Vector3(x0, -0.2f, z0), new Vector3(x1, 0f, z1), floor, true, 2f);
            Box(parent, "Ceiling", new Vector3(x0, LayoutPlan.WallHeight, z0), new Vector3(x1, LayoutPlan.WallHeight + 0.2f, z1),
                LMat("M_L_StorageCeiling", new Color(0.78f, 0.79f, 0.8f), 0.1f), true);
            Box(parent, "Wall_W", new Vector3(x0 - 0.2f, 0f, z0), new Vector3(x0, LayoutPlan.WallHeight, z1), wall, true);
            Box(parent, "Wall_E", new Vector3(x1, 0f, z0), new Vector3(x1 + 0.2f, LayoutPlan.WallHeight, z1), wall, true);
            Box(parent, "Wall_N", new Vector3(x0 - 0.2f, 0f, z1), new Vector3(x1 + 0.2f, LayoutPlan.WallHeight, z1 + 0.2f), wall, true);

            SpotLight(parent, new Vector3(x0 + (x1 - x0) * 0.3f, LayoutPlan.WallHeight - 0.05f, -7.5f), true);
            SpotLight(parent, new Vector3(x0 + (x1 - x0) * 0.7f, LayoutPlan.WallHeight - 0.05f, -7.5f), true);
        }

        private static void BuildCafeInterior(Transform cafe)
        {
            Material wood = LMat("M_L_DarkWood", new Color(0.3f, 0.2f, 0.13f), 0.35f);
            Material top = LMat("M_L_White", new Color(0.97f, 0.97f, 0.97f), 0.3f);
            Material metal = LMat("M_L_Metal", new Color(0.7f, 0.72f, 0.75f), 0.8f);
            Material black = LMat("M_L_Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);

            // Стойка бариста с кофемашиной; покупка — E по стойке.
            GameObject counter = Box(cafe, "CoffeeCounter", new Vector3(-19.6f, 0f, -6.6f), new Vector3(-16.8f, 1.05f, -6.0f), wood, true);
            Box(cafe, "CounterTop", new Vector3(-19.65f, 1.05f, -6.65f), new Vector3(-16.75f, 1.09f, -5.95f), top, false);
            Box(cafe, "Machine", new Vector3(-19.2f, 1.09f, -6.4f), new Vector3(-18.6f, 1.55f, -6.05f), metal, false);
            Box(cafe, "MachineFront", new Vector3(-19.15f, 1.2f, -6.42f), new Vector3(-18.65f, 1.45f, -6.4f), black, false);
            AddShop(counter, NeighborOffer.Coffee, 150);

            Person(cafe, new Vector3(-18f, 0f, -5.5f), new Color(0.35f, 0.22f, 0.15f));

            Poster(cafe, "Menu", new Vector3(-17.5f, 2.2f, -5.0f), Quaternion.identity, new Vector2(2.2f, 0.9f), black,
                "<b>МЕНЮ</b>\nэспрессо 120 · капучино 150 · латте 170", new Color(1f, 0.9f, 0.75f));

            // Столики со стульями.
            foreach (Vector3 p in new[] { new Vector3(-15.6f, 0f, -7.6f), new Vector3(-18.6f, 0f, -8.8f) })
            {
                PrimitivePart(PrimitiveType.Cylinder, cafe, "Table", p + new Vector3(0f, 0.37f, 0f), new Vector3(0.08f, 0.37f, 0.08f), black, true);
                PrimitivePart(PrimitiveType.Cylinder, cafe, "TableTop", p + new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 0.02f, 0.7f), wood, false);
                foreach (float dx in new[] { -0.55f, 0.55f })
                    PrimitivePart(PrimitiveType.Cylinder, cafe, "Stool", p + new Vector3(dx, 0.23f, 0f), new Vector3(0.35f, 0.23f, 0.35f), wood, true);
            }
        }

        private static void BuildFlowerInterior(Transform flowers)
        {
            Material wood = LMat("M_L_Wood", new Color(0.55f, 0.38f, 0.24f), 0.35f);
            Material bucket = LMat("M_L_Bucket", new Color(0.35f, 0.4f, 0.45f), 0.6f);
            Material leaves = LMat("M_L_Leaves", new Color(0.25f, 0.55f, 0.3f), 0.2f);
            Color[] blooms =
            {
                new Color(0.9f, 0.2f, 0.35f), new Color(1f, 0.85f, 0.3f), new Color(0.95f, 0.95f, 0.95f),
                new Color(0.85f, 0.45f, 0.75f), new Color(1f, 0.55f, 0.25f)
            };

            GameObject counter = Box(flowers, "FlowerCounter", new Vector3(11.2f, 0f, -6.6f), new Vector3(13.6f, 1.0f, -6.0f), wood, true);
            AddShop(counter, NeighborOffer.Flowers, 500);
            Person(flowers, new Vector3(12.4f, 0f, -5.5f), new Color(0.3f, 0.5f, 0.35f));

            // Вёдра с букетами вдоль стен.
            int i = 0;
            foreach (Vector3 p in new[]
                     {
                         new Vector3(8.7f, 0f, -9.3f), new Vector3(8.7f, 0f, -8.5f), new Vector3(8.7f, 0f, -7.7f),
                         new Vector3(8.7f, 0f, -6.9f), new Vector3(8.7f, 0f, -6.1f), new Vector3(10.2f, 0f, -5.5f)
                     })
            {
                PrimitivePart(PrimitiveType.Cylinder, flowers, "Bucket", p + new Vector3(0f, 0.2f, 0f), new Vector3(0.36f, 0.2f, 0.36f), bucket, true);
                PrimitivePart(PrimitiveType.Sphere, flowers, "Leaves", p + new Vector3(0f, 0.55f, 0f), new Vector3(0.45f, 0.35f, 0.45f), leaves, false);
                Material bloom = LMat($"M_L_Bloom_{i % blooms.Length}", blooms[i % blooms.Length], 0.3f);
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI * 0.5f + i;
                    PrimitivePart(PrimitiveType.Sphere, flowers, "Bloom",
                        p + new Vector3(Mathf.Cos(a) * 0.1f, 0.72f + (k % 2) * 0.05f, Mathf.Sin(a) * 0.1f),
                        new Vector3(0.12f, 0.12f, 0.12f), bloom, false);
                }
                i++;
            }
        }

        /// <summary>Продавец за стойкой: фигура из капсулы, головы и фартука.</summary>
        private static void Person(Transform parent, Vector3 feet, Color apron)
        {
            Transform p = Group(parent, "Seller");
            PrimitivePart(PrimitiveType.Capsule, p, "Body", feet + new Vector3(0f, 0.8f, 0f), new Vector3(0.5f, 0.8f, 0.4f),
                LMat("M_L_Shirt", new Color(0.92f, 0.92f, 0.94f), 0.2f), true);
            PrimitivePart(PrimitiveType.Sphere, p, "Head", feet + new Vector3(0f, 1.75f, 0f), new Vector3(0.26f, 0.3f, 0.26f),
                LMat("M_L_Skin", new Color(0.93f, 0.76f, 0.64f), 0.3f), false);
            Box(p, "Apron", feet + new Vector3(-0.22f, 0.45f, -0.21f), feet + new Vector3(0.22f, 1.2f, -0.19f),
                LMat($"M_L_Apron_{apron.GetHashCode():X}", apron, 0.2f), false);
        }

        private static void AddShop(GameObject counter, NeighborOffer offer, int price)
        {
            NeighborShop shop = counter.AddComponent<NeighborShop>();
            SerializedObject so = new SerializedObject(shop);
            so.FindProperty("offer").enumValueIndex = (int)offer;
            so.FindProperty("price").intValue = price;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----------------------------------------------------------------
        // Тележка и швабра
        // ----------------------------------------------------------------

        private static HandCart BuildHandCart()
        {
            GameObject old;
            while ((old = FindIncludingInactive("HandCart")) != null) Undo.DestroyObjectImmediate(old);

            Material frame = LMat("M_L_CartFrame", new Color(0.2f, 0.36f, 0.62f), 0.4f);
            Material deck = LMat("M_L_Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);
            Material grip = LMat("M_L_Magenta", new Color(0.9f, 0.12f, 0.6f), 0.4f);

            GameObject root = new GameObject("HandCart");
            Undo.RegisterCreatedObjectUndo(root, "Тележка");
            Transform r = root.transform;

            // Модель строится в начале координат, потом тележка ставится на место.
            Box(r, "Deck", new Vector3(-0.46f, 0.2f, -0.35f), new Vector3(0.46f, 0.25f, 0.35f), deck, true);
            Box(r, "LipL", new Vector3(-0.47f, 0.25f, -0.35f), new Vector3(-0.44f, 0.3f, 0.35f), frame, false);
            Box(r, "LipR", new Vector3(0.44f, 0.25f, -0.35f), new Vector3(0.47f, 0.3f, 0.35f), frame, false);
            foreach (float x in new[] { -0.32f, 0.32f })
                Box(r, "Post", new Vector3(x - 0.02f, 0.25f, -0.4f), new Vector3(x + 0.02f, 0.95f, -0.36f), frame, true);
            Box(r, "Handle", new Vector3(-0.36f, 0.93f, -0.42f), new Vector3(0.36f, 0.97f, -0.36f), grip, true);
            foreach (float x in new[] { -0.38f, 0.38f })
                foreach (float z in new[] { -0.27f, 0.27f })
                {
                    GameObject wheel = PrimitivePart(PrimitiveType.Cylinder, r, "Wheel", new Vector3(x, 0.09f, z),
                        new Vector3(0.18f, 0.03f, 0.18f), deck, false);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }

            // Места под коробки: 2×2 на платформе и ещё ярус сверху.
            StorageRack rack = root.AddComponent<StorageRack>();
            SerializedObject so = new SerializedObject(rack);
            SerializedProperty slots = so.FindProperty("slots");
            Vector3[] positions =
            {
                new Vector3(-0.22f, 0.405f, -0.16f), new Vector3(0.22f, 0.405f, -0.16f),
                new Vector3(-0.22f, 0.405f, 0.16f), new Vector3(0.22f, 0.405f, 0.16f),
                new Vector3(-0.22f, 0.71f, -0.16f), new Vector3(0.22f, 0.71f, -0.16f),
                new Vector3(-0.22f, 0.71f, 0.16f), new Vector3(0.22f, 0.71f, 0.16f)
            };
            slots.arraySize = positions.Length;
            for (int i = 0; i < positions.Length; i++) slots.GetArrayElementAtIndex(i).vector3Value = positions[i];
            so.FindProperty("placeName").stringValue = "на тележку";
            so.ApplyModifiedPropertiesWithoutUndo();

            HandCart cart = root.AddComponent<HandCart>();
            SerializedObject cso = new SerializedObject(cart);
            cso.FindProperty("leftGrip").vector3Value = new Vector3(-0.3f, 0.95f, -0.39f);
            cso.FindProperty("rightGrip").vector3Value = new Vector3(0.3f, 0.95f, -0.39f);
            cso.ApplyModifiedPropertiesWithoutUndo();

            // Покупатели обходят тележку, но в навигационную сетку она не «впекается».
            NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(0.95f, 1f, 0.8f);
            obstacle.center = new Vector3(0f, 0.5f, 0f);
            obstacle.carving = false;
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            root.transform.position = new Vector3(-9f, 0f, -12.6f);
            root.SetActive(false);
            return cart;
        }

        /// <summary>Швабра на складе у двери в зал (лежит на полу) и ведро рядом.</summary>
        private static void BuildMop()
        {
            GameObject old;
            while ((old = FindIncludingInactive("Mop")) != null) Undo.DestroyObjectImmediate(old);
            while ((old = FindIncludingInactive("MopBucket")) != null) Undo.DestroyObjectImmediate(old);

            Material stick = LMat("M_L_MopStick", new Color(0.86f, 0.18f, 0.55f), 0.4f);
            Material head = LMat("M_L_MopHead", new Color(0.82f, 0.82f, 0.78f), 0.1f);

            GameObject root = new GameObject("Mop");
            Undo.RegisterCreatedObjectUndo(root, "Швабра");
            Transform r = root.transform;
            PrimitivePart(PrimitiveType.Cylinder, r, "Stick", new Vector3(0f, 0.1f, 0f), new Vector3(0.03f, 0.6f, 0.03f), stick, true);
            Box(r, "Head", new Vector3(-0.17f, -0.56f, -0.06f), new Vector3(0.17f, -0.5f, 0.06f), head, true);
            Box(r, "Strings", new Vector3(-0.16f, -0.62f, -0.07f), new Vector3(0.16f, -0.56f, 0.07f), head, false);

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            Mop mop = root.AddComponent<Mop>();
            SerializedObject so = new SerializedObject(mop);
            so.FindProperty("displayName").stringValue = "Швабра";
            so.FindProperty("holdOffset").vector3Value = new Vector3(0.25f, -0.25f, 0.2f);
            so.FindProperty("holdRotation").vector3Value = new Vector3(-35f, 0f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Лежит на полу склада вдоль стены у ворот.
            root.transform.SetPositionAndRotation(new Vector3(-9.3f, 0.07f, -9.5f), Quaternion.Euler(0f, 0f, 90f));

            GameObject bucket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bucket.name = "MopBucket";
            Undo.RegisterCreatedObjectUndo(bucket, "Ведро");
            bucket.transform.SetPositionAndRotation(new Vector3(-8.55f, 0.18f, -9.0f), Quaternion.identity);
            bucket.transform.localScale = new Vector3(0.32f, 0.18f, 0.32f);
            bucket.GetComponent<Renderer>().sharedMaterial = LMat("M_L_Bucket", new Color(0.35f, 0.4f, 0.45f), 0.6f);
        }

        /// <summary>Прозрачный материал URP/Lit (грязь, стекло аквариума).</summary>
        private static Material TransparentMaterial(string name, Color color, float smoothness)
        {
            Material m = LMat(name, color, smoothness);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ----------------------------------------------------------------
        // Новый декор
        // ----------------------------------------------------------------

        /// <summary>Аквариум на тумбе: стекло, вода, песок и рыбки.</summary>
        private static void BuildAquarium(Transform r)
        {
            Material black = LMat("M_L_Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);
            Material sand = LMat("M_L_Sand", new Color(0.86f, 0.78f, 0.58f), 0.1f);
            Material fish = LMat("M_L_Fish", new Color(1f, 0.5f, 0.15f), 0.5f, 0.6f);
            Material plant = LMat("M_L_Leaves", new Color(0.25f, 0.55f, 0.3f), 0.2f);

            Part(r, "Cabinet", new Vector3(0f, 0.375f, 0f), new Vector3(1f, 0.75f, 0.45f), black);
            Part(r, "Water", new Vector3(0f, 1.0f, 0f), new Vector3(0.94f, 0.46f, 0.4f),
                TransparentMaterial("M_L_Water", new Color(0.35f, 0.65f, 0.9f, 0.35f), 0.9f), false);
            Part(r, "Glass", new Vector3(0f, 1.0f, 0f), new Vector3(0.98f, 0.5f, 0.44f), GlassMaterial(), false);
            Part(r, "Sand", new Vector3(0f, 0.78f, 0f), new Vector3(0.94f, 0.05f, 0.4f), sand, false);
            Part(r, "Hood", new Vector3(0f, 1.27f, 0f), new Vector3(1f, 0.04f, 0.45f), black, false);
            foreach (Vector3 p in new[] { new Vector3(-0.25f, 1.0f, 0.05f), new Vector3(0.15f, 1.1f, -0.08f), new Vector3(0.3f, 0.92f, 0.1f) })
                Part(r, "Fish", p, new Vector3(0.08f, 0.04f, 0.02f), fish, false);
            foreach (float x in new[] { -0.38f, 0.36f })
                PrimitivePart(PrimitiveType.Capsule, r, "Weed", new Vector3(x, 0.95f, 0.08f), new Vector3(0.05f, 0.15f, 0.05f), plant, false);
        }

        /// <summary>Неоновая вывеска на стойке: розовая рамка и надпись.</summary>
        private static void BuildNeonSign(Transform r)
        {
            Material black = LMat("M_L_Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);
            Material glow = LMat("M_L_MagentaGlow", new Color(0.93f, 0.1f, 0.62f), 0.5f, 2.5f);

            foreach (float x in new[] { -0.5f, 0.5f })
                Part(r, "Leg", new Vector3(x, 0.45f, 0f), new Vector3(0.04f, 0.9f, 0.04f), black);
            Part(r, "Base", new Vector3(0f, 0.02f, 0f), new Vector3(1.2f, 0.04f, 0.3f), black);
            Part(r, "Board", new Vector3(0f, 1.25f, 0f), new Vector3(1.2f, 0.7f, 0.04f), black);
            Part(r, "GlowTop", new Vector3(0f, 1.55f, 0.025f), new Vector3(1.1f, 0.025f, 0.01f), glow, false);
            Part(r, "GlowBottom", new Vector3(0f, 0.95f, 0.025f), new Vector3(1.1f, 0.025f, 0.01f), glow, false);

            TextMeshPro label = CreateLabel("Neon", r, new Vector3(0f, 1.25f, 0.03f), Quaternion.Euler(0f, 180f, 0f),
                new Vector2(1.05f, 0.45f), new Color(1f, 0.45f, 0.85f));
            label.fontSizeMax = 4f;
            label.fontStyle = FontStyles.Bold;
            label.text = "на связи";
        }

        /// <summary>Ковёр с розовой каймой — под ним можно ставить другую мебель.</summary>
        private static void BuildRug(Transform r)
        {
            Part(r, "Border", new Vector3(0f, 0.005f, 0f), new Vector3(2f, 0.01f, 1.4f),
                LMat("M_L_Magenta", new Color(0.9f, 0.12f, 0.6f), 0.4f));
            Part(r, "Field", new Vector3(0f, 0.011f, 0f), new Vector3(1.8f, 0.004f, 1.2f),
                LMat("M_L_RugField", new Color(0.22f, 0.23f, 0.27f), 0.1f), false);
        }
    }
}
