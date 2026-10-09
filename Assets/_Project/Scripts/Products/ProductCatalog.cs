using System.Collections.Generic;
using UnityEngine;

namespace NaSvyazi.Products
{
    /// <summary>
    /// Каталог поставщика: список товаров, доступных для заказа.
    /// Позже здесь появится открытие новых категорий по мере развития салона.
    /// </summary>
    [CreateAssetMenu(fileName = "ProductCatalog", menuName = "На связи/Каталог товаров", order = 1)]
    public class ProductCatalog : ScriptableObject
    {
        public List<ProductData> products = new List<ProductData>();
    }
}
