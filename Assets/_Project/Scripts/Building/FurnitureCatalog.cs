using System.Collections.Generic;
using UnityEngine;

namespace NaSvyazi.Building
{
    // Unity сохраняет ScriptableObject только если класс лежит в файле с тем же именем:
    // иначе в собранной игре ссылка на каталог становится пустой.
    /// <summary>Каталог мебели для терминала и для загрузки сохранений.</summary>
    [CreateAssetMenu(fileName = "FurnitureCatalog", menuName = "На связи/Каталог мебели", order = 21)]
    public class FurnitureCatalog : ScriptableObject
    {
        public List<FurnitureData> items = new List<FurnitureData>();

        public FurnitureData Find(string id) => items.Find(f => f != null && f.id == id);
    }
}
