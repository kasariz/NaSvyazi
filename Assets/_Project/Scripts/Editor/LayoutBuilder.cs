using System.Collections.Generic;
using System.IO;
using NaSvyazi.Building;
using NaSvyazi.Interaction;
using NaSvyazi.Products;
using NaSvyazi.Store;
using NaSvyazi.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace NaSvyazi.EditorTools
{
    /// <summary>
    /// Итерация 11: новая карта. Маленький салон с витриной на улицу, склад с воротами
    /// (доставка приезжает на улицу), этапы расширения зала и склада, второй зал.
    /// Все стены и пол всех этапов строятся сразу; StagePiece включает нужные.
    /// Числа (границы залов, проёмы, точки) берутся из LayoutPlan — их же использует игра.
    /// </summary>
    public static partial class NaSvyaziTools
    {
        private const string LayoutRootName = "Layout";
        private const string LayoutMeshesFolder = "Assets/_Project/Meshes/Layout";
        private const string LayoutMaterialsFolder = "Assets/_Project/Materials/Layout";
        private const string TexturesFolder = "Assets/_Project/Textures";

        private const float WallHalf = 0.1f;    // половина толщины стены
        private const float FacadeTop = 4.2f;   // высота фасада (выше стен зала — крыши с улицы не видно)

        private static int layoutMeshCounter;

        /// <summary>Сцена уже перестроена под новую карту — старые команды оформления не нужны.</summary>
        private static bool HasNewLayout(string command)
        {
            if (GameObject.Find(LayoutRootName) == null) return false;
            Debug.Log($"[На связи] Карта перестроена (итерация 11) — «{command}» больше не нужно.");
            return true;
        }

        [MenuItem("На связи/Перестроить карту (итерация 11)", priority = 9)]
        private static void PrepareIteration11()
        {
            GameObject systems = GameObject.Find("GameSystems");
            if (systems == null || Object.FindAnyObjectByType<DayManager>() == null)
            {
                Debug.LogError("[На связи] Сначала выполните итерации 3–10.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Перестроить карту",
                    "Старые стены, пол, улица, прилавок и оформление будут удалены и построены заново " +
                    "(касса, терминал, стеллаж и коробки переедут на новые места). Продолжить?",
                    "Перестроить", "Отмена"))
                return;

            RemoveOldEnvironment();

            FurnitureCatalog catalog = CreateFurnitureCatalog();
            Transform layout = BuildLayout();
            BuildCounter();
            MoveStoreObjects();
            GameObject rack = PlaceStarterFurniture(catalog);
            PlaceStarterBoxes(rack);

            // Менеджер расширений (этапы зала и склада).
            GetOrAdd<ExpansionManager>(systems);
            foreach (StagePiece piece in layout.GetComponentsInChildren<StagePiece>(true))
                piece.gameObject.SetActive(piece.VisibleAt(0));

            OrderUI order = Object.FindAnyObjectByType<OrderUI>();
            if (order != null) SetReference(order, "furnitureCatalog", catalog);
            Save.SaveSystem save = Object.FindAnyObjectByType<Save.SaveSystem>();
            if (save != null) SetReference(save, "furnitureCatalog", catalog);

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = layout.gameObject;
            MarkSceneDirty();
            Debug.Log("[На связи] Итерация 11: новая карта — салон 8×10 м, склад с воротами, доставка на улицу, " +
                      "этапы расширения. Сохраните сцену (Ctrl+S).");
        }

        // ----------------------------------------------------------------
        // Удаление старой карты
        // ----------------------------------------------------------------

        /// <summary>Группы старой карты — удаляются, где бы ни находились.</summary>
        private static readonly string[] OldEnvironmentGroups = { "Environment", "Decor", "Expansion_Hall", LayoutRootName };

        /// <summary>Отдельные объекты старой карты — удаляются, только если лежат в корне сцены.</summary>
        private static readonly string[] OldEnvironmentObjects =
        {
            "Counter", "Floor", "Ceiling", "Wall_N", "Wall_E", "Wall_W", "Wall_S", "Wall_S2", "Wall_S_Lintel",
            "Wall_E_Gate", "Wall_E2", "Street", "Curb_W", "Curb_E", "Curb_S"
        };

        private static void RemoveOldEnvironment()
        {
            foreach (string name in OldEnvironmentGroups)
            {
                GameObject go;
                while ((go = FindIncludingInactive(name)) != null)
                {
                    RescueChildren(go.transform);
                    Undo.DestroyObjectImmediate(go);
                }
            }

            foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (System.Array.IndexOf(OldEnvironmentObjects, go.name) >= 0)
                {
                    RescueChildren(go.transform);
                    Undo.DestroyObjectImmediate(go);
                }

            // Старая мебель, кроме стартового стеллажа, удаляется (на новой карте ей нет места).
            foreach (PlacedFurniture f in Object.FindObjectsByType<PlacedFurniture>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (f.name != "Shelf")
                    Undo.DestroyObjectImmediate(f.gameObject);

            if (AssetDatabase.IsValidFolder(LayoutMeshesFolder)) AssetDatabase.DeleteAsset(LayoutMeshesFolder);
        }

        private static readonly string[] RescueNames =
        {
            "CustomerSpawn", "DeliveryPoint", "CheckoutQueue", "Player", "GameSystems", "Navigation", "Boxes",
            "EventSystem", "Cashier"
        };

        /// <summary>Игровые объекты внутри удаляемой старой карты выносятся в корень сцены.</summary>
        private static void RescueChildren(Transform root)
        {
            Stack<Transform> stack = new Stack<Transform>();
            foreach (Transform child in root) stack.Push(child);

            while (stack.Count > 0)
            {
                Transform t = stack.Pop();
                if (ShouldRescue(t))
                {
                    Undo.SetTransformParent(t, null, "Перенос объекта");
                    continue;
                }
                foreach (Transform child in t) stack.Push(child);
            }
        }

        private static bool ShouldRescue(Transform t)
        {
            if (System.Array.IndexOf(RescueNames, t.name) >= 0) return true;
            if (t.GetComponent<Camera>() != null) return true;

            Light light = t.GetComponent<Light>();
            if (light != null && light.type == LightType.Directional) return true;

            foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb is StagePiece) continue;
                string ns = mb.GetType().Namespace;
                if (ns != null && ns.StartsWith("NaSvyazi")) return true;
            }
            return false;
        }

        // ----------------------------------------------------------------
        // Постройка здания
        // ----------------------------------------------------------------

        private static Transform BuildLayout()
        {
            layoutMeshCounter = 0;
            EnsureFolder(LayoutMeshesFolder);

            GameObject root = new GameObject(LayoutRootName);
            Undo.RegisterCreatedObjectUndo(root, "Новая карта");
            Transform r = root.transform;

            BuildStreet(Group(r, "Street"));
            BuildFacade(Group(r, "Facade"));
            BuildSalon(Group(r, "Salon"));
            BuildStorage(Group(r, "Storage"));

            // Покупатели не ходят через дверь склада (игрок — ходит).
            GameObject blocker = new GameObject("InnerDoor_NavBlock");
            blocker.transform.SetParent(r, false);
            blocker.transform.position = new Vector3(-8f, 1f,
                (LayoutPlan.InnerDoorZMin + LayoutPlan.InnerDoorZMax) * 0.5f);
            NavMeshObstacle obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(0.8f, 2f, LayoutPlan.InnerDoorZMax - LayoutPlan.InnerDoorZMin + 0.2f);
            obstacle.carving = true;

            return r;
        }

        // --- Улица ---

        private static void BuildStreet(Transform s)
        {
            Material paving = LMat("M_L_Paving", Color.white, 0.15f, 0f, PavingTexture());
            Material asphalt = LMat("M_L_Asphalt", Color.white, 0.1f, 0f, AsphaltTexture());
            Material curb = LMat("M_L_Curb", new Color(0.72f, 0.72f, 0.7f));
            Material marking = LMat("M_L_Marking", new Color(0.95f, 0.95f, 0.92f));

            Box(s, "Sidewalk", new Vector3(-20f, -0.2f, -18f), new Vector3(14f, 0f, -10f), paving, true, 2f);
            Box(s, "Road", new Vector3(-20f, -0.3f, -28f), new Vector3(14f, -0.1f, -18.2f), asphalt, false, 4f);
            Box(s, "Curb", new Vector3(-20f, -0.1f, -18.2f), new Vector3(14f, 0.12f, -18f), curb, true);
            for (float x = -18f; x < 13f; x += 4f)
                Box(s, "Marking", new Vector3(x, -0.1f, -23.1f), new Vector3(x + 2f, -0.095f, -22.9f), marking, false);

            // Невидимые ограждения: с тротуара не уйти.
            Barrier(s, new Vector3(-20f, 0f, -18.3f), new Vector3(14f, 3f, -18.1f));
            Barrier(s, new Vector3(-20.3f, 0f, -18.3f), new Vector3(-20f, 3f, -9.9f));
            Barrier(s, new Vector3(14f, 0f, -18.3f), new Vector3(14.3f, 3f, -9.9f));

            // Скамейки у витрины (как в торговом центре на фото) и фонари.
            Bench(s, new Vector3(-6.3f, 0f, -10.75f));
            Bench(s, new Vector3(5.5f, 0f, -10.75f));
            StreetLamp(s, new Vector3(-16.5f, 0f, -17.4f));
            StreetLamp(s, new Vector3(3f, 0f, -17.4f));
            StreetLamp(s, new Vector3(11.5f, 0f, -17.4f));
        }

        private static void Bench(Transform parent, Vector3 p)
        {
            Material seat = LMat("M_L_BenchSeat", new Color(0.16f, 0.55f, 0.75f), 0.4f);
            Material frame = LMat("M_L_Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);

            Transform b = Group(parent, "Bench");
            Box(b, "Seat", p + new Vector3(-0.7f, 0.36f, -0.22f), p + new Vector3(0.7f, 0.46f, 0.22f), seat, true);
            foreach (float x in new[] { -0.62f, 0.62f })
            {
                Box(b, "Leg", p + new Vector3(x - 0.02f, 0f, -0.2f), p + new Vector3(x + 0.02f, 0.36f, -0.16f), frame, false);
                Box(b, "Leg", p + new Vector3(x - 0.02f, 0f, 0.16f), p + new Vector3(x + 0.02f, 0.36f, 0.2f), frame, false);
                Box(b, "Foot", p + new Vector3(x - 0.02f, 0f, -0.2f), p + new Vector3(x + 0.02f, 0.03f, 0.2f), frame, false);
            }
        }

        private static void StreetLamp(Transform parent, Vector3 p)
        {
            Material dark = LMat("M_L_Frame", new Color(0.16f, 0.17f, 0.19f), 0.4f);
            Material glow = LMat("M_L_LampWarm", new Color(1f, 0.92f, 0.75f), 0.5f, 2f);

            Transform l = Group(parent, "StreetLamp");
            Box(l, "Pole", p + new Vector3(-0.06f, 0f, -0.06f), p + new Vector3(0.06f, 4.2f, 0.06f), dark, true);
            Box(l, "Arm", p + new Vector3(-0.04f, 4.1f, -0.04f), p + new Vector3(0.04f, 4.18f, 0.9f), dark, false);
            Box(l, "Head", p + new Vector3(-0.15f, 3.98f, 0.7f), p + new Vector3(0.15f, 4.1f, 1.05f), glow, false, 0f, false);
        }

        // --- Фасад (линия z = −10) ---

        private static void BuildFacade(Transform f)
        {
            Material black = LMat("M_L_Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);
            Material magentaGlow = LMat("M_L_MagentaGlow", new Color(0.93f, 0.1f, 0.62f), 0.5f, 2.5f);
            Material storage = LMat("M_L_StorageFacade", new Color(0.56f, 0.58f, 0.6f), 0.2f);
            Material shutter = LMat("M_L_Shutter", new Color(0.66f, 0.68f, 0.7f), 0.35f);
            Material hazard = LMat("M_L_Hazard", new Color(0.96f, 0.76f, 0.1f), 0.3f);

            // Соседи по улице.
            NeighborFacade(f, "Neighbor_Cafe", -20f, -14f, new Color(0.42f, 0.29f, 0.21f), "КОФЕЙНЯ «ЗЕРНО»",
                new Color(1f, 0.9f, 0.75f));
            NeighborFacade(f, "Neighbor_Flowers", 8f, 14f, new Color(0.27f, 0.45f, 0.33f), "ЦВЕТЫ",
                new Color(0.95f, 0.98f, 0.92f));

            // Склад: стена с воротами, короб рольставни, жёлтые отбойники, вывеска.
            Transform st = Group(f, "StorageFront");
            float gx0 = LayoutPlan.GateXMin, gx1 = LayoutPlan.GateXMax;
            Box(st, "Wall", new Vector3(-14f, 0f, -10.1f), new Vector3(gx0, FacadeTop, -9.9f), storage, true);
            Box(st, "Wall", new Vector3(gx1, 0f, -10.1f), new Vector3(-8f, FacadeTop, -9.9f), storage, true);
            Box(st, "Wall", new Vector3(gx0, 2.6f, -10.1f), new Vector3(gx1, FacadeTop, -9.9f), storage, true);
            Box(st, "ShutterBox", new Vector3(gx0 - 0.15f, 2.6f, -10.35f), new Vector3(gx1 + 0.15f, 2.95f, -10.1f), shutter, false);
            Box(st, "Bumper", new Vector3(gx0 - 0.12f, 0f, -10.16f), new Vector3(gx0, 1.2f, -10.1f), hazard, false);
            Box(st, "Bumper", new Vector3(gx1, 0f, -10.16f), new Vector3(gx1 + 0.12f, 1.2f, -10.1f), hazard, false);
            Box(st, "SignPlate", new Vector3(-13.7f, 3.15f, -10.16f), new Vector3(-8.3f, 3.85f, -10.1f), black, false);
            WorldLabel(st, "SignText", new Vector3(-11f, 3.5f, -10.17f), Quaternion.identity, new Vector2(5.2f, 0.55f),
                "СКЛАД  ·  ПРИЁМ ТОВАРА", Color.white, 4f);

            // Салон: сегмент 0 (x −8…0) — всегда витрина со входом.
            Transform a = Group(f, "SalonFront_0");
            Storefront(a, -8f, 0f, true);
            Box(a, "Header", new Vector3(-8.1f, 3f, -10.2f), new Vector3(0f, FacadeTop, -9.9f), black, true);
            Box(a, "Strip", new Vector3(-7.6f, 3.62f, -10.23f), new Vector3(-5.5f, 3.7f, -10.2f), magentaGlow, false, 0f, false);
            Box(a, "Strip", new Vector3(-2.5f, 3.62f, -10.23f), new Vector3(-0.4f, 3.7f, -10.2f), magentaGlow, false, 0f, false);
            Box(a, "LogoDot", new Vector3(-5.05f, 3.8f, -10.23f), new Vector3(-4.87f, 3.98f, -10.2f), magentaGlow, false, 0f, false);
            WorldLabel(a, "Logo", new Vector3(-3.95f, 3.62f, -10.24f), Quaternion.identity, new Vector2(2.1f, 0.7f),
                "<b>на связи</b>", Color.white, 6f);

            // Сегменты 1 и 2: до покупки — «Аренда», после — продолжение витрины.
            FacadeSegment(f, 1, 0f, 4f, "АРЕНДА\n48 м²");
            FacadeSegment(f, 2, 4f, 8f, "АРЕНДА\n48 м²");
        }

        private static void FacadeSegment(Transform f, int stage, float x0, float x1, string rentText)
        {
            Material black = LMat("M_L_Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);
            Material magentaGlow = LMat("M_L_MagentaGlow", new Color(0.93f, 0.1f, 0.62f), 0.5f, 2.5f);
            Material rent = LMat("M_L_RentWall", new Color(0.8f, 0.8f, 0.78f), 0.2f);
            Material plate = LMat("M_L_White", new Color(0.97f, 0.97f, 0.97f), 0.3f);

            Transform closed = Group(f, $"SalonFront_{stage}_Rent", LayoutZone.Salon, 0, stage - 1);
            Box(closed, "Wall", new Vector3(x0, 0f, -10.1f), new Vector3(x1, FacadeTop, -9.9f), rent, true);
            float cx = (x0 + x1) * 0.5f;
            Box(closed, "Plate", new Vector3(cx - 1.1f, 1.3f, -10.13f), new Vector3(cx + 1.1f, 2.3f, -10.1f), plate, false);
            WorldLabel(closed, "RentText", new Vector3(cx, 1.8f, -10.14f), Quaternion.identity, new Vector2(2f, 0.9f),
                rentText, new Color(0.1f, 0.1f, 0.12f), 3f);

            Transform open = Group(f, $"SalonFront_{stage}", LayoutZone.Salon, stage);
            Storefront(open, x0, x1, false);
            Box(open, "Header", new Vector3(x0, 3f, -10.2f), new Vector3(x1, FacadeTop, -9.9f), black, true);
            Box(open, "Strip", new Vector3(x0 + 0.4f, 3.62f, -10.23f), new Vector3(x1 - 0.4f, 3.7f, -10.2f), magentaGlow,
                false, 0f, false);
        }

        /// <summary>Стеклянная витрина от x0 до x1: стекло, стойки, нижний профиль, при необходимости — вход.</summary>
        private static void Storefront(Transform parent, float x0, float x1, bool withDoor)
        {
            Material glass = GlassMaterial();
            Material frame = LMat("M_L_Frame", new Color(0.16f, 0.17f, 0.19f), 0.4f);
            float d0 = LayoutPlan.DoorXMin, d1 = LayoutPlan.DoorXMax;

            List<Vector2> panes = new List<Vector2>();
            if (withDoor && d0 > x0 && d1 < x1)
            {
                panes.Add(new Vector2(x0, d0));
                panes.Add(new Vector2(d1, x1));
            }
            else panes.Add(new Vector2(x0, x1));

            foreach (Vector2 pane in panes)
            {
                Box(parent, "Glass", new Vector3(pane.x, 0.08f, -10.02f), new Vector3(pane.y, 3f, -9.98f), glass, true, 0f, false);
                Box(parent, "Rail", new Vector3(pane.x, 0f, -10.06f), new Vector3(pane.y, 0.08f, -9.94f), frame, false);
                // Вертикальные стойки примерно через 2 м.
                int spans = Mathf.Max(1, Mathf.RoundToInt((pane.y - pane.x) / 2f));
                for (int i = 0; i <= spans; i++)
                {
                    float x = Mathf.Lerp(pane.x, pane.y, (float)i / spans);
                    Box(parent, "Mullion", new Vector3(x - 0.04f, 0f, -10.06f), new Vector3(x + 0.04f, 3f, -9.94f), frame, true);
                }
            }

            if (withDoor && d0 > x0 && d1 < x1)
            {
                // Над входом — стеклянная фрамуга.
                Box(parent, "DoorHead", new Vector3(d0, 2.3f, -10.06f), new Vector3(d1, 2.36f, -9.94f), frame, true);
                Box(parent, "Transom", new Vector3(d0, 2.36f, -10.02f), new Vector3(d1, 3f, -9.98f), glass, true, 0f, false);
            }
        }

        private static void NeighborFacade(Transform parent, string name, float x0, float x1, Color wall, string sign,
            Color signColor)
        {
            Material wallMat = LMat($"M_L_{name}", wall, 0.2f);
            Material window = LMat("M_L_DarkGlass", new Color(0.1f, 0.12f, 0.14f), 0.9f);
            Material frame = LMat("M_L_Frame", new Color(0.16f, 0.17f, 0.19f), 0.4f);

            Transform n = Group(parent, name);
            Box(n, "Wall", new Vector3(x0, 0f, -10.1f), new Vector3(x1, FacadeTop, -9.9f), wallMat, true);
            Box(n, "Window", new Vector3(x0 + 0.6f, 0.5f, -10.13f), new Vector3(x1 - 0.6f, 2.7f, -10.1f), window, false);
            Box(n, "WindowFrame", new Vector3(x0 + 0.55f, 0.45f, -10.12f), new Vector3(x1 - 0.55f, 0.5f, -10.1f), frame, false);
            float cx = (x0 + x1) * 0.5f;
            WorldLabel(n, "Sign", new Vector3(cx, 3.5f, -10.12f), Quaternion.identity, new Vector2(x1 - x0 - 1f, 0.6f),
                sign, signColor, 4f);
        }

        // --- Торговый зал ---

        private static void BuildSalon(Transform s)
        {
            Material floor = LMat("M_L_SalonFloor", Color.white, 0.35f, 0f, SalonTilesTexture());
            Material ceiling = LMat("M_L_Ceiling", Color.white, 0.1f, 0f, CeilingTexture());
            Material wall = LMat("M_L_WallWhite", new Color(0.95f, 0.95f, 0.94f), 0.15f);

            // Пол, потолок, светильники каждого этапа.
            for (int i = 0; i < LayoutPlan.Salon.Length; i++)
            {
                Transform g = Group(s, $"Stage_{i}_Room", LayoutZone.Salon, i);
                foreach (Rect a in LayoutPlan.Salon[i].areas)
                    RoomShell(g, a, floor, 2.4f, ceiling, true);
            }

            float d0 = LayoutPlan.InnerDoorZMin, d1 = LayoutPlan.InnerDoorZMax;
            float a0 = LayoutPlan.ArchXMin, a1 = LayoutPlan.ArchXMax;

            // Западная стена (общая со складом) с дверью на склад.
            Transform w = Group(s, "Wall_West");
            WallZ(w, -8f, -10f, d0, wall);
            WallZ(w, -8f, d1, 0f, wall);
            Box(w, "DoorLintel", new Vector3(-8f - WallHalf, 2.2f, d0), new Vector3(-8f + WallHalf, 3f, d1), wall, true);
            DoorFrameZ(w, -8f, d0, d1, 2.2f);

            // Задняя стена зала (z = 0): участки по этапам и арка во второй зал.
            Transform n0 = Group(s, "Wall_Back_0", LayoutZone.Salon, 0);
            WallX(n0, 0f, -8f, 0f, wall);
            Transform n1 = Group(s, "Wall_Back_1", LayoutZone.Salon, 1);
            WallX(n1, 0f, 0f, a0, wall);
            Transform n1b = Group(s, "Wall_Back_1_ArchPart", LayoutZone.Salon, 1, 2);
            WallX(n1b, 0f, a0, 4f, wall);
            Transform n2a = Group(s, "Wall_Back_2_ArchPart", LayoutZone.Salon, 2, 2);
            WallX(n2a, 0f, 4f, a1, wall);
            Transform n2 = Group(s, "Wall_Back_2", LayoutZone.Salon, 2);
            WallX(n2, 0f, a1, 8f, wall);
            Transform arch = Group(s, "Arch", LayoutZone.Salon, 3);
            Box(arch, "Lintel", new Vector3(a0, 2.4f, -WallHalf), new Vector3(a1, 3f, WallHalf), wall, true);
            DoorFrameX(arch, 0f, a0, a1, 2.4f);

            // Восточная стена — там, где сейчас кончается зал.
            WallZ(Group(s, "Wall_East_0", LayoutZone.Salon, 0, 0), 0f, -10f, 0f, wall);
            WallZ(Group(s, "Wall_East_1", LayoutZone.Salon, 1, 1), 4f, -10f, 0f, wall);
            WallZ(Group(s, "Wall_East_2", LayoutZone.Salon, 2), 8f, -10f, 0f, wall);

            // Второй зал (x 0…8, z 0…7, затем до 12).
            Transform r3 = Group(s, "Room2_Walls", LayoutZone.Salon, 3);
            WallZ(r3, 0f, 0f, 7f, wall);
            WallZ(r3, 8f, 0f, 7f, wall);
            WallX(Group(s, "Room2_Back_3", LayoutZone.Salon, 3, 3), 7f, 0f, 8f, wall);
            Transform r4 = Group(s, "Room2_Walls_Ext", LayoutZone.Salon, 4);
            WallZ(r4, 0f, 7f, 12f, wall);
            WallZ(r4, 8f, 7f, 12f, wall);
            WallX(r4, 12f, 0f, 8f, wall);

            DecorateSalonInterior(s);
        }

        /// <summary>Оформление в стиле салона связи: чёрно-розовые акценты на белых стенах.</summary>
        private static void DecorateSalonInterior(Transform s)
        {
            Material black = LMat("M_L_Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);
            Material magenta = LMat("M_L_Magenta", new Color(0.9f, 0.12f, 0.6f), 0.4f);
            Material magentaGlow = LMat("M_L_MagentaGlow", new Color(0.93f, 0.1f, 0.62f), 0.5f, 2.5f);
            Material mat = LMat("M_L_Mat", new Color(0.18f, 0.18f, 0.2f), 0.05f);
            Material wire = LMat("M_L_Frame", new Color(0.16f, 0.17f, 0.19f), 0.4f);

            Transform d = Group(s, "Decor");
            Quaternion facingStreet = Quaternion.identity;           // текст читается со стороны −Z
            Quaternion facingEast = Quaternion.Euler(0f, -90f, 0f);  // на западной стене, читается из зала
            Quaternion facingWest = Quaternion.Euler(0f, 90f, 0f);   // на восточной стене

            // Вывеска за прилавком: чёрный короб с логотипом и розовой подсветкой снизу.
            Box(d, "BackSign", new Vector3(-6.3f, 2.15f, -0.15f), new Vector3(-1.7f, 2.85f, -WallHalf), black, false);
            Box(d, "BackSignGlow", new Vector3(-6.3f, 2.06f, -0.15f), new Vector3(-1.7f, 2.12f, -WallHalf), magentaGlow,
                false, 0f, false);
            WorldLabel(d, "BackSignText", new Vector3(-4f, 2.5f, -0.16f), facingStreet, new Vector2(4f, 0.6f),
                "<b>на связи</b>", Color.white, 5f);

            // Большой постер над стеллажом на западной стене (как на фото над витриной).
            Poster(d, "Poster_Number", new Vector3(-8f + WallHalf, 2.45f, -4.2f), facingEast, new Vector2(3.4f, 0.85f),
                black, "<b>ПЕРЕНЕСЁМ ВАШ НОМЕР</b>\nза один день", Color.white);
            Poster(d, "Poster_Tariffs", new Vector3(-8f + WallHalf, 2.45f, -1.6f), facingEast, new Vector2(1.6f, 0.85f),
                magenta, "<b>ТАРИФЫ</b>\nот 300 ₽/мес", Color.white);

            // Табличка над дверью склада.
            Poster(d, "Sign_Storage", new Vector3(-8f + WallHalf, 2.55f, -6.6f), facingEast, new Vector2(1.2f, 0.3f),
                black, "СКЛАД", Color.white);

            // Коврик у входа.
            Box(d, "EntranceMat", new Vector3(LayoutPlan.DoorXMin - 0.1f, 0f, -9.9f),
                new Vector3(LayoutPlan.DoorXMax + 0.1f, 0.012f, -8.7f), mat, false);

            // Подвесной лайтбокс за стеклом витрины (видно с улицы и из зала).
            Transform lb = Group(d, "Lightbox");
            Box(lb, "Frame", new Vector3(-6f, 0.9f, -9.62f), new Vector3(-4.9f, 2.5f, -9.5f), black, false);
            Box(lb, "Panel", new Vector3(-5.93f, 0.97f, -9.63f), new Vector3(-4.97f, 1.55f, -9.49f), magentaGlow, false, 0f, false);
            Box(lb, "Wire", new Vector3(-5.9f, 2.5f, -9.57f), new Vector3(-5.89f, 3f, -9.56f), wire, false);
            Box(lb, "Wire", new Vector3(-5.01f, 2.5f, -9.57f), new Vector3(-5f, 3f, -9.56f), wire, false);
            WorldLabel(lb, "Front", new Vector3(-5.45f, 1.95f, -9.64f), facingStreet, new Vector2(0.95f, 0.8f),
                "<b>−50 %</b>\nна смартфоны", Color.white, 2.2f);
            WorldLabel(lb, "FrontSub", new Vector3(-5.45f, 1.26f, -9.645f), facingStreet, new Vector2(0.9f, 0.45f),
                "при подключении", Color.white, 1.2f);
            WorldLabel(lb, "Back", new Vector3(-5.45f, 1.95f, -9.48f), Quaternion.Euler(0f, 180f, 0f),
                new Vector2(0.95f, 0.8f), "<b>−50 %</b>\nна смартфоны", Color.white, 2.2f);

            // Постеры на восточных стенах каждого этапа (исчезают вместе со стеной).
            Poster(Group(s, "Decor_East_0", LayoutZone.Salon, 0, 0), "Poster_Internet",
                new Vector3(0f - WallHalf, 1.9f, -5.5f), facingWest, new Vector2(2.4f, 1.6f), magenta,
                "<b>БЕЗЛИМИТНЫЙ\nИНТЕРНЕТ</b>\nв выходные и праздники\n<b>от 350 ₽</b>", Color.white);
            Poster(Group(s, "Decor_East_1", LayoutZone.Salon, 1, 1), "Poster_Internet",
                new Vector3(4f - WallHalf, 1.9f, -5.5f), facingWest, new Vector2(2.4f, 1.6f), magenta,
                "<b>БЕЗЛИМИТНЫЙ\nИНТЕРНЕТ</b>\nв выходные и праздники\n<b>от 350 ₽</b>", Color.white);
            Transform e2 = Group(s, "Decor_East_2", LayoutZone.Salon, 2);
            Poster(e2, "Poster_Internet", new Vector3(8f - WallHalf, 1.9f, -6.5f), facingWest, new Vector2(2.4f, 1.6f),
                magenta, "<b>БЕЗЛИМИТНЫЙ\nИНТЕРНЕТ</b>\nв выходные и праздники\n<b>от 350 ₽</b>", Color.white);
            Poster(e2, "Poster_Home", new Vector3(8f - WallHalf, 1.9f, -3f), facingWest, new Vector2(2f, 1.6f),
                black, "<b>ДОМАШНИЙ\nИНТЕРНЕТ</b>\nи ТВ\nподключим за день", Color.white);

            // Вывеска над аркой во второй зал.
            Transform arch = Group(s, "Decor_Arch", LayoutZone.Salon, 3);
            Poster(arch, "Sign_Room2", new Vector3((LayoutPlan.ArchXMin + LayoutPlan.ArchXMax) * 0.5f, 2.7f, -WallHalf),
                facingStreet, new Vector2(2.9f, 0.45f), black, "<b>ЗАЛ СМАРТФОНОВ</b>", Color.white);

            Poster(Group(s, "Decor_Room2_3", LayoutZone.Salon, 3, 3), "Poster_Phones",
                new Vector3(4f, 1.9f, 7f - WallHalf), facingStreet, new Vector2(3f, 1.5f), black,
                "<b>НОВЫЕ СМАРТФОНЫ</b>\nуже в продаже\n<b>рассрочка 0 %</b>", Color.white);
            Poster(Group(s, "Decor_Room2_4", LayoutZone.Salon, 4), "Poster_Phones",
                new Vector3(4f, 1.9f, 12f - WallHalf), facingStreet, new Vector2(3f, 1.5f), black,
                "<b>НОВЫЕ СМАРТФОНЫ</b>\nуже в продаже\n<b>рассрочка 0 %</b>", Color.white);
        }

        // --- Склад ---

        private static void BuildStorage(Transform s)
        {
            Material floor = LMat("M_L_Concrete", Color.white, 0.2f, 0f, ConcreteTexture());
            Material ceiling = LMat("M_L_StorageCeiling", new Color(0.78f, 0.79f, 0.8f), 0.1f);
            Material wall = LMat("M_L_WallStorage", new Color(0.8f, 0.81f, 0.82f), 0.1f);

            for (int i = 0; i < LayoutPlan.Storage.Length; i++)
            {
                Transform g = Group(s, $"Stage_{i}_Room", LayoutZone.Storage, i);
                foreach (Rect a in LayoutPlan.Storage[i].areas)
                    RoomShell(g, a, floor, 4f, ceiling, false);

                Rect area = LayoutPlan.Storage[i].areas[0];
                // Западная стена этого участка.
                WallZ(g, -14f, area.yMin, area.yMax, wall);

                // Северная стена — только пока склад не расширен дальше.
                Transform north = Group(s, $"Wall_North_{i}", LayoutZone.Storage, i, i);
                WallX(north, area.yMax, -14f, -8f, wall);
            }

            // Восточная стена за пределами зала (склад вырос глубже задней стены салона).
            WallZ(Group(s, "Wall_East_2", LayoutZone.Storage, 2), -8f, 0f, LayoutPlan.Storage[2].areas[0].yMax, wall);

            // Разметка у ворот изнутри.
            Material hazard = LMat("M_L_Hazard", new Color(0.96f, 0.76f, 0.1f), 0.3f);
            Box(s, "GateLine", new Vector3(LayoutPlan.GateXMin, 0f, -9.75f), new Vector3(LayoutPlan.GateXMax, 0.01f, -9.6f),
                hazard, false);
        }

        // ----------------------------------------------------------------
        // Строительные блоки
        // ----------------------------------------------------------------

        /// <summary>Пол, потолок, светильники и точечный свет для прямоугольного участка.</summary>
        private static void RoomShell(Transform parent, Rect a, Material floor, float floorTile, Material ceiling,
            bool salon)
        {
            Box(parent, "Floor", new Vector3(a.xMin, -0.2f, a.yMin), new Vector3(a.xMax, 0f, a.yMax), floor, true, floorTile);
            Box(parent, "Ceiling", new Vector3(a.xMin, LayoutPlan.WallHeight, a.yMin),
                new Vector3(a.xMax, LayoutPlan.WallHeight + 0.2f, a.yMax), ceiling, true, salon ? 2.4f : 0f, false);

            Material lamp = LMat("M_L_Lamp", new Color(1f, 0.98f, 0.93f), 0.5f, 2.2f);
            if (salon)
            {
                // Круглые встроенные светильники сеткой ~2 м.
                for (float x = a.xMin + 1f; x < a.xMax; x += 2f)
                    for (float z = a.yMin + 1f; z < a.yMax; z += 2f)
                    {
                        GameObject disc = PrimitivePart(PrimitiveType.Cylinder, parent, "Downlight",
                            new Vector3(x, LayoutPlan.WallHeight - 0.01f, z), new Vector3(0.22f, 0.01f, 0.22f), lamp, false);
                        disc.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }
            }
            else
            {
                // Линейные лампы на складе.
                for (float z = a.yMin + 1.5f; z < a.yMax; z += 3f)
                    Box(parent, "TubeLamp", new Vector3(a.center.x - 0.8f, LayoutPlan.WallHeight - 0.05f, z - 0.06f),
                        new Vector3(a.center.x + 0.8f, LayoutPlan.WallHeight, z + 0.06f), lamp, false, 0f, false);
            }

            // Точечный свет: по одному на каждые ~40 м².
            int lights = Mathf.Max(1, Mathf.RoundToInt(a.width * a.height / 40f));
            bool alongX = a.width >= a.height;
            for (int i = 0; i < lights; i++)
            {
                float t = (i + 0.5f) / lights;
                Vector3 p = alongX
                    ? new Vector3(Mathf.Lerp(a.xMin, a.xMax, t), 2.6f, a.center.y)
                    : new Vector3(a.center.x, 2.6f, Mathf.Lerp(a.yMin, a.yMax, t));
                GameObject go = new GameObject("PointLight");
                go.transform.SetParent(parent, false);
                go.transform.position = p;
                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = salon ? 8f : 7f;
                light.intensity = salon ? 1.4f : 1.1f;
                light.color = salon ? new Color(1f, 0.97f, 0.93f) : new Color(0.95f, 0.97f, 1f);
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>Стена вдоль X (на линии z) от x0 до x1, с перекрытием углов.</summary>
        private static void WallX(Transform parent, float z, float x0, float x1, Material m) =>
            Box(parent, "Wall", new Vector3(x0 - WallHalf, 0f, z - WallHalf),
                new Vector3(x1 + WallHalf, LayoutPlan.WallHeight, z + WallHalf), m, true);

        /// <summary>Стена вдоль Z (на линии x) от z0 до z1.</summary>
        private static void WallZ(Transform parent, float x, float z0, float z1, Material m) =>
            Box(parent, "Wall", new Vector3(x - WallHalf, 0f, z0), new Vector3(x + WallHalf, LayoutPlan.WallHeight, z1), m, true);

        /// <summary>Чёрный наличник проёма в стене вдоль Z.</summary>
        private static void DoorFrameZ(Transform parent, float x, float z0, float z1, float height)
        {
            Material frame = LMat("M_L_Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);
            float t = WallHalf + 0.02f;
            Box(parent, "Jamb", new Vector3(x - t, 0f, z0), new Vector3(x + t, height, z0 + 0.06f), frame, false);
            Box(parent, "Jamb", new Vector3(x - t, 0f, z1 - 0.06f), new Vector3(x + t, height, z1), frame, false);
            Box(parent, "Head", new Vector3(x - t, height - 0.06f, z0), new Vector3(x + t, height, z1), frame, false);
        }

        /// <summary>Чёрный наличник проёма в стене вдоль X.</summary>
        private static void DoorFrameX(Transform parent, float z, float x0, float x1, float height)
        {
            Material frame = LMat("M_L_Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);
            float t = WallHalf + 0.02f;
            Box(parent, "Jamb", new Vector3(x0, 0f, z - t), new Vector3(x0 + 0.06f, height, z + t), frame, false);
            Box(parent, "Jamb", new Vector3(x1 - 0.06f, 0f, z - t), new Vector3(x1, height, z + t), frame, false);
            Box(parent, "Head", new Vector3(x0, height - 0.06f, z - t), new Vector3(x1, height, z + t), frame, false);
        }

        /// <summary>Постер на стене: подложка и текст перед ней. position — точка на поверхности стены.</summary>
        private static void Poster(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size,
            Material background, string text, Color textColor)
        {
            Vector3 toViewer = rotation * Vector3.back;
            GameObject back = CreateCube(name, parent, Vector3.zero, new Vector3(size.x, size.y, 0.02f), background, false);
            back.transform.SetPositionAndRotation(position + toViewer * 0.011f, rotation);
            // Размер шрифта TextMeshPro в мире ≈ 10 единиц на метр высоты строки; автоподбор уменьшит, если не влезет.
            WorldLabel(parent, name + "_Text", position + toViewer * 0.024f, rotation, size * 0.9f, text, textColor,
                size.y * 3.5f);
        }

        /// <summary>Группа (пустой объект в начале координат), при необходимости — часть этапа.</summary>
        private static Transform Group(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform Group(Transform parent, string name, LayoutZone zone, int minStage, int maxStage = 99)
        {
            Transform t = Group(parent, name);
            t.gameObject.AddComponent<StagePiece>().Setup(zone, minStage, maxStage);
            return t;
        }

        /// <summary>
        /// Параллелепипед между углами min и max (мировые координаты).
        /// uvMeters &gt; 0 — текстура кладётся по мировым координатам (плитка не растягивается).
        /// </summary>
        private static GameObject Box(Transform parent, string name, Vector3 min, Vector3 max, Material m,
            bool collider, float uvMeters = 0f, bool castShadows = true)
        {
            Vector3 size = max - min;
            Vector3 center = (min + max) * 0.5f;
            GameObject go;

            if (uvMeters > 0f)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = center;
                go.AddComponent<MeshFilter>().sharedMesh = TiledBoxMesh(size, center, uvMeters);
                go.AddComponent<MeshRenderer>().sharedMaterial = m;
                if (collider) go.AddComponent<BoxCollider>().size = size;
            }
            else
            {
                go = CreateCube(name, parent, Vector3.zero, size, m, collider);
                go.transform.position = center;
            }

            if (!castShadows) go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Невидимая преграда (только коллайдер).</summary>
        private static void Barrier(Transform parent, Vector3 min, Vector3 max)
        {
            GameObject go = new GameObject("Barrier");
            go.transform.SetParent(parent, false);
            go.transform.position = (min + max) * 0.5f;
            go.AddComponent<BoxCollider>().size = max - min;
        }

        /// <summary>Меш-куб с UV в мировых метрах (сохраняется ассетом, чтобы сцена на него ссылалась).</summary>
        private static Mesh TiledBoxMesh(Vector3 size, Vector3 center, float tile)
        {
            MeshData data = new MeshData { half = size * 0.5f, center = center, tile = tile };

            // Для каждой грани: нормаль n и оси a, b с a × b = −n (порядок обхода Unity).
            AddFace(data, Vector3.up, Vector3.right, Vector3.forward);
            AddFace(data, Vector3.down, Vector3.forward, Vector3.right);
            AddFace(data, Vector3.forward, Vector3.up, Vector3.right);
            AddFace(data, Vector3.back, Vector3.right, Vector3.up);
            AddFace(data, Vector3.right, Vector3.forward, Vector3.up);
            AddFace(data, Vector3.left, Vector3.up, Vector3.forward);

            Mesh mesh = new Mesh();
            mesh.name = $"LayoutBox_{layoutMeshCounter}";
            mesh.SetVertices(data.vertices);
            mesh.SetNormals(data.normals);
            mesh.SetUVs(0, data.uvs);
            mesh.SetTriangles(data.triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            AssetDatabase.CreateAsset(mesh, $"{LayoutMeshesFolder}/LayoutBox_{layoutMeshCounter++}.asset");
            return mesh;
        }

        private class MeshData
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
            public Vector3 half;
            public Vector3 center;
            public float tile;
        }

        private static void AddFace(MeshData d, Vector3 n, Vector3 a, Vector3 b)
        {
            Vector3 c = Vector3.Scale(n, d.half);
            Vector3 ha = Vector3.Scale(a, d.half), hb = Vector3.Scale(b, d.half);
            Vector3[] corners = { c - ha - hb, c - ha + hb, c + ha + hb, c + ha - hb };
            int start = d.vertices.Count;

            foreach (Vector3 v in corners)
            {
                d.vertices.Add(v);
                d.normals.Add(n);
                // UV — по мировым координатам грани, чтобы плитка на соседних участках совпадала.
                Vector3 w = v + d.center;
                Vector2 uv = Mathf.Abs(n.y) > 0.5f ? new Vector2(w.x, w.z)
                    : Mathf.Abs(n.z) > 0.5f ? new Vector2(w.x, w.y)
                    : new Vector2(w.z, w.y);
                d.uvs.Add(uv / d.tile);
            }
            d.triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        // ----------------------------------------------------------------
        // Материалы и текстуры
        // ----------------------------------------------------------------

        /// <summary>Материал URP/Lit; при повторной постройке свойства обновляются.</summary>
        private static Material LMat(string name, Color color, float smoothness = 0.25f, float emission = 0f,
            Texture2D texture = null)
        {
            EnsureFolder(LayoutMaterialsFolder);
            string path = $"{LayoutMaterialsFolder}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }

            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");

            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Прозрачное стекло витрины (URP/Lit, режим Transparent, без теней).</summary>
        private static Material GlassMaterial()
        {
            Material m = LMat("M_L_Glass", new Color(0.78f, 0.88f, 0.92f, 0.15f), 0.95f);
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

        private static Texture2D SalonTilesTexture() => LTexture("T_SalonTiles", 512, (x, y) =>
        {
            const int tile = 128, grout = 3;
            if (x % tile < grout || y % tile < grout) return new Color(0.76f, 0.73f, 0.68f);
            float v = (Hash(x / tile, y / tile, 1) - 0.5f) * 0.03f + (Hash(x, y, 2) - 0.5f) * 0.015f;
            return new Color(0.91f + v, 0.88f + v, 0.82f + v);
        });

        private static Texture2D CeilingTexture() => LTexture("T_CeilingGrid", 512, (x, y) =>
        {
            const int panel = 128, line = 5;
            if (x % panel < line || y % panel < line) return new Color(0.66f, 0.67f, 0.69f);
            float v = (Hash(x, y, 3) - 0.5f) * 0.03f;
            return new Color(0.94f + v, 0.94f + v, 0.94f + v);
        });

        private static Texture2D PavingTexture() => LTexture("T_Paving", 512, (x, y) =>
        {
            const int paver = 64, grout = 3;
            int row = y / paver;
            int px = x + (row % 2) * paver / 2; // перевязка рядов
            if (px % paver < grout || y % paver < grout) return new Color(0.36f, 0.36f, 0.37f);
            float v = (Hash(px / paver, row, 4) - 0.5f) * 0.08f + (Hash(x, y, 5) - 0.5f) * 0.03f;
            return new Color(0.6f + v, 0.6f + v, 0.61f + v);
        });

        private static Texture2D ConcreteTexture() => LTexture("T_Concrete", 256, (x, y) =>
        {
            float v = (ValueNoise(x, y, 16, 6) - 0.5f) * 0.06f + (Hash(x, y, 7) - 0.5f) * 0.03f;
            return new Color(0.55f + v, 0.55f + v, 0.56f + v);
        });

        private static Texture2D AsphaltTexture() => LTexture("T_Asphalt", 256, (x, y) =>
        {
            float v = (ValueNoise(x, y, 8, 8) - 0.5f) * 0.05f + (Hash(x, y, 9) - 0.5f) * 0.06f;
            return new Color(0.2f + v, 0.2f + v, 0.21f + v);
        });

        /// <summary>Текстура генерируется кодом один раз и сохраняется в PNG.</summary>
        private static Texture2D LTexture(string name, int size, System.Func<int, int, Color> pixel)
        {
            string path = $"{TexturesFolder}/{name}.png";
            if (!File.Exists(path))
            {
                EnsureFolder(TexturesFolder);
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, true);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        tex.SetPixel(x, y, pixel(x, y));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Псевдослучайное число 0…1 для пикселя (всегда одинаковое для одних координат).</summary>
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        /// <summary>Плавный шум: случайные значения в узлах сетки cell, между ними — интерполяция.</summary>
        private static float ValueNoise(int x, int y, int cell, int seed)
        {
            int cx = x / cell, cy = y / cell;
            float fx = (x % cell) / (float)cell, fy = (y % cell) / (float)cell;
            float a = Hash(cx, cy, seed), b = Hash(cx + 1, cy, seed);
            float c = Hash(cx, cy + 1, seed), d = Hash(cx + 1, cy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // ----------------------------------------------------------------
        // Прилавок и игровые объекты
        // ----------------------------------------------------------------

        /// <summary>Прилавок в стиле фото: чёрный корпус, белая столешница, розовая плашка и надпись.</summary>
        private static void BuildCounter()
        {
            Material black = LMat("M_L_Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);
            Material white = LMat("M_L_White", new Color(0.97f, 0.97f, 0.97f), 0.3f);
            Material magenta = LMat("M_L_Magenta", new Color(0.9f, 0.12f, 0.6f), 0.4f);

            Vector3 p = LayoutPlan.CounterPosition;
            GameObject root = new GameObject("Counter");
            root.transform.position = p;
            Undo.RegisterCreatedObjectUndo(root, "Прилавок");
            Transform c = root.transform;

            // Корпус с коллайдером; верх столешницы — на высоте 1 м (на ней стоят касса и терминал).
            Box(c, "Body", p + new Vector3(-1.5f, 0f, -0.4f), p + new Vector3(1.5f, 0.95f, 0.4f), black, true);
            Box(c, "Top", p + new Vector3(-1.53f, 0.95f, -0.43f), p + new Vector3(1.53f, 1f, 0.43f), white, true);

            // Лицевая сторона (к покупателям, −Z).
            float z = p.z - 0.4f;
            Box(c, "Panel", p + new Vector3(0.3f, 0.12f, -0.415f), p + new Vector3(1.4f, 0.55f, -0.4f), magenta, false);
            Box(c, "Line", p + new Vector3(-1.4f, 0.62f, -0.415f), p + new Vector3(0.2f, 0.63f, -0.4f), white, false);
            WorldLabel(c, "Title", new Vector3(p.x - 0.6f, 0.82f, z - 0.02f), Quaternion.identity, new Vector2(1.6f, 0.28f),
                "<b>ВСЕГДА НА СВЯЗИ</b>", Color.white, 1.4f);
            WorldLabel(c, "List", new Vector3(p.x - 0.6f, 0.38f, z - 0.02f), Quaternion.identity, new Vector2(1.6f, 0.4f),
                "SIM-карты · тарифы\nсмартфоны · аксессуары\nдомашний интернет", new Color(0.8f, 0.8f, 0.82f), 0.8f);
        }

        /// <summary>Касса, терминал, очередь, кассир, табличка, бак и точки переезжают на новые места.</summary>
        private static void MoveStoreObjects()
        {
            CashRegister register = Object.FindAnyObjectByType<CashRegister>(FindObjectsInactive.Include);
            if (register != null) Move(register.transform, LayoutPlan.RegisterPosition, FacingCashier);

            OrderTerminal terminal = Object.FindAnyObjectByType<OrderTerminal>(FindObjectsInactive.Include);
            if (terminal != null) Move(terminal.transform, LayoutPlan.TerminalPosition, FacingCashier);

            CheckoutQueue queue = Object.FindAnyObjectByType<CheckoutQueue>(FindObjectsInactive.Include);
            if (queue != null) Move(queue.transform, LayoutPlan.QueuePosition, Quaternion.identity);

            Cashier cashier = Object.FindAnyObjectByType<Cashier>(FindObjectsInactive.Include);
            if (cashier != null) Move(cashier.transform, LayoutPlan.CashierPosition, Quaternion.Euler(0f, 180f, 0f));

            StoreSign sign = Object.FindAnyObjectByType<StoreSign>(FindObjectsInactive.Include);
            if (sign != null) Move(sign.transform, LayoutPlan.SignPosition, Quaternion.identity);

            TrashBin trash = Object.FindAnyObjectByType<TrashBin>(FindObjectsInactive.Include);
            if (trash != null) Move(trash.transform, new Vector3(-7.35f, 0.35f, -9.35f), Quaternion.identity);

            GameObject spawn = FindIncludingInactive("CustomerSpawn");
            if (spawn != null) Move(spawn.transform, LayoutPlan.CustomerSpawn, Quaternion.identity);

            GameObject delivery = FindIncludingInactive("DeliveryPoint");
            if (delivery != null) Move(delivery.transform, LayoutPlan.DeliveryPoint, Quaternion.identity);

            Player.PlayerController player = Object.FindAnyObjectByType<Player.PlayerController>(FindObjectsInactive.Include);
            if (player != null) Move(player.transform, LayoutPlan.PlayerStart, Quaternion.identity);
        }

        private static void Move(Transform t, Vector3 position, Quaternion rotation)
        {
            Undo.RecordObject(t, "Новая карта");
            t.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Стартовый стеллаж — у западной стены зала, складской стеллаж — на складе.</summary>
        private static GameObject PlaceStarterFurniture(FurnitureCatalog catalog)
        {
            GameObject shelf = GameObject.Find("Shelf");
            if (shelf == null)
            {
                FurnitureData tall = catalog.Find("shelf_tall");
                if (tall != null && tall.prefab != null)
                {
                    shelf = (GameObject)PrefabUtility.InstantiatePrefab(tall.prefab);
                    shelf.name = "Shelf";
                    Undo.RegisterCreatedObjectUndo(shelf, "Стеллаж");
                    SerializedObject so = new SerializedObject(shelf.GetComponent<PlacedFurniture>());
                    so.FindProperty("instanceId").stringValue = "start_shelf";
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            // Лицевая сторона стеллажа (+Z) смотрит в зал, спиной — к стене.
            if (shelf != null) Move(shelf.transform, new Vector3(-7.6f, 0f, -3.8f), Quaternion.Euler(0f, 90f, 0f));

            FurnitureData rackData = catalog.Find("storage_rack");
            if (rackData == null || rackData.prefab == null) return null;

            GameObject rack = (GameObject)PrefabUtility.InstantiatePrefab(rackData.prefab);
            rack.name = "StorageRack";
            rack.transform.SetPositionAndRotation(new Vector3(-13.2f, 0f, -5.38f), Quaternion.Euler(0f, 180f, 0f));
            Undo.RegisterCreatedObjectUndo(rack, "Складской стеллаж");
            SerializedObject rso = new SerializedObject(rack.GetComponent<PlacedFurniture>());
            rso.FindProperty("instanceId").stringValue = "start_rack";
            rso.ApplyModifiedPropertiesWithoutUndo();
            return rack;
        }

        /// <summary>Стартовые коробки: часть — на складском стеллаже, остальные — на полу склада.</summary>
        private static void PlaceStarterBoxes(GameObject rack)
        {
            GameObject group = GameObject.Find("Boxes");
            List<ProductBox> boxes = new List<ProductBox>();
            if (group != null) boxes.AddRange(group.GetComponentsInChildren<ProductBox>(true));

            if (boxes.Count == 0)
            {
                if (group != null) Undo.DestroyObjectImmediate(group);
                SpawnBoxes(CreateBoxPrefab(), CreateProducts(), Vector3.zero);
                group = GameObject.Find("Boxes");
                if (group == null) return;
                boxes.AddRange(group.GetComponentsInChildren<ProductBox>(true));
            }

            // Места на стеллаже: те же, что задаёт BuildStorageRack (x ±0.23, три полки).
            List<Vector3> rackSlots = new List<Vector3>();
            if (rack != null)
                foreach (float y in new[] { 0.235f, 0.895f, 1.555f })
                    foreach (float x in new[] { -0.23f, 0.23f })
                        rackSlots.Add(rack.transform.TransformPoint(new Vector3(x, y, 0f)));

            int floorIndex = 0;
            for (int i = 0; i < boxes.Count; i++)
            {
                Vector3 position;
                if (i < rackSlots.Count) position = rackSlots[i];
                else
                {
                    int column = floorIndex % 6, row = floorIndex / 6;
                    position = new Vector3(-13.4f + column * 0.55f, 0.16f, -7.2f + row * 0.45f);
                    floorIndex++;
                }
                Move(boxes[i].transform, position, Quaternion.identity);
            }
        }
    }
}
