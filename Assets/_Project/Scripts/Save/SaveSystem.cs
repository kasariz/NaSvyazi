using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Products;
using NaSvyazi.Store;
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
                    sectionPath = GetPath(section.transform),
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

            if (EconomyManager.Instance != null) EconomyManager.Instance.SetBalance(data.balance);
            if (ReputationManager.Instance != null) ReputationManager.Instance.SetValue(data.reputation);

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

        private void RestoreShelves(List<ShelfEntry> entries)
        {
            Dictionary<string, ShelfEntry> byPath = new Dictionary<string, ShelfEntry>();
            foreach (ShelfEntry e in entries) byPath[e.sectionPath] = e;

            foreach (ShelfSection section in new List<ShelfSection>(ShelfSection.All))
            {
                if (byPath.TryGetValue(GetPath(section.transform), out ShelfEntry entry))
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
            foreach (ProductBox box in FindObjectsByType<ProductBox>(FindObjectsSortMode.None))
                Destroy(box.gameObject);

            foreach (BoxEntry e in entries)
            {
                ProductBox box = Instantiate(boxPrefab, e.position + Vector3.up * 0.02f, e.rotation);
                box.Fill(FindProduct(e.productId), e.count);
            }
        }

        private ProductData FindProduct(string id)
        {
            if (catalog == null || string.IsNullOrEmpty(id)) return null;
            return catalog.products.Find(p => p != null && p.id == id);
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
                return data != null ? data.day : 0;
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
        public int version = 1;
        public int day = 1;
        public int balance;
        public int reputation = 50;
        public List<PriceEntry> prices = new List<PriceEntry>();
        public List<string> unlocked = new List<string>();
        public List<ShelfEntry> shelves = new List<ShelfEntry>();
        public List<BoxEntry> boxes = new List<BoxEntry>();
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
