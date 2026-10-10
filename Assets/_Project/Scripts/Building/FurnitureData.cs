using System.Collections.Generic;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>Вид мебели, который можно купить в терминале и расставить в зале.</summary>
    [CreateAssetMenu(fileName = "Furniture_", menuName = "На связи/Мебель", order = 20)]
    public class FurnitureData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;

        [Min(0)] public int price = 2000;

        [Tooltip("С какого уровня салона можно купить")]
        [Min(1)] public int minLevel = 1;

        [Tooltip("Префаб с компонентом PlacedFurniture")]
        public GameObject prefab;
    }

    /// <summary>Каталог мебели для терминала и для загрузки сохранений.</summary>
    [CreateAssetMenu(fileName = "FurnitureCatalog", menuName = "На связи/Каталог мебели", order = 21)]
    public class FurnitureCatalog : ScriptableObject
    {
        public List<FurnitureData> items = new List<FurnitureData>();

        public FurnitureData Find(string id) => items.Find(f => f != null && f.id == id);
    }
}
