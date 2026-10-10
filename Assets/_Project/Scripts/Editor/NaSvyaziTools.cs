using System.Collections.Generic;
using System.IO;
using NaSvyazi.Audio;
using NaSvyazi.Building;
using NaSvyazi.Customers;
using NaSvyazi.Economy;
using NaSvyazi.Interaction;
using NaSvyazi.Products;
using NaSvyazi.Store;
using NaSvyazi.Tariffs;
using NaSvyazi.UI;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

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

            // Повторный запуск не создаёт дубли: существующие объекты пропускаются.
            GameObject shelf = GameObject.Find("Shelf");
            if (shelf == null) shelf = BuildShelf(new Vector3(-3f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f));
            else Debug.Log("[На связи] Стеллаж уже есть — пропускаю.");

            if (GameObject.Find("Boxes") == null) SpawnBoxes(boxPrefab, products, new Vector3(-1.5f, 0.5f, -3f));
            else Debug.Log("[На связи] Тестовые коробки уже есть — пропускаю.");

            if (Object.FindAnyObjectByType<TrashBin>() == null) CreateTrashBin(new Vector3(4f, 0f, -7f));
            else Debug.Log("[На связи] Мусорный бак уже есть — пропускаю.");

            Selection.activeGameObject = shelf;
            MarkSceneDirty();
            Debug.Log("[На связи] Сцена подготовлена: товары, стеллаж, коробки, мусорный бак. Сохраните сцену (Ctrl+S).");
        }

        [MenuItem("На связи/Подготовить сцену (итерация 3)", priority = 1)]
        private static void PrepareIteration3()
        {
            ProductCatalog catalog = CreateCatalog();
            GameObject boxPrefab = CreateBoxPrefab();

            // Системный объект со всеми «менеджерами».
            GameObject systems = GameObject.Find("GameSystems");
            if (systems == null)
            {
                systems = new GameObject("GameSystems");
                Undo.RegisterCreatedObjectUndo(systems, "GameSystems");
            }

            GetOrAdd<EconomyManager>(systems);
            DeliveryService delivery = GetOrAdd<DeliveryService>(systems);
            GetOrAdd<GameHUD>(systems);
            OrderUI orderUI = GetOrAdd<OrderUI>(systems);

            Transform deliveryPoint = CreateDeliveryPoint(new Vector3(6f, 0f, -8.6f));
            OrderTerminal terminal = CreateOrderTerminal(new Vector3(0.9f, 1.0f, 6.0f));

            // Связываем ссылки между компонентами.
            SetReference(delivery, "boxPrefab", boxPrefab.GetComponent<ProductBox>());
            SetReference(delivery, "deliveryPoint", deliveryPoint);
            SetReference(orderUI, "catalog", catalog);
            SetReference(orderUI, "delivery", delivery);
            SetReference(terminal, "orderUI", orderUI);

            Selection.activeGameObject = systems;
            MarkSceneDirty();
            Debug.Log("[На связи] Итерация 3: экономика, интерфейс, терминал заказа и зона доставки добавлены. Сохраните сцену (Ctrl+S).");
        }

        [MenuItem("На связи/Подготовить сцену (итерация 4)", priority = 2)]
        private static void PrepareIteration4()
        {
            ProductCatalog catalog = CreateCatalog();
            GameObject customerPrefab = CreateCustomerPrefab();

            GameObject systems = GameObject.Find("GameSystems");
            if (systems == null)
            {
                Debug.LogError("[На связи] Сначала выполните «Подготовить сцену (итерация 3)».");
                return;
            }

            CreateDoorAndStreet();
            CreateNavigation();

            Transform spawn = FindOrCreatePoint("CustomerSpawn", new Vector3(-6f, 0f, -15f), Quaternion.identity);
            CheckoutQueue queue = CreateCheckoutQueue(new Vector3(-0.8f, 0f, 5.0f));
            CashRegister register = CreateCashRegister(new Vector3(-0.8f, 1.0f, 6.0f));

            GetOrAdd<PriceManager>(systems);
            GetOrAdd<PriceUI>(systems);
            CustomerSpawner spawner = GetOrAdd<CustomerSpawner>(systems);

            SetReference(spawner, "customerPrefab", customerPrefab.GetComponent<Customer>());
            SetReference(spawner, "spawnPoint", spawn);
            SetReference(spawner, "queue", queue);
            SetReference(spawner, "catalog", catalog);
            SetReference(register, "queue", queue);

            Selection.activeGameObject = systems;
            MarkSceneDirty();
            Debug.Log("[На связи] Итерация 4: покупатели, цены, касса, навигация, дверь и улица добавлены. Сохраните сцену (Ctrl+S).");
        }

        [MenuItem("На связи/Подготовить сцену (итерация 5)", priority = 3)]
        private static void PrepareIteration5()
        {
            GameObject systems = GameObject.Find("GameSystems");
            CashRegister register = Object.FindAnyObjectByType<CashRegister>();
            CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>();
            if (systems == null || register == null || spawner == null)
            {
                Debug.LogError("[На связи] Сначала выполните «Подготовить сцену (итерация 4)».");
                return;
            }

            TariffCatalog tariffs = CreateTariffCatalog();

            GetOrAdd<ReputationManager>(systems);
            ChangeUI changeUI = GetOrAdd<ChangeUI>(systems);
            ConsultationUI consultationUI = GetOrAdd<ConsultationUI>(systems);

            SetReference(register, "changeUI", changeUI);
            SetReference(register, "consultationUI", consultationUI);
            SetReference(register, "tariffCatalog", tariffs);
            SetReference(spawner, "tariffCatalog", tariffs);

            Selection.activeGameObject = systems;
            MarkSceneDirty();
            Debug.Log("[На связи] Итерация 5: касса со сканированием и сдачей, консультации по тарифам, репутация. Сохраните сцену (Ctrl+S).");
        }

        [MenuItem("На связи/Подготовить сцену (итерация 6)", priority = 4)]
        private static void PrepareIteration6()
        {
            GameObject systems = GameObject.Find("GameSystems");
            if (systems == null || Object.FindAnyObjectByType<CashRegister>() == null)
            {
                Debug.LogError("[На связи] Сначала выполните итерации 3–5.");
                return;
            }

            ProductCatalog catalog = CreateCatalog(); // добавит наушники/колонки/смартфоны
            GameObject boxPrefab = CreateBoxPrefab();

            GetOrAdd<UnlockManager>(systems);
            DayReportUI report = GetOrAdd<DayReportUI>(systems);
            DayManager day = GetOrAdd<DayManager>(systems);
            Save.SaveSystem save = GetOrAdd<Save.SaveSystem>(systems);

            SetReference(day, "reportUI", report);
            SetReference(save, "catalog", catalog);
            SetReference(save, "boxPrefab", boxPrefab.GetComponent<ProductBox>());

            // Каталог в окне заказа и у покупателей — тот же, обновлённый.
            OrderUI order = Object.FindAnyObjectByType<OrderUI>();
            if (order != null) SetReference(order, "catalog", catalog);
            CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>();
            if (spawner != null) SetReference(spawner, "catalog", catalog);

            Selection.activeGameObject = systems;
            MarkSceneDirty();
            Debug.Log("[На связи] Итерация 6: рабочий день, отчёт, сохранение, открытие категорий. Сохраните сцену (Ctrl+S).");
        }

        // ----------------------------------------------------------------
        // Итерация 7: звук, пауза, главное меню, оформление, сборка
        // ----------------------------------------------------------------

        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("На связи/Подготовить сцену (итерация 7)", priority = 5)]
        private static void PrepareIteration7()
        {
            GameObject systems = GameObject.Find("GameSystems");
            if (systems == null || Object.FindAnyObjectByType<DayManager>() == null)
            {
                Debug.LogError("[На связи] Сначала выполните итерации 3–6.");
                return;
            }

            GetOrAdd<SoundManager>(systems);
            GetOrAdd<PauseMenuUI>(systems);
            DecorateSalon();
            MarkSceneDirty();

            // Сцену салона сохраняем автоматически: дальше создаётся сцена главного меню.
            Scene salon = SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(salon);
            string salonPath = salon.path;

            CreateMainMenuScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(salonPath, true)
            };
            PlayerSettings.productName = "NaSvyazi";

            EditorSceneManager.OpenScene(salonPath);
            Debug.Log("[На связи] Итерация 7: звук, пауза, главное меню, оформление салона. Сцены добавлены в сборку.");
        }

        [MenuItem("На связи/Открыть главное меню", priority = 12)]
        private static void OpenMainMenu()
        {
            if (!System.IO.File.Exists(MainMenuScenePath))
            {
                Debug.LogError("[На связи] Сцены главного меню ещё нет — выполните итерацию 7.");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MainMenuScenePath);
        }

        private static void CreateMainMenuScene()
        {
            if (System.IO.File.Exists(MainMenuScenePath)) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Camera cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            }

            GameObject menu = new GameObject("MainMenu");
            menu.AddComponent<MainMenuUI>();
            menu.AddComponent<SoundManager>();

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        /// <summary>
        /// Оформление салона: плитка на полу, светлые стены, потолок со светильниками,
        /// фирменная полоса, логотип, рекламные постеры, вывеска над входом, коврик.
        /// </summary>
        private static void DecorateSalon()
        {
            if (GameObject.Find("Decor") != null)
            {
                Debug.Log("[На связи] Оформление уже есть — пропускаю.");
                return;
            }

            GameObject decor = new GameObject("Decor");
            Undo.RegisterCreatedObjectUndo(decor, "Оформление");
            Transform d = decor.transform;

            Color accent = new Color(0.86f, 0.18f, 0.55f);
            Color dark = new Color(0.1f, 0.1f, 0.12f);

            // Пол — плитка.
            GameObject floor = GameObject.Find("Floor");
            if (floor != null)
            {
                Undo.RecordObject(floor.GetComponent<Renderer>(), "Оформление");
                floor.GetComponent<Renderer>().sharedMaterial = GetFloorTilesMaterial();
            }

            // Стены — светлые.
            Material wall = GetMaterial("M_Wall", new Color(0.93f, 0.92f, 0.9f));
            foreach (string name in new[] { "Wall_N", "Wall_E", "Wall_W", "Wall_S", "Wall_S2", "Wall_S_Lintel" })
            {
                GameObject w = GameObject.Find(name);
                if (w == null) continue;
                Undo.RecordObject(w.GetComponent<Renderer>(), "Оформление");
                w.GetComponent<Renderer>().sharedMaterial = wall;
            }

            // Прилавок — тёмный с акцентной полосой со стороны покупателей.
            GameObject counter = GameObject.Find("Counter");
            if (counter != null)
            {
                Undo.RecordObject(counter.GetComponent<Renderer>(), "Оформление");
                counter.GetComponent<Renderer>().sharedMaterial = GetMaterial("M_Counter", new Color(0.2f, 0.2f, 0.23f));
            }
            WorldCube(d, "CounterStripe", new Vector3(0f, 0.8f, 5.59f), new Vector3(3.02f, 0.12f, 0.02f),
                GetMaterial("M_Accent", accent));

            // Потолок не отбрасывает тень — внутрь салона по-прежнему попадает солнечный свет.
            // Коллайдер нужен, чтобы сквозь потолок нельзя было выпрыгнуть.
            GameObject ceiling = WorldCube(d, "Ceiling", new Vector3(0f, 3.1f, 0f), new Vector3(20f, 0.2f, 20f),
                GetMaterial("M_Ceiling", new Color(0.96f, 0.96f, 0.96f)), withCollider: true);
            ceiling.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            Material lamp = GetEmissiveMaterial("M_Lamp", new Color(1f, 0.98f, 0.92f), 2f);
            foreach (float x in new[] { -5f, 0f, 5f })
                foreach (float z in new[] { -4f, 4f })
                    WorldCube(d, "Lamp", new Vector3(x, 2.99f, z), new Vector3(1.2f, 0.02f, 0.4f), lamp)
                        .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Фирменная полоса и логотип на стене за прилавком.
            WorldCube(d, "WallStripe", new Vector3(0f, 2.55f, 9.885f), new Vector3(19.6f, 0.22f, 0.02f),
                GetMaterial("M_Accent", accent));
            // Вывеска над входом (снаружи) и коврик у двери.
            WorldCube(d, "Signboard", new Vector3(-6f, 2.65f, -10.13f), new Vector3(3.2f, 0.6f, 0.04f),
                GetMaterial("M_Signboard", dark));
            WorldCube(d, "EntranceMat", new Vector3(-6f, 0.005f, -9.3f), new Vector3(2f, 0.01f, 1.2f),
                GetMaterial("M_Mat", new Color(0.25f, 0.25f, 0.27f)));

            CreateDecorTexts(d);
        }

        private static readonly string[] DecorTextNames =
        {
            "Logo", "LogoSub", "Poster_Max_Text", "Poster_Connect_Text", "Poster_Phones_Text", "SignboardText"
        };

        /// <summary>Логотип, тексты постеров и вывески.</summary>
        private static void CreateDecorTexts(Transform d)
        {
            Color accent = new Color(0.86f, 0.18f, 0.55f);
            Color dark = new Color(0.1f, 0.1f, 0.12f);

            WorldLabel(d, "Logo", new Vector3(0f, 1.95f, 9.87f), Quaternion.identity, new Vector2(7f, 0.9f),
                "НА СВЯЗИ", dark, 9f);
            WorldLabel(d, "LogoSub", new Vector3(0f, 1.4f, 9.87f), Quaternion.identity, new Vector2(7f, 0.35f),
                "салон связи", new Color(0.35f, 0.35f, 0.38f), 3f);

            CreatePoster(d, "Poster_Max", new Vector3(-9.88f, 1.7f, -2f), -90f, new Vector2(1.6f, 1.1f), accent,
                "Тариф «Максимум»\nбезлимитный интернет\n850 руб./мес", Color.white);
            CreatePoster(d, "Poster_Connect", new Vector3(-9.88f, 1.7f, 4f), -90f, new Vector2(1.6f, 1.1f), dark,
                "Подключим\nза 5 минут", Color.white);
            CreatePoster(d, "Poster_Phones", new Vector3(9.88f, 1.7f, 0f), 90f, new Vector2(1.8f, 1.1f),
                new Color(0.15f, 0.35f, 0.6f), "Новые смартфоны\nуже в продаже", Color.white);

            WorldLabel(d, "SignboardText", new Vector3(-6f, 2.65f, -10.155f), Quaternion.identity,
                new Vector2(3f, 0.5f), "НА СВЯЗИ", accent, 6f);
        }

        [MenuItem("На связи/Исправить надписи и потолок", priority = 13)]
        private static void FixDecor()
        {
            GameObject decor = GameObject.Find("Decor");
            if (decor == null)
            {
                Debug.LogError("[На связи] Оформления ещё нет — выполните итерацию 7.");
                return;
            }

            // Старые надписи (со сбитыми координатами) удаляем и создаём заново.
            foreach (string name in DecorTextNames)
            {
                GameObject old = GameObject.Find(name);
                while (old != null)
                {
                    Undo.DestroyObjectImmediate(old);
                    old = GameObject.Find(name);
                }
            }
            CreateDecorTexts(decor.transform);

            GameObject ceiling = GameObject.Find("Ceiling");
            if (ceiling != null && ceiling.GetComponent<Collider>() == null)
                Undo.AddComponent<BoxCollider>(ceiling);

            MarkSceneDirty();
            Debug.Log("[На связи] Надписи пересозданы, у потолка появился коллайдер. Сохраните сцену (Ctrl+S).");
        }

        private static GameObject WorldCube(Transform parent, string name, Vector3 position, Vector3 size, Material m,
            bool withCollider = false)
        {
            GameObject go = CreateCube(name, parent, Vector3.zero, size, m, keepCollider: withCollider);
            go.transform.position = position;
            return go;
        }

        /// <summary>
        /// Надпись в мире. Положение и поворот задаются пустому объекту-держателю,
        /// а текст стоит внутри него в нуле: так координаты не сбрасываются
        /// (у текстового RectTransform под обычным Transform сбивались X и Y).
        /// </summary>
        private static TextMeshPro WorldLabel(Transform parent, string name, Vector3 position, Quaternion rotation,
            Vector2 size, string text, Color color, float maxFontSize)
        {
            GameObject holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(holder, name);

            TextMeshPro label = CreateLabel("Text", holder.transform, Vector3.zero, Quaternion.identity, size, color);
            label.fontSizeMax = maxFontSize;
            label.text = text;
            return label;
        }

        /// <summary>Постер на стене: подложка и текст перед ней (со стороны зрителя).</summary>
        private static void CreatePoster(Transform parent, string name, Vector3 position, float rotationY,
            Vector2 size, Color background, string text, Color textColor)
        {
            Quaternion rotation = Quaternion.Euler(0f, rotationY, 0f);

            if (GameObject.Find(name) == null)
            {
                GameObject back = WorldCube(parent, name, position, new Vector3(size.x, size.y, 0.02f),
                    GetMaterial($"M_{name}", background));
                back.transform.rotation = rotation;
            }

            Vector3 textPosition = position - rotation * Vector3.forward * 0.015f;
            WorldLabel(parent, name + "_Text", textPosition, rotation, size * 0.88f, text, textColor, 3f);
        }

        private static Material GetEmissiveMaterial(string name, Color color, float intensity)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Material m = GetMaterial(name, color);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Материал плитки: текстура генерируется кодом и сохраняется как PNG.</summary>
        private static Material GetFloorTilesMaterial()
        {
            const string texturePath = "Assets/_Project/Textures/T_FloorTiles.png";
            if (!System.IO.File.Exists(texturePath))
            {
                EnsureFolder("Assets/_Project/Textures");
                const int size = 512, tile = 64, grout = 2;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                Color a = new Color(0.86f, 0.86f, 0.84f), b = new Color(0.8f, 0.8f, 0.79f), g = new Color(0.6f, 0.6f, 0.6f);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        bool isGrout = x % tile < grout || y % tile < grout;
                        bool odd = ((x / tile) + (y / tile)) % 2 == 1;
                        tex.SetPixel(x, y, isGrout ? g : (odd ? a : b));
                    }
                System.IO.File.WriteAllBytes(texturePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texturePath);
            }

            Material m = GetMaterial("M_FloorTiles", Color.white);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            m.SetTextureScale("_BaseMap", new Vector2(4f, 4f));
            EditorUtility.SetDirty(m);
            return m;
        }

        [MenuItem("На связи/Удалить сохранение игры", priority = 30)]
        private static void DeleteSaveFile()
        {
            if (!Save.SaveSystem.HasSave)
            {
                Debug.Log("[На связи] Сохранения нет.");
                return;
            }

            if (EditorUtility.DisplayDialog("Удалить сохранение?",
                    $"Файл будет удалён:\n{Save.SaveSystem.SavePath}", "Удалить", "Отмена"))
            {
                Save.SaveSystem.DeleteSave();
                Debug.Log("[На связи] Сохранение удалено. Следующий запуск начнётся с первого дня.");
            }
        }

        private const string TariffsFolder = "Assets/_Project/Data/Tariffs";

        /// <summary>Вымышленная линейка тарифов и типовые запросы клиентов.</summary>
        private static TariffCatalog CreateTariffCatalog()
        {
            EnsureFolder(TariffsFolder);

            List<TariffData> list = new List<TariffData>
            {
                CreateTariff("tablet", "Для планшета", 300, 30, 0,
                    "Только интернет: для планшета, модема или второго устройства."),
                CreateTariff("basic", "Базовый", 350, 10, 300,
                    "Недорогой тариф на каждый день."),
                CreateTariff("talk", "Разговорный", 400, 5, 1000,
                    "Много минут для тех, кто любит поговорить."),
                CreateTariff("internet", "Интернет+", 550, 40, 300,
                    "Много интернета для соцсетей и видео."),
                CreateTariff("max", "Максимум", 850, -1, 800,
                    "Безлимитный интернет и много минут."),
            };

            string path = "Assets/_Project/Data/TariffCatalog.asset";
            TariffCatalog catalog = AssetDatabase.LoadAssetAtPath<TariffCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TariffCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            catalog.tariffs = list;
            catalog.needs = new List<TariffNeed>
            {
                Need("Мне нужен интернет для соцсетей и видео, звоню редко", 30, 100),
                Need("Почти не пользуюсь интернетом, но очень много разговариваю", 2, 800),
                Need("Хочу самый дешёвый тариф — только звонить и иногда мессенджеры", 1, 100),
                Need("Работаю в дороге, нужен безлимитный интернет и звонки", -1, 300),
                Need("Нужна SIM-карта для планшета, звонить с неё не буду", 20, 0),
                Need("Нужно побольше и интернета, и минут — работаю с телефона", 30, 600),
                Need("Ребёнку в школу: немного интернета и звонки родителям", 5, 200),
            };

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static TariffData CreateTariff(string id, string name, int fee, int gb, int minutes, string description)
        {
            string path = $"{TariffsFolder}/Tariff_{id}.asset";
            TariffData existing = AssetDatabase.LoadAssetAtPath<TariffData>(path);
            if (existing != null) return existing;

            TariffData t = ScriptableObject.CreateInstance<TariffData>();
            t.id = id;
            t.displayName = name;
            t.monthlyFee = fee;
            t.gigabytes = gb;
            t.minutes = minutes;
            t.description = description;
            AssetDatabase.CreateAsset(t, path);
            return t;
        }

        private static TariffNeed Need(string request, int minGb, int minMinutes) =>
            new TariffNeed { request = request, minGigabytes = minGb, minMinutes = minMinutes };

        /// <summary>
        /// Расставляет окружение и основные объекты по эталонным мировым координатам.
        /// Пол — верхняя грань на высоте 0, все остальные объекты стоят на нём.
        /// </summary>
        [MenuItem("На связи/Выровнять стены, пол и объекты", priority = 10)]
        private static void AlignLayout()
        {
            GameObject environment = GameObject.Find("Environment");
            if (environment != null)
            {
                Undo.RecordObject(environment.transform, "Выравнивание");
                environment.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                environment.transform.localScale = Vector3.one;
            }

            // Пол: куб 20×1×20 с верхней гранью на y = 0 (или плоскость 2×1×2 на y = 0).
            GameObject floor = GameObject.Find("Floor");
            if (floor != null)
            {
                MeshFilter mf = floor.GetComponent<MeshFilter>();
                bool isPlane = mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Plane";
                if (isPlane) Align("Floor", new Vector3(0f, 0f, 0f), new Vector3(2f, 1f, 2f));
                else Align("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
            }

            Align("Wall_N", new Vector3(0f, 1.5f, 10f), new Vector3(20f, 3f, 0.2f));
            if (FindIncludingInactive("Wall_E_Gate") != null)
            {
                // Восточная стена разделена под проём во второй зал.
                Align("Wall_E", new Vector3(10f, 1.5f, -3.75f), new Vector3(0.2f, 3f, 12.5f));
                Align("Wall_E_Gate", new Vector3(10f, 1.5f, 4.5f), new Vector3(0.2f, 3f, 4f));
                Align("Wall_E2", new Vector3(10f, 1.5f, 8.25f), new Vector3(0.2f, 3f, 3.5f));
            }
            else
            {
                Align("Wall_E", new Vector3(10f, 1.5f, 0f), new Vector3(0.2f, 3f, 20f));
            }
            Align("Wall_W", new Vector3(-10f, 1.5f, 0f), new Vector3(0.2f, 3f, 20f));

            // Южная стена с дверным проёмом x ∈ [−7; −5].
            bool hasDoor = GameObject.Find("Wall_S2") != null;
            if (hasDoor)
            {
                Align("Wall_S", new Vector3(-8.5f, 1.5f, -10f), new Vector3(3f, 3f, 0.2f));
                Align("Wall_S2", new Vector3(2.5f, 1.5f, -10f), new Vector3(15f, 3f, 0.2f));
                Align("Wall_S_Lintel", new Vector3(-6f, 2.65f, -10f), new Vector3(2f, 0.7f, 0.2f));
            }
            else
            {
                Align("Wall_S", new Vector3(0f, 1.5f, -10f), new Vector3(20f, 3f, 0.2f));
            }

            Align("Counter", new Vector3(0f, 0.5f, 6f), new Vector3(3f, 1f, 0.8f));

            Align("Street", new Vector3(0f, -0.5f, -14f), new Vector3(20f, 1f, 8f));
            Align("Curb_W", new Vector3(-10f, 0.5f, -14f), new Vector3(0.2f, 1f, 8f));
            Align("Curb_E", new Vector3(10f, 0.5f, -14f), new Vector3(0.2f, 1f, 8f));
            Align("Curb_S", new Vector3(0f, 0.5f, -18f), new Vector3(20f, 1f, 0.2f));

            // Объекты салона (масштаб не меняется).
            AlignPosition("Player", new Vector3(0f, 0f, -5f), Quaternion.identity);
            AlignPosition("Shelf", new Vector3(-3f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f));
            AlignPosition("TrashBin", new Vector3(4f, 0.35f, -7f), Quaternion.identity);
            AlignPosition("DeliveryPoint", new Vector3(6f, 0f, -8.6f), Quaternion.identity);
            AlignPosition("OrderTerminal", new Vector3(0.9f, 1f, 6f), FacingCashier);
            AlignPosition("CashRegister", new Vector3(-0.8f, 1f, 6f), FacingCashier);
            AlignPosition("CheckoutQueue", new Vector3(-0.8f, 0f, 5f), Quaternion.identity);
            AlignPosition("CustomerSpawn", new Vector3(-6f, 0f, -15f), Quaternion.identity);

            StoreSign sign = Object.FindAnyObjectByType<StoreSign>();
            if (sign != null)
            {
                Undo.RecordObject(sign.transform, "Выравнивание");
                sign.transform.SetPositionAndRotation(new Vector3(3f, 1.6f, 9.875f), Quaternion.identity);
            }

            MarkSceneDirty();
            Debug.Log("[На связи] Стены, пол и объекты выровнены. Сохраните сцену (Ctrl+S).");
        }

        private static void Align(string name, Vector3 worldPos, Vector3 worldScale)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) return;

            Transform t = go.transform;
            Undo.RecordObject(t, "Выравнивание");
            t.rotation = Quaternion.identity;
            t.position = worldPos;

            // Масштаб задаётся в мировых единицах с учётом масштаба родителя.
            Vector3 parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(worldScale.x / parentScale.x, worldScale.y / parentScale.y,
                worldScale.z / parentScale.z);
        }

        private static void AlignPosition(string name, Vector3 worldPos, Quaternion rotation)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) return;

            Undo.RecordObject(go.transform, "Выравнивание");
            go.transform.SetPositionAndRotation(worldPos, rotation);
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
                CreateProduct("speaker", "Портативная колонка", ProductCategory.Audio, 1500, 3490, 6,
                    new Vector3(0.12f, 0.16f, 0.07f), new Color(0.2f, 0.55f, 0.45f)),
                CreateProduct("phone_basic", "Смартфон начального уровня", ProductCategory.Phones, 6500, 9990, 4,
                    new Vector3(0.09f, 0.17f, 0.04f), new Color(0.3f, 0.3f, 0.34f)),
                CreateProduct("phone_mid", "Смартфон среднего класса", ProductCategory.Phones, 14000, 19990, 3,
                    new Vector3(0.09f, 0.17f, 0.04f), new Color(0.55f, 0.2f, 0.6f)),
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

        /// <summary>Каталог поставщика со всеми товарами из папки товаров.</summary>
        private static ProductCatalog CreateCatalog()
        {
            CreateProducts();

            string path = "Assets/_Project/Data/ProductCatalog.asset";
            ProductCatalog catalog = AssetDatabase.LoadAssetAtPath<ProductCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ProductCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            List<ProductData> all = new List<ProductData>();
            foreach (string guid in AssetDatabase.FindAssets("t:ProductData", new[] { ProductsFolder }))
            {
                ProductData p = AssetDatabase.LoadAssetAtPath<ProductData>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null) all.Add(p);
            }

            all.Sort((a, b) => a.category != b.category
                ? a.category.CompareTo(b.category)
                : string.CompareOrdinal(a.displayName, b.displayName));

            catalog.products = all;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
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

        private static GameObject BuildShelf(Vector3 position, Quaternion rotation) =>
            BuildShelf(position, rotation, 1.9f, new[] { 0.08f, 0.53f, 0.98f, 1.43f }, "M_Shelf",
                new Color(0.88f, 0.88f, 0.9f));

        /// <summary>Стеллаж заданной высоты с полками на указанных высотах (по 2 секции на полку).</summary>
        private static GameObject BuildShelf(Vector3 position, Quaternion rotation, float height, float[] boardHeights,
            string materialName, Color color)
        {
            const float width = 1.8f, depth = 0.45f, thickness = 0.03f;

            Material frame = GetMaterial(materialName, color);

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
        // Зона доставки и терминал заказа
        // ----------------------------------------------------------------

        private static Transform CreateDeliveryPoint(Vector3 position)
        {
            GameObject existing = GameObject.Find("DeliveryPoint");
            if (existing != null) return existing.transform;

            GameObject point = new GameObject("DeliveryPoint");
            point.transform.position = position;
            Undo.RegisterCreatedObjectUndo(point, "Зона доставки");

            // Жёлтая разметка на полу (без коллайдера).
            CreateCube("Marker", point.transform, new Vector3(0f, 0.005f, 0.35f), new Vector3(1.6f, 0.01f, 1.2f),
                GetMaterial("M_Delivery", new Color(0.95f, 0.78f, 0.15f)), keepCollider: false);

            TextMeshPro label = CreateLabel("Label", point.transform, new Vector3(0f, 0.012f, 1.1f),
                Quaternion.Euler(90f, 180f, 0f), new Vector2(1.6f, 0.25f), new Color(0.1f, 0.1f, 0.1f));
            label.text = "ЗОНА ДОСТАВКИ";
            label.fontSizeMax = 2f;

            return point.transform;
        }

        private static OrderTerminal CreateOrderTerminal(Vector3 position)
        {
            OrderTerminal existing = Object.FindAnyObjectByType<OrderTerminal>();
            if (existing != null) return existing;

            // Экран смотрит на кассира (за прилавком, со стороны +Z).
            GameObject root = new GameObject("OrderTerminal");
            root.transform.SetPositionAndRotation(position, FacingCashier);
            Undo.RegisterCreatedObjectUndo(root, "Терминал заказа");

            Material dark = GetMaterial("M_Device", new Color(0.12f, 0.12f, 0.14f));
            Material screen = GetMaterial("M_Screen", new Color(0.15f, 0.35f, 0.6f));

            CreateCube("Stand", root.transform, new Vector3(0f, 0.08f, 0.03f), new Vector3(0.08f, 0.16f, 0.06f),
                dark, keepCollider: false);
            CreateCube("Monitor", root.transform, new Vector3(0f, 0.34f, 0f), new Vector3(0.56f, 0.36f, 0.05f),
                dark, keepCollider: true);
            CreateCube("Screen", root.transform, new Vector3(0f, 0.34f, -0.026f), new Vector3(0.52f, 0.32f, 0.004f),
                screen, keepCollider: false);

            TextMeshPro label = CreateLabel("Label", root.transform, new Vector3(0f, 0.34f, -0.03f),
                Quaternion.identity, new Vector2(0.48f, 0.2f), Color.white);
            label.text = "ЗАКАЗ\nТОВАРА";

            return root.AddComponent<OrderTerminal>();
        }

        // ----------------------------------------------------------------
        // Итерация 4: покупатели, навигация, дверь, касса
        // ----------------------------------------------------------------

        private const string CustomerPrefabPath = PrefabsFolder + "/Customer.prefab";

        private static GameObject CreateCustomerPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefabPath);
            if (existing != null) return existing;

            EnsureFolder(PrefabsFolder);

            GameObject root = new GameObject("Customer");
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.85f, 0f);
            capsule.radius = 0.28f;
            capsule.height = 1.7f;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.7f;
            agent.speed = 1.8f;
            agent.angularSpeed = 540f;
            agent.acceleration = 10f;
            agent.stoppingDistance = 0.15f;

            Customer customer = root.AddComponent<Customer>();

            // Тело — капсула, цвет задаётся случайно при появлении.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            body.transform.localScale = new Vector3(0.56f, 0.85f, 0.56f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = GetMaterial("M_Customer", Color.white);

            // «Козырёк» показывает, куда смотрит покупатель.
            CreateCube("Visor", root.transform, new Vector3(0f, 1.48f, 0.24f), new Vector3(0.32f, 0.1f, 0.12f),
                GetMaterial("M_Device", new Color(0.12f, 0.12f, 0.14f)), keepCollider: false);

            TextMeshPro bubble = CreateLabel("Bubble", root.transform, new Vector3(0f, 2.0f, 0f),
                Quaternion.identity, new Vector2(1.8f, 0.3f), new Color(1f, 0.9f, 0.3f));
            bubble.fontSizeMax = 2f;

            SerializedObject so = new SerializedObject(customer);
            so.FindProperty("bodyRenderer").objectReferenceValue = body.GetComponent<Renderer>();
            so.FindProperty("bubble").objectReferenceValue = bubble;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CustomerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Дверной проём в южной стене и улица перед салоном.</summary>
        private static void CreateDoorAndStreet()
        {
            if (GameObject.Find("Street") != null) return;

            GameObject environment = GameObject.Find("Environment");
            Transform parent = environment != null ? environment.transform : null;

            GameObject wall = GameObject.Find("Wall_S");
            if (wall != null)
            {
                Undo.RecordObject(wall.transform, "Дверной проём");
                Vector3 p = wall.transform.position;
                float height = wall.transform.lossyScale.y;
                float thickness = wall.transform.lossyScale.z;

                // Левый кусок стены: x от −10 до −7. Проём: x от −7 до −5.
                wall.transform.position = new Vector3(-8.5f, p.y, p.z);
                wall.transform.localScale = new Vector3(3f, height, thickness);

                GameObject right = Object.Instantiate(wall, wall.transform.parent);
                right.name = "Wall_S2";
                right.transform.position = new Vector3(2.5f, p.y, p.z);
                right.transform.localScale = new Vector3(15f, height, thickness);
                Undo.RegisterCreatedObjectUndo(right, "Дверной проём");

                GameObject lintel = Object.Instantiate(wall, wall.transform.parent);
                lintel.name = "Wall_S_Lintel";
                lintel.transform.position = new Vector3(-6f, p.y + height * 0.5f - 0.35f, p.z);
                lintel.transform.localScale = new Vector3(2f, 0.7f, thickness);
                Undo.RegisterCreatedObjectUndo(lintel, "Дверной проём");
            }
            else
            {
                Debug.LogWarning("[На связи] Не найдена стена Wall_S — дверной проём сделайте вручную.");
            }

            Material street = GetMaterial("M_Street", new Color(0.32f, 0.33f, 0.35f));
            Material curb = GetMaterial("M_Curb", new Color(0.55f, 0.55f, 0.57f));

            GameObject streetGo = CreateCube("Street", parent, Vector3.zero, new Vector3(20f, 1f, 8f), street, true);
            streetGo.transform.position = new Vector3(0f, -0.5f, -14f);
            Undo.RegisterCreatedObjectUndo(streetGo, "Улица");

            CreateWorldCube("Curb_W", parent, new Vector3(-10f, 0.5f, -14f), new Vector3(0.2f, 1f, 8f), curb);
            CreateWorldCube("Curb_E", parent, new Vector3(10f, 0.5f, -14f), new Vector3(0.2f, 1f, 8f), curb);
            CreateWorldCube("Curb_S", parent, new Vector3(0f, 0.5f, -18f), new Vector3(20f, 1f, 0.2f), curb);
        }

        private static void CreateWorldCube(string name, Transform parent, Vector3 worldPos, Vector3 size, Material m)
        {
            GameObject go = CreateCube(name, parent, Vector3.zero, size, m, true);
            go.transform.position = worldPos;
            Undo.RegisterCreatedObjectUndo(go, name);
        }

        /// <summary>Навигационная сетка: строится при запуске игры (NavMeshBaker).</summary>
        private static void CreateNavigation()
        {
            if (Object.FindAnyObjectByType<NavMeshBaker>() != null) return;

            GameObject nav = new GameObject("Navigation");
            Undo.RegisterCreatedObjectUndo(nav, "Навигация");

            NavMeshSurface surface = nav.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            // Игрок (слой Player) и слой Ignore Raycast не считаются препятствиями.
            surface.layerMask = ~((1 << 2) | (1 << 3));

            nav.AddComponent<NavMeshBaker>();
        }

        private static Transform FindOrCreatePoint(string name, Vector3 position, Quaternion rotation)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) return existing.transform;

            GameObject go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go.transform;
        }

        private static CheckoutQueue CreateCheckoutQueue(Vector3 position)
        {
            CheckoutQueue existing = Object.FindAnyObjectByType<CheckoutQueue>();
            if (existing != null) return existing;

            // Ось Z смотрит на прилавок (+Z), очередь тянется назад.
            Transform point = FindOrCreatePoint("CheckoutQueue", position, Quaternion.identity);
            return point.gameObject.AddComponent<CheckoutQueue>();
        }

        private static CashRegister CreateCashRegister(Vector3 position)
        {
            CashRegister existing = Object.FindAnyObjectByType<CashRegister>();
            if (existing != null) return existing;

            // Дисплей смотрит на кассира (за прилавком, со стороны +Z).
            GameObject root = new GameObject("CashRegister");
            root.transform.SetPositionAndRotation(position, FacingCashier);
            Undo.RegisterCreatedObjectUndo(root, "Касса");

            Material dark = GetMaterial("M_Device", new Color(0.12f, 0.12f, 0.14f));
            Material screen = GetMaterial("M_Screen", new Color(0.15f, 0.35f, 0.6f));

            // Корпус с клавиатурой, стойка и наклонённый дисплей — без зазоров между деталями.
            CreateCube("Base", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.42f, 0.1f, 0.36f),
                dark, keepCollider: true);
            CreateCube("Keys", root.transform, new Vector3(0f, 0.105f, -0.08f), new Vector3(0.3f, 0.01f, 0.14f),
                GetMaterial("M_Keys", new Color(0.35f, 0.35f, 0.38f)), keepCollider: false);
            CreateCube("Neck", root.transform, new Vector3(0f, 0.15f, 0.1f), new Vector3(0.06f, 0.1f, 0.05f),
                dark, keepCollider: false);

            GameObject screenPivot = new GameObject("ScreenPivot");
            screenPivot.transform.SetParent(root.transform, false);
            screenPivot.transform.localPosition = new Vector3(0f, 0.29f, 0.1f);
            screenPivot.transform.localRotation = Quaternion.Euler(12f, 0f, 0f); // лёгкий наклон назад

            CreateCube("Frame", screenPivot.transform, Vector3.zero, new Vector3(0.32f, 0.2f, 0.03f),
                dark, keepCollider: true);
            CreateCube("Display", screenPivot.transform, new Vector3(0f, 0f, -0.016f),
                new Vector3(0.29f, 0.17f, 0.004f), screen, keepCollider: false);

            TextMeshPro label = CreateLabel("Label", screenPivot.transform, new Vector3(0f, 0f, -0.02f),
                Quaternion.identity, new Vector2(0.27f, 0.15f), Color.white);
            label.text = "КАССА";

            return root.AddComponent<CashRegister>();
        }

        // ----------------------------------------------------------------
        // Итерация 8: обучение, уровни, мебель, расширение, кассир
        // ----------------------------------------------------------------

        private const string FurnitureFolder = PrefabsFolder + "/Furniture";
        private const string FurnitureDataFolder = "Assets/_Project/Data/Furniture";

        [MenuItem("На связи/Подготовить сцену (итерация 8)", priority = 6)]
        private static void PrepareIteration8()
        {
            GameObject systems = GameObject.Find("GameSystems");
            CashRegister register = Object.FindAnyObjectByType<CashRegister>();
            if (systems == null || register == null || Object.FindAnyObjectByType<DayManager>() == null)
            {
                Debug.LogError("[На связи] Сначала выполните итерации 3–7.");
                return;
            }

            FurnitureCatalog furniture = CreateFurnitureCatalog();
            ReplaceSceneShelf(furniture);
            ExpansionParts expansion = CreateExpansionHall();
            Cashier cashier = CreateCashier(register);

            GetOrAdd<LevelManager>(systems);
            GetOrAdd<TutorialManager>(systems);
            GetOrAdd<FurniturePlacer>(systems);
            ExpansionManager expansionManager = GetOrAdd<ExpansionManager>(systems);
            StaffManager staff = GetOrAdd<StaffManager>(systems);

            SetReference(expansionManager, "hallRoot", expansion.hall);
            SetReference(expansionManager, "gateWall", expansion.gate);
            SetReference(staff, "cashier", cashier);

            OrderUI order = Object.FindAnyObjectByType<OrderUI>();
            if (order != null) SetReference(order, "furnitureCatalog", furniture);
            Save.SaveSystem save = Object.FindAnyObjectByType<Save.SaveSystem>();
            if (save != null) SetReference(save, "furnitureCatalog", furniture);

            Selection.activeGameObject = systems;
            MarkSceneDirty();
            Debug.Log("[На связи] Итерация 8: обучение, уровни, мебель, второй зал, кассир. Сохраните сцену (Ctrl+S).");
        }

        private static FurnitureCatalog CreateFurnitureCatalog()
        {
            EnsureFolder(FurnitureFolder);
            EnsureFolder(FurnitureDataFolder);

            GameObject tall = CreateShelfPrefab("Shelf_Tall", "shelf_tall", 1.9f,
                new[] { 0.08f, 0.53f, 0.98f, 1.43f }, "M_Shelf", new Color(0.88f, 0.88f, 0.9f));
            GameObject low = CreateShelfPrefab("Shelf_Low", "shelf_low", 1.0f,
                new[] { 0.08f, 0.53f }, "M_ShelfLow", new Color(0.32f, 0.33f, 0.37f));

            List<FurnitureData> items = new List<FurnitureData>
            {
                CreateFurnitureData("shelf_tall", "Стеллаж высокий",
                    "4 полки, 8 секций под товар. Основной стеллаж салона.", 2500, 1, tall),
                CreateFurnitureData("shelf_low", "Стеллаж низкий",
                    "2 полки, 4 секции. Удобен у прилавка и в центре зала: не закрывает обзор.", 1500, 1, low),
            };

            string path = "Assets/_Project/Data/FurnitureCatalog.asset";
            FurnitureCatalog catalog = AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<FurnitureCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }
            catalog.items = items;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static GameObject CreateShelfPrefab(string name, string id, float height, float[] boards,
            string material, Color color)
        {
            string path = $"{FurnitureFolder}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject root = BuildShelf(Vector3.zero, Quaternion.identity, height, boards, material, color);
            root.name = name;

            PlacedFurniture placed = root.AddComponent<PlacedFurniture>();
            SerializedObject so = new SerializedObject(placed);
            so.FindProperty("furnitureId").stringValue = id;
            so.FindProperty("instanceId").stringValue = "";
            so.FindProperty("footprint").vector3Value = new Vector3(1.8f, height, 0.45f);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static FurnitureData CreateFurnitureData(string id, string name, string description, int price,
            int minLevel, GameObject prefab)
        {
            string path = $"{FurnitureDataFolder}/Furniture_{id}.asset";
            FurnitureData data = AssetDatabase.LoadAssetAtPath<FurnitureData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<FurnitureData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.id = id;
            data.displayName = name;
            data.description = description;
            data.price = price;
            data.minLevel = minLevel;
            data.prefab = prefab;
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>Стеллаж, собранный в сцене вручную, заменяется экземпляром префаба (для сохранения и перестановки).</summary>
        private static void ReplaceSceneShelf(FurnitureCatalog catalog)
        {
            GameObject old = GameObject.Find("Shelf");
            if (old == null || old.GetComponent<PlacedFurniture>() != null) return;

            FurnitureData tall = catalog.Find("shelf_tall");
            if (tall == null || tall.prefab == null) return;

            Vector3 position = old.transform.position;
            Quaternion rotation = old.transform.rotation;
            Undo.DestroyObjectImmediate(old);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(tall.prefab);
            instance.name = "Shelf"; // имя сохраняется — старые сохранения найдут свои полки
            instance.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(instance, "Стеллаж");

            SerializedObject so = new SerializedObject(instance.GetComponent<PlacedFurniture>());
            so.FindProperty("instanceId").stringValue = "start_shelf";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private struct ExpansionParts
        {
            public GameObject hall;
            public GameObject gate;
        }

        /// <summary>
        /// Второй зал за восточной стеной (x от 10 до 18) и съёмный участок стены — проём.
        /// Восточная стена делится на три части: z от −10 до 2.5, проём 2.5…6.5, z от 6.5 до 10.
        /// </summary>
        private static ExpansionParts CreateExpansionHall()
        {
            ExpansionParts parts = new ExpansionParts();

            GameObject existingHall = FindIncludingInactive("Expansion_Hall");
            GameObject existingGate = GameObject.Find("Wall_E_Gate");
            if (existingHall != null && existingGate != null)
            {
                parts.hall = existingHall;
                parts.gate = existingGate;
                return parts;
            }

            GameObject environment = GameObject.Find("Environment");
            Transform envT = environment != null ? environment.transform : null;

            // Делим восточную стену.
            GameObject wall = GameObject.Find("Wall_E");
            if (wall != null)
            {
                Undo.RecordObject(wall.transform, "Расширение");
                float height = wall.transform.lossyScale.y;
                float thickness = wall.transform.lossyScale.x;
                wall.transform.position = new Vector3(10f, height * 0.5f, -3.75f);
                wall.transform.localScale = new Vector3(thickness, height, 12.5f);

                parts.gate = Object.Instantiate(wall, wall.transform.parent);
                parts.gate.name = "Wall_E_Gate";
                parts.gate.transform.position = new Vector3(10f, height * 0.5f, 4.5f);
                parts.gate.transform.localScale = new Vector3(thickness, height, 4f);
                Undo.RegisterCreatedObjectUndo(parts.gate, "Расширение");

                GameObject north = Object.Instantiate(wall, wall.transform.parent);
                north.name = "Wall_E2";
                north.transform.position = new Vector3(10f, height * 0.5f, 8.25f);
                north.transform.localScale = new Vector3(thickness, height, 3.5f);
                Undo.RegisterCreatedObjectUndo(north, "Расширение");
            }

            // Сам зал (выключен до покупки).
            GameObject hall = new GameObject("Expansion_Hall");
            if (envT != null) hall.transform.SetParent(envT, true);
            hall.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(hall, "Расширение");
            Transform h = hall.transform;

            Material floor = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_FloorTiles.mat")
                             ?? GetMaterial("M_FloorTiles", Color.white);
            Material wallMat = GetMaterial("M_Wall", new Color(0.93f, 0.92f, 0.9f));

            WorldCube(h, "Floor_2", new Vector3(14f, -0.5f, 0f), new Vector3(8f, 1f, 20f), floor, withCollider: true);
            WorldCube(h, "Wall_2N", new Vector3(14f, 1.5f, 10f), new Vector3(8.2f, 3f, 0.2f), wallMat, withCollider: true);
            WorldCube(h, "Wall_2S", new Vector3(14f, 1.5f, -10f), new Vector3(8.2f, 3f, 0.2f), wallMat, withCollider: true);
            WorldCube(h, "Wall_2E", new Vector3(18f, 1.5f, 0f), new Vector3(0.2f, 3f, 20f), wallMat, withCollider: true);

            GameObject ceiling = WorldCube(h, "Ceiling_2", new Vector3(14f, 3.1f, 0f), new Vector3(8f, 0.2f, 20f),
                GetMaterial("M_Ceiling", new Color(0.96f, 0.96f, 0.96f)), withCollider: true);
            ceiling.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            Material lamp = GetEmissiveMaterial("M_Lamp", new Color(1f, 0.98f, 0.92f), 2f);
            foreach (float z in new[] { -5f, 0f, 5f })
                WorldCube(h, "Lamp", new Vector3(14f, 2.99f, z), new Vector3(1.2f, 0.02f, 0.4f), lamp)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            hall.SetActive(false);
            parts.hall = hall;
            return parts;
        }

        /// <summary>Кассир за прилавком. Внешность — первая модель покупателя, если персонажи подключены.</summary>
        private static Cashier CreateCashier(CashRegister register)
        {
            Cashier existing = Object.FindAnyObjectByType<Cashier>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            GameObject root = new GameObject("Cashier");
            root.transform.SetPositionAndRotation(new Vector3(-0.8f, 0f, 6.75f), Quaternion.Euler(0f, 180f, 0f));
            Undo.RegisterCreatedObjectUndo(root, "Кассир");

            GameObject model = null;
            string[] variants = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder + "/Customers" });
            if (variants.Length > 0)
            {
                GameObject variant = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(variants[0]));
                Transform source = variant != null ? variant.transform.Find("Model") : null;
                if (source != null)
                {
                    model = Object.Instantiate(source.gameObject, root.transform);
                    model.name = "Model";
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                }
            }

            if (model == null)
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                body.transform.localScale = new Vector3(0.56f, 0.85f, 0.56f);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = GetMaterial("M_Staff", new Color(0.86f, 0.18f, 0.55f));
            }

            Cashier cashier = root.AddComponent<Cashier>();
            SetReference(cashier, "register", register);
            root.SetActive(false);
            return cashier;
        }

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t.gameObject;
            return null;
        }

        /// <summary>Поворот устройств на прилавке: лицевой стороной к кассиру.</summary>
        private static readonly Quaternion FacingCashier = Quaternion.Euler(0f, 180f, 0f);

        [MenuItem("На связи/Пересоздать кассу и терминал", priority = 11)]
        private static void RebuildDevices()
        {
            // Запоминаем связи старых объектов.
            CheckoutQueue queue = Object.FindAnyObjectByType<CheckoutQueue>();
            ChangeUI changeUI = Object.FindAnyObjectByType<ChangeUI>();
            ConsultationUI consultationUI = Object.FindAnyObjectByType<ConsultationUI>();
            OrderUI orderUI = Object.FindAnyObjectByType<OrderUI>();
            TariffCatalog tariffs = AssetDatabase.LoadAssetAtPath<TariffCatalog>("Assets/_Project/Data/TariffCatalog.asset");

            CashRegister oldRegister = Object.FindAnyObjectByType<CashRegister>();
            if (oldRegister != null) Undo.DestroyObjectImmediate(oldRegister.gameObject);
            OrderTerminal oldTerminal = Object.FindAnyObjectByType<OrderTerminal>();
            if (oldTerminal != null) Undo.DestroyObjectImmediate(oldTerminal.gameObject);

            CashRegister register = CreateCashRegister(new Vector3(-0.8f, 1.0f, 6.0f));
            OrderTerminal terminal = CreateOrderTerminal(new Vector3(0.9f, 1.0f, 6.0f));

            if (queue != null) SetReference(register, "queue", queue);
            if (changeUI != null) SetReference(register, "changeUI", changeUI);
            if (consultationUI != null) SetReference(register, "consultationUI", consultationUI);
            if (tariffs != null) SetReference(register, "tariffCatalog", tariffs);
            if (orderUI != null) SetReference(terminal, "orderUI", orderUI);

            Selection.activeGameObject = register.gameObject;
            MarkSceneDirty();
            Debug.Log("[На связи] Касса и терминал пересозданы и повёрнуты к кассиру. Сохраните сцену (Ctrl+S).");
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static void SetReference(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
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
