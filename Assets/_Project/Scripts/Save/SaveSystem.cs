using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NaSvyazi.Building;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Events;
using NaSvyazi.Products;
using NaSvyazi.Progression;
using NaSvyazi.Store;
using NaSvyazi.UI;
using UnityEngine;

namespace NaSvyazi.Save
{
    /// <summary>
    /// Сохранение и загрузка игры в JSON-файл (Application.persistentDataPath/save.json).
    /// Сохраняется: день, баланс, репутация, цены, открытые категории,
    /// товар на полках и коробки в салоне. Автосохранение — в начале каждого дня.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        [SerializeField] private ProductCatalog catalog;
        [SerializeField] private ProductBox boxPrefab;
        [SerializeField] private FurnitureCatalog furnitureCatalog;

        [Tooltip("Загружать сохранение при запуске сцены")]
        [SerializeField] private bool loadOnStart = true;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
        public static bool HasSave => File.Exists(SavePath);

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (loadOnStart && HasSave) Load();
        }

        // ----------------------------------------------------------------
        // Сохранение
        // ----------------------------------------------------------------

        public void Save()
        {
            SaveData data = new SaveData();

            if (DayManager.Instance != null) data.day = DayManager.Instance.Day;
            if (EconomyManager.Instance != null) data.balance = EconomyManager.Instance.Balance;
            if (ReputationManager.Instance != null) data.reputation = ReputationManager.Instance.Value;
            if (LevelManager.Instance != null)
            {
                data.level = LevelManager.Instance.Level;
                data.xp = LevelManager.Instance.Xp;
            }
            if (TutorialManager.Instance != null) data.tutorialDone = TutorialManager.Instance.Completed;
            if (ExpansionManager.Instance != null)
            {
                data.salonStage = ExpansionManager.Instance.SalonStage;
                data.storageStage = ExpansionManager.Instance.StorageStage;
            }
            if (StaffManager.Instance != null) data.cashierHired = StaffManager.Instance.CashierHired;

            if (SkillManager.Instance != null)
                foreach (KeyValuePair<string, int> pair in SkillManager.Instance.GetRanks())
                    data.skills.Add(new SkillEntry { id = pair.Key, rank = pair.Value });
            data.eventId = StoreEventManager.CurrentId ?? "";

            if (UpgradeManager.Instance != null)
            {
                data.shelfTier = UpgradeManager.Instance.ShelfTier;
                data.hasCart = UpgradeManager.Instance.HasCart;
                if (UpgradeManager.Instance.Cart != null)
                {
                    data.cartPosition = UpgradeManager.Instance.Cart.transform.position;
                    data.cartRotation = UpgradeManager.Instance.Cart.transform.rotation;
                }
            }
            if (Story.LoreManager.Instance != null) data.letters = Story.LoreManager.Instance.GetShown();
            if (GoalManager.Instance != null)
            {
                data.goalReached = GoalManager.Instance.GoalReached;
                data.debtDays = GoalManager.Instance.DebtDays;
            }

            foreach (PlacedFurniture f in FindObjectsByType<PlacedFurniture>(FindObjectsSortMode.None))
            {
                data.furniture.Add(new FurnitureEntry
                {
                    furnitureId = f.FurnitureId,
                    instanceId = f.InstanceId,
                    position = f.transform.position,
                    rotation = f.transform.rotation
                });
            }

            if (PriceManager.Instance != null)
                foreach (KeyValuePair<ProductData, int> pair in PriceManager.Instance.CustomPrices)
                    data.prices.Add(new PriceEntry { productId = pair.Key.id, price = pair.Value });

            if (UnlockManager.Instance != null)
                foreach (ProductCategory c in UnlockManager.Instance.GetUnlocked())
                    data.unlocked.Add(c.ToString());

            foreach (ShelfSection section in ShelfSection.All)
            {
                if (section.IsEmpty) continue;
                data.shelves.Add(new ShelfEntry
                {
                    sectionPath = GetSectionKey(section),
                    productId = section.Product.id,
                    count = section.Count
                });
            }

            foreach (ProductBox box in FindObjectsByType<ProductBox>(FindObjectsSortMode.None))
            {
                data.boxes.Add(new BoxEntry
                {
                    productId = box.Product != null ? box.Product.id : "",
                    count = box.Count,
                    position = box.transform.position,
                    rotation = box.transform.rotation
                });
            }

            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true), Encoding.UTF8);
                GameEvents.Notify("Игра сохранена");
                Debug.Log($"[Сохранение] {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Сохранение] Не удалось сохранить: {e.Message}");
            }
        }

        // ----------------------------------------------------------------
        // Загрузка
        // ----------------------------------------------------------------

        public void Load()
        {
            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath, Encoding.UTF8));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Сохранение] Не удалось загрузить: {e.Message}");
                return;
            }
            if (data == null) return;

            // Сохранения до перестройки карты (итерация 11) не подходят: мебель и коробки стояли в других местах.
            if (data.version < SaveData.CurrentVersion)
            {
                Debug.LogWarning("[Сохранение] Сохранение от старой версии карты — начинаем новую игру.");
                GameEvents.Notify("Карта салона изменилась — старое сохранение не загружено, начинаем заново.");
                return;
            }

            if (EconomyManager.Instance != null) EconomyManager.Instance.SetBalance(data.balance);
            if (ReputationManager.Instance != null) ReputationManager.Instance.SetValue(data.reputation);
            if (LevelManager.Instance != null) LevelManager.Instance.SetState(data.level, data.xp);
            if (TutorialManager.Instance != null) TutorialManager.Instance.SetCompleted(data.tutorialDone || data.day > 1);
            if (ExpansionManager.Instance != null) ExpansionManager.Instance.SetStages(data.salonStage, data.storageStage);
            if (StaffManager.Instance != null) StaffManager.Instance.SetCashierHired(data.cashierHired);

            if (SkillManager.Instance != null && data.skills != null)
            {
                List<KeyValuePair<string, int>> ranks = new List<KeyValuePair<string, int>>();
                foreach (SkillEntry e in data.skills) ranks.Add(new KeyValuePair<string, int>(e.id, e.rank));
                SkillManager.Instance.SetRanks(ranks);
            }
            if (StoreEventManager.Instance != null) StoreEventManager.Instance.SetCurrent(data.eventId);
            // Ярусы полок — до восстановления товара на полках, тележка — до коробок (на ней могут стоять).
            if (UpgradeManager.Instance != null)
                UpgradeManager.Instance.SetState(data.shelfTier, data.hasCart, data.cartPosition, data.cartRotation);
            if (Story.LoreManager.Instance != null && data.letters != null)
                Story.LoreManager.Instance.SetShown(data.letters);
            if (GoalManager.Instance != null) GoalManager.Instance.SetState(data.goalReached, data.debtDays);

            RestoreFurniture(data.furniture);

            if (UnlockManager.Instance != null)
            {
                List<ProductCategory> categories = new List<ProductCategory>();
                foreach (string name in data.unlocked)
                    if (Enum.TryParse(name, out ProductCategory c)) categories.Add(c);
                UnlockManager.Instance.SetUnlocked(categories);
            }

            if (PriceManager.Instance != null)
                foreach (PriceEntry p in data.prices)
                {
                    ProductData product = FindProduct(p.productId);
                    if (product != null) PriceManager.Instance.SetPrice(product, p.price);
                }

            RestoreShelves(data.shelves);
            RestoreBoxes(data.boxes);

            if (DayManager.Instance != null) DayManager.Instance.SetDay(data.day);

            GameEvents.Notify($"Загружено сохранение: день {data.day}");
        }

        /// <summary>
        /// Мебель из сохранения: стоящая в сцене заменяется сохранённой.
        /// Старые сохранения без списка мебели оставляют сцену как есть.
        /// </summary>
        private void RestoreFurniture(List<FurnitureEntry> entries)
        {
            if (entries == null || entries.Count == 0 || furnitureCatalog == null) return;

            foreach (PlacedFurniture f in FindObjectsByType<PlacedFurniture>(FindObjectsSortMode.None))
            {
                f.gameObject.SetActive(false); // сразу убирает полки из списка ShelfSection.All
                Destroy(f.gameObject);
            }

            foreach (FurnitureEntry e in entries)
            {
                FurnitureData data = furnitureCatalog.Find(e.furnitureId);
                if (data == null || data.prefab == null) continue;

                GameObject go = Instantiate(data.prefab, e.position, e.rotation);
                go.name = data.prefab.name;
                PlacedFurniture placed = go.GetComponent<PlacedFurniture>();
                if (placed != null) placed.SetInstanceId(e.instanceId);
            }

            GameEvents.RaiseLayoutChanged();
        }

        private void RestoreShelves(List<ShelfEntry> entries)
        {
            Dictionary<string, ShelfEntry> byPath = new Dictionary<string, ShelfEntry>();
            foreach (ShelfEntry e in entries) byPath[e.sectionPath] = e;

            foreach (ShelfSection section in new List<ShelfSection>(ShelfSection.All))
            {
                // Новый формат ключа — «номер мебели/секция», старый — путь в иерархии.
                if (byPath.TryGetValue(GetSectionKey(section), out ShelfEntry entry)
                    || byPath.TryGetValue(GetPath(section.transform), out entry))
                {
                    ProductData product = FindProduct(entry.productId);
                    if (product != null) section.Restore(product, entry.count);
                    else section.Clear();
                }
                else
                {
                    section.Clear();
                }
            }
        }

        private void RestoreBoxes(List<BoxEntry> entries)
        {
            if (boxPrefab == null) return;

            // Коробки, расставленные в сцене заранее, заменяются сохранёнными.
            // Сначала снимаем их со стеллажей склада: Destroy срабатывает в конце кадра,
            // а место на стеллаже должно освободиться сразу.
            foreach (ProductBox box in FindObjectsByType<ProductBox>(FindObjectsSortMode.None))
            {
                box.transform.SetParent(null);
                box.gameObject.SetActive(false);
                Destroy(box.gameObject);
            }

            foreach (BoxEntry e in entries)
            {
                ProductBox box = Instantiate(boxPrefab, e.position + Vector3.up * 0.02f, e.rotation);
                box.Fill(FindProduct(e.productId), e.count);

                // Коробка стояла на складском стеллаже — вернуть её на место.
                StorageRack.TryAttachNear(box);
            }
        }

        private ProductData FindProduct(string id)
        {
            if (catalog == null || string.IsNullOrEmpty(id)) return null;
            return catalog.products.Find(p => p != null && p.id == id);
        }

        /// <summary>Ключ полки: номер экземпляра мебели и имя секции.</summary>
        private static string GetSectionKey(ShelfSection section)
        {
            PlacedFurniture furniture = section.GetComponentInParent<PlacedFurniture>();
            return furniture != null ? $"{furniture.InstanceId}/{section.name}" : GetPath(section.transform);
        }

        /// <summary>Путь объекта в иерархии, например «Shelf/Section_1A».</summary>
        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        /// <summary>Номер дня в сохранении (для кнопки «Продолжить»), 0 — если сохранения нет.</summary>
        public static int ReadSavedDay()
        {
            if (!HasSave) return 0;
            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath, Encoding.UTF8));
                return data != null && data.version >= SaveData.CurrentVersion ? data.day : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Удалить файл сохранения.</summary>
        public static void DeleteSave()
        {
            if (HasSave) File.Delete(SavePath);
        }
    }

    // ----------------------------------------------------------------
    // Структура файла сохранения
    // ----------------------------------------------------------------

    [Serializable]
    public class SaveData
    {
        /// <summary>3 — новая карта (итерация 11): склад, этапы расширения зала.</summary>
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public int day = 1;
        public int balance;
        public int reputation = 50;
        public int level = 1;
        public int xp;
        public bool tutorialDone;
        public int salonStage;
        public int storageStage;
        public bool cashierHired;
        public List<FurnitureEntry> furniture = new List<FurnitureEntry>();
        public List<PriceEntry> prices = new List<PriceEntry>();
        public List<string> unlocked = new List<string>();
        public List<ShelfEntry> shelves = new List<ShelfEntry>();
        public List<BoxEntry> boxes = new List<BoxEntry>();
        public List<SkillEntry> skills = new List<SkillEntry>();
        public string eventId = "";
        public int shelfTier;
        public bool hasCart;
        public Vector3 cartPosition;
        public Quaternion cartRotation = Quaternion.identity;
        public List<string> letters = new List<string>();
        public bool goalReached;
        public int debtDays;
    }

    [Serializable]
    public class SkillEntry
    {
        public string id;
        public int rank;
    }

    [Serializable]
    public class FurnitureEntry
    {
        public string furnitureId;
        public string instanceId;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    public class PriceEntry
    {
        public string productId;
        public int price;
    }

    [Serializable]
    public class ShelfEntry
    {
        public string sectionPath;
        public string productId;
        public int count;
    }

    [Serializable]
    public class BoxEntry
    {
        public string productId;
        public int count;
        public Vector3 position;
        public Quaternion rotation;
    }
}
