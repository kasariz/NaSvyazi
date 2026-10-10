namespace NaSvyazi.Products
{
    /// <summary>Категории товаров салона связи.</summary>
    public enum ProductCategory
    {
        Accessories = 0, // Аксессуары: чехлы, стёкла
        Cables = 1,      // Кабели и зарядные устройства
        SimCards = 2,    // SIM-карты и стартовые пакеты
        Audio = 3,       // Наушники, колонки
        Phones = 4       // Смартфоны
    }

    public static class ProductCategoryExtensions
    {
        /// <summary>
        /// Дорогие товары (смартфоны, аудио) выставляются только в витрину — как в настоящем салоне.
        /// </summary>
        public static bool RequiresShowcase(this ProductCategory category) =>
            category == ProductCategory.Phones || category == ProductCategory.Audio;

        /// <summary>Название категории для интерфейса.</summary>
        public static string ToDisplayName(this ProductCategory category)
        {
            switch (category)
            {
                case ProductCategory.Accessories: return "Аксессуары";
                case ProductCategory.Cables: return "Кабели и зарядки";
                case ProductCategory.SimCards: return "SIM-карты";
                case ProductCategory.Audio: return "Аудио";
                case ProductCategory.Phones: return "Смартфоны";
                default: return category.ToString();
            }
        }
    }
}
