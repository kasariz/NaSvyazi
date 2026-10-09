using NaSvyazi.Customers;
using NaSvyazi.Economy;
using NaSvyazi.Interaction;
using NaSvyazi.Player;
using NaSvyazi.Products;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Товар, выложенный покупателем на прилавок. Игрок сканирует его кликом (ЛКМ),
    /// после чего сумма на дисплее кассы увеличивается.
    /// </summary>
    public class CheckoutItem : MonoBehaviour, IActionTarget
    {
        private CashRegister register;

        public ProductData Product { get; private set; }
        public int Price { get; private set; }

        public void Init(CashRegister owner, BasketItem item)
        {
            register = owner;
            Product = item.product;
            Price = item.price;

            GameObject visual = Product.CreateVisual(transform);
            visual.transform.localPosition = Vector3.zero;

            // Коллайдер чуть больше товара, чтобы по нему было легко попасть прицелом.
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.size = Product.size + new Vector3(0.04f, 0.04f, 0.04f);
            box.center = new Vector3(0f, Product.size.y * 0.5f, 0f);
        }

        public string GetPrimaryPrompt(PlayerInteractor interactor) =>
            $"Сканировать: {Product.displayName} — {EconomyManager.Format(Price)}";

        public void DoPrimary(PlayerInteractor interactor) => register.Scan(this);

        public string GetSecondaryPrompt(PlayerInteractor interactor) => null;
        public void DoSecondary(PlayerInteractor interactor) { }
    }
}
