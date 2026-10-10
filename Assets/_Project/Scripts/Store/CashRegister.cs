using System.Collections.Generic;
using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Customers;
using NaSvyazi.Economy;
using NaSvyazi.Interaction;
using NaSvyazi.Player;
using NaSvyazi.Tariffs;
using NaSvyazi.UI;
using TMPro;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>Состояния кассы.</summary>
    public enum RegisterState
    {
        Idle,            // нет покупателя у кассы
        Scanning,        // товары на прилавке, игрок сканирует
        AwaitingPayment, // всё отсканировано, ждём оплату
        Consulting       // клиент пришёл за консультацией по тарифу
    }

    /// <summary>
    /// Касса — конечный автомат обслуживания покупателя:
    /// покупатель подходит → выкладывает товары на прилавок → игрок сканирует каждый (ЛКМ) →
    /// покупатель платит картой или наличными (игрок отсчитывает сдачу) → уходит.
    /// Клиенту, пришедшему за консультацией, игрок подбирает тариф.
    /// </summary>
    public class CashRegister : MonoBehaviour, IInteractable
    {
        [SerializeField] private CheckoutQueue queue;
        [SerializeField] private ChangeUI changeUI;
        [SerializeField] private ConsultationUI consultationUI;
        [SerializeField] private TariffCatalog tariffCatalog;

        [Header("Раскладка товаров на прилавке (относительно кассы)")]
        // Касса повёрнута к кассиру (на 180°), поэтому «вправо от кассы» в мире — это −X в её осях.
        [Tooltip("Место первого товара: справа от кассы (если смотреть из-за прилавка)")]
        [SerializeField] private Vector3 itemsStart = new Vector3(-0.38f, 0f, -0.08f);
        [SerializeField] private Vector3 itemsColumnStep = new Vector3(-0.17f, 0f, 0f);
        [SerializeField] private Vector3 itemsRowStep = new Vector3(0f, 0f, 0.18f);
        [SerializeField] private int itemsColumns = 5;

        private readonly List<CheckoutItem> items = new List<CheckoutItem>();
        private TMP_Text display;
        private Customer current;
        private int scannedTotal;

        public RegisterState State { get; private set; } = RegisterState.Idle;

        private void Awake()
        {
            display = GetComponentInChildren<TMP_Text>();
            UpdateDisplay();
        }

        private void Update()
        {
            // Покупатель ушёл сам (например, кончилось терпение) — сбрасываем кассу.
            if (State != RegisterState.Idle && current == null)
            {
                ResetRegister();
                return;
            }

            if (State == RegisterState.Idle && queue != null)
            {
                Customer first = queue.First;
                if (first != null && first.IsReadyToPay) BeginService(first);
            }
        }

        // --- Обслуживание ---

        private void BeginService(Customer customer)
        {
            current = customer;
            current.SetBeingServed(true);

            if (customer.Intent == CustomerIntent.Consultation)
            {
                State = RegisterState.Consulting;
                if (customer.Need != null) customer.Say(customer.Need.request);
            }
            else
            {
                State = RegisterState.Scanning;
                scannedTotal = 0;
                SpawnItems(customer.Basket);
            }

            UpdateDisplay();
        }

        private void SpawnItems(IReadOnlyList<BasketItem> basket)
        {
            for (int i = 0; i < basket.Count; i++)
            {
                Vector3 local = itemsStart + itemsColumnStep * (i % itemsColumns)
                                           + itemsRowStep * (i / itemsColumns);

                GameObject go = new GameObject($"CheckoutItem_{i}");
                go.transform.SetPositionAndRotation(transform.TransformPoint(local), transform.rotation);

                CheckoutItem item = go.AddComponent<CheckoutItem>();
                item.Init(this, basket[i]);
                items.Add(item);
            }
        }

        /// <summary>Отсканировать товар (вызывается из CheckoutItem по ЛКМ).</summary>
        public void Scan(CheckoutItem item)
        {
            if (State != RegisterState.Scanning || !items.Remove(item)) return;

            scannedTotal += item.Price;
            Destroy(item.gameObject);
            SoundManager.Play(Sfx.Scan);
            UpdateDisplay();

            if (items.Count == 0)
            {
                State = RegisterState.AwaitingPayment;
                current.Say(current.PaysByCard
                    ? "Оплачу картой"
                    : $"Вот, {EconomyManager.Format(current.CashGiven)} наличными");
                UpdateDisplay();
            }
        }

        // --- Автоматическая работа (нанятый кассир) ---

        /// <summary>Отсканировать следующий товар на прилавке.</summary>
        public void ScanNextAuto()
        {
            if (State == RegisterState.Scanning && items.Count > 0) Scan(items[0]);
        }

        /// <summary>Принять оплату: картой или наличными с точной сдачей.</summary>
        public void AutoAcceptPayment()
        {
            if (State != RegisterState.AwaitingPayment || current == null) return;

            if (current.PaysByCard) StoreStats.CardPayments++;
            else StoreStats.CashPayments++;
            FinishSale(scannedTotal, "Спасибо!");
        }

        /// <summary>Подобрать тариф: с вероятностью accuracy — лучший, иначе случайный.</summary>
        public void AutoConsult(float accuracy)
        {
            if (State != RegisterState.Consulting || current == null || tariffCatalog == null) return;

            TariffData best = tariffCatalog.BestFor(current.Need);
            TariffData chosen = best;
            if (best == null || Random.value > accuracy)
            {
                List<TariffData> all = tariffCatalog.tariffs.FindAll(t => t != null);
                if (all.Count > 0) chosen = all[Random.Range(0, all.Count)];
            }
            OnTariffChosen(chosen);
        }

        // --- IInteractable (E): приём оплаты или консультация ---

        public string GetPrompt(PlayerInteractor interactor)
        {
            switch (State)
            {
                case RegisterState.AwaitingPayment:
                    return current.PaysByCard
                        ? $"Принять оплату картой: {EconomyManager.Format(scannedTotal)}"
                        : $"Принять наличные: {EconomyManager.Format(current.CashGiven)} (к оплате {EconomyManager.Format(scannedTotal)})";
                case RegisterState.Consulting:
                    return "Подобрать тариф клиенту";
                default:
                    return null;
            }
        }

        public bool CanInteract(PlayerInteractor interactor) =>
            !ModalWindow.IsAnyOpen &&
            (State == RegisterState.AwaitingPayment || State == RegisterState.Consulting);

        public void Interact(PlayerInteractor interactor)
        {
            if (State == RegisterState.AwaitingPayment)
            {
                if (current.PaysByCard)
                {
                    StoreStats.CardPayments++;
                    FinishSale(scannedTotal, "Спасибо!");
                }
                else if (changeUI != null)
                {
                    changeUI.OpenFor(scannedTotal, current.CashGiven, OnChangeGiven, interactor.Controller);
                }
            }
            else if (State == RegisterState.Consulting && consultationUI != null && tariffCatalog != null)
            {
                consultationUI.OpenFor(current.Need, tariffCatalog, OnTariffChosen, interactor.Controller);
            }
        }

        /// <summary>Игрок выдал сдачу. Лишняя сдача — убыток салона.</summary>
        private void OnChangeGiven(int changeGiven)
        {
            if (current == null) return;

            int due = current.CashGiven - scannedTotal;
            int income = current.CashGiven - changeGiven;

            StoreStats.CashPayments++;
            if (changeGiven > due)
            {
                StoreStats.ChangeErrors++;
                StoreStats.ChangeLoss += changeGiven - due;
                GameEvents.Notify($"Вы выдали лишнюю сдачу: {EconomyManager.Format(changeGiven - due)}");
            }

            FinishSale(income, changeGiven > due ? "Ого, спасибо!" : "Спасибо!");
        }

        private void FinishSale(int income, string phrase)
        {
            if (EconomyManager.Instance != null) EconomyManager.Instance.Earn(income, "Продажа");
            SoundManager.Play(Sfx.Purchase);

            StoreStats.CustomersServed++;
            StoreStats.ItemsSold += current.ItemCount;
            ReputationManager.Add(1, "покупатель обслужен");
            LevelManager.AddXp(1 + current.ItemCount, "продажа");
            GameEvents.RaiseSaleCompleted();

            current.CompletePurchase(phrase);
            ResetRegister();
        }

        /// <summary>
        /// Взять стартовый пакет SIM с полки (для подключения тарифа). Возвращает его цену продажи.
        /// </summary>
        private static bool TakeSimFromShelf(out int simPrice)
        {
            foreach (ShelfSection section in ShelfSection.All)
            {
                if (section.IsEmpty || section.Product.category != Products.ProductCategory.SimCards) continue;
                if (section.TryTakeOne(out Products.ProductData sim))
                {
                    simPrice = PriceManager.PriceOf(sim);
                    return true;
                }
            }
            simPrice = 0;
            return false;
        }

        /// <summary>
        /// Итог консультации. Для подключения нужен стартовый пакет SIM с полки:
        /// доход — абонентская плата за первый месяц плюс цена SIM.
        /// </summary>
        private void OnTariffChosen(TariffData chosen)
        {
            if (current == null) return;

            ConsultationResult result = tariffCatalog.Evaluate(current.Need, chosen);

            int simPrice = 0;
            if (result != ConsultationResult.Unsuitable && !TakeSimFromShelf(out simPrice))
            {
                StoreStats.OutOfStockMisses++;
                ReputationManager.Add(-2, "нет SIM-карт для подключения");
                SoundManager.Play(Sfx.Error);
                GameEvents.Notify("На полках нет SIM-карт — клиент ушёл без подключения");
                current.CompletePurchase("Как это — нет SIM-карт?");
                ResetRegister();
                return;
            }
            if (simPrice > 0) StoreStats.ItemsSold++;
            switch (result)
            {
                case ConsultationResult.Best:
                    StoreStats.ConsultationsBest++;
                    ReputationManager.Add(3, "точно подобран тариф");
                    LevelManager.AddXp(6, "точная консультация");
                    GameEvents.RaiseSaleCompleted();
                    Earn(chosen.monthlyFee + simPrice, "Подключение тарифа");
                    SoundManager.Play(Sfx.Purchase);
                    GameEvents.Notify($"Отличный выбор! Клиент подключил «{chosen.displayName}»");
                    current.CompletePurchase("То, что нужно, спасибо!");
                    break;

                case ConsultationResult.Acceptable:
                    StoreStats.ConsultationsAcceptable++;
                    ReputationManager.Add(-1, "клиенту подобран тариф дороже нужного");
                    LevelManager.AddXp(2, "консультация");
                    GameEvents.RaiseSaleCompleted();
                    Earn(chosen.monthlyFee + simPrice, "Подключение тарифа");
                    TariffData best = tariffCatalog.BestFor(current.Need);
                    GameEvents.Notify(best != null
                        ? $"Подходит, но выгоднее был бы «{best.displayName}»"
                        : "Тариф подходит");
                    current.CompletePurchase("Дороговато, но ладно");
                    break;

                default:
                    StoreStats.ConsultationsFailed++;
                    ReputationManager.Add(-4, "неподходящий тариф");
                    SoundManager.Play(Sfx.Error);
                    GameEvents.Notify("Тариф не подходит клиенту — он ушёл без подключения");
                    current.CompletePurchase("Это мне не подходит...");
                    break;
            }

            ResetRegister();
        }

        private static void Earn(int amount, string reason)
        {
            if (EconomyManager.Instance != null) EconomyManager.Instance.Earn(amount, reason);
        }

        private void ResetRegister()
        {
            foreach (CheckoutItem item in items)
                if (item != null) Destroy(item.gameObject);
            items.Clear();

            current = null;
            scannedTotal = 0;
            State = RegisterState.Idle;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (display == null) return;

            switch (State)
            {
                case RegisterState.Scanning:
                    display.text = $"ИТОГО\n{EconomyManager.Format(scannedTotal)}";
                    break;
                case RegisterState.AwaitingPayment:
                    display.text = $"К ОПЛАТЕ\n{EconomyManager.Format(scannedTotal)}";
                    break;
                case RegisterState.Consulting:
                    display.text = "ТАРИФЫ";
                    break;
                default:
                    display.text = "КАССА";
                    break;
            }
        }
    }
}
