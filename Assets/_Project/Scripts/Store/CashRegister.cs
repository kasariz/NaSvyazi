using NaSvyazi.Customers;
using NaSvyazi.Economy;
using NaSvyazi.Interaction;
using NaSvyazi.Player;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Касса. Игрок обслуживает первого покупателя в очереди: принимает оплату,
    /// деньги поступают на баланс, покупатель уходит.
    /// (В следующей итерации здесь появятся сканирование товара и выбор способа оплаты.)
    /// </summary>
    public class CashRegister : MonoBehaviour, IInteractable
    {
        [SerializeField] private CheckoutQueue queue;

        private Customer ReadyCustomer
        {
            get
            {
                Customer first = queue != null ? queue.First : null;
                return first != null && first.IsReadyToPay ? first : null;
            }
        }

        public string GetPrompt(PlayerInteractor interactor)
        {
            Customer c = ReadyCustomer;
            return c == null ? null : $"Пробить покупку: {c.ItemCount} шт. на {EconomyManager.Format(c.BasketTotal)}";
        }

        public bool CanInteract(PlayerInteractor interactor) => ReadyCustomer != null;

        public void Interact(PlayerInteractor interactor)
        {
            Customer c = ReadyCustomer;
            if (c == null) return;

            int total = c.BasketTotal;
            if (EconomyManager.Instance != null) EconomyManager.Instance.Earn(total, "Продажа");

            StoreStats.CustomersServed++;
            StoreStats.ItemsSold += c.ItemCount;

            c.CompletePurchase();
        }
    }
}
