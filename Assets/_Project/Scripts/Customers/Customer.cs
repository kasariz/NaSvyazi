using System.Collections.Generic;
using NaSvyazi.Economy;
using NaSvyazi.Products;
using NaSvyazi.Store;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace NaSvyazi.Customers
{
    /// <summary>Состояния покупателя (конечный автомат).</summary>
    public enum CustomerState
    {
        GoingToShelf, // идёт к полке с нужным товаром
        Browsing,     // смотрит товар и цену, принимает решение
        InQueue,      // стоит в очереди к кассе
        Leaving       // уходит из салона
    }

    /// <summary>Товар в корзине и цена, по которой он был взят с полки.</summary>
    public struct BasketItem
    {
        public ProductData product;
        public int price;

        public BasketItem(ProductData product, int price)
        {
            this.product = product;
            this.price = price;
        }
    }

    /// <summary>
    /// Покупатель. Приходит со списком желаемых товаров, обходит полки,
    /// по каждой позиции решает, покупать ли по текущей цене (DemandModel),
    /// затем встаёт в очередь к кассе. Если очередь двигается слишком долго — уходит.
    /// Перемещение — NavMeshAgent по навигационной сетке салона.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Customer : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private TMP_Text bubble;

        [Header("Поведение")]
        [SerializeField] private float browseTime = 1.5f;
        [SerializeField] private float queuePatience = 90f;
        [SerializeField] private float bubbleTime = 2.5f;

        [Tooltip("Если покупатель не может дойти до цели за это время — он переходит дальше")]
        [SerializeField] private float stuckTimeout = 25f;

        private readonly List<BasketItem> basket = new List<BasketItem>();
        private List<ProductData> shoppingList = new List<ProductData>();

        private NavMeshAgent agent;
        private CheckoutQueue queue;
        private Vector3 exitPoint;
        private float priceTolerance;

        private int listIndex;
        private ShelfSection targetSection;
        private float stateTimer;
        private float patienceTimer;
        private float bubbleTimer;
        private int queueIndex = -1;

        public CustomerState State { get; private set; }
        public int ItemCount => basket.Count;
        public IReadOnlyList<BasketItem> Basket => basket;

        public int BasketTotal
        {
            get
            {
                int sum = 0;
                foreach (BasketItem item in basket) sum += item.price;
                return sum;
            }
        }

        /// <summary>Готов платить: первый в очереди и уже дошёл до кассы.</summary>
        public bool IsReadyToPay =>
            State == CustomerState.InQueue && queue != null && queue.IndexOf(this) == 0 && HasArrived();

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();

            // Случайный цвет одежды, чтобы покупатели различались.
            if (bodyRenderer != null)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, Random.ColorHSV(0f, 1f, 0.35f, 0.7f, 0.55f, 0.9f));
                bodyRenderer.SetPropertyBlock(block);
            }

            if (bubble != null) bubble.text = "";
        }

        /// <summary>Инициализация сразу после появления.</summary>
        public void Init(List<ProductData> wants, float tolerance, CheckoutQueue checkoutQueue, Vector3 exit)
        {
            shoppingList = wants;
            priceTolerance = tolerance;
            queue = checkoutQueue;
            exitPoint = exit;
            listIndex = 0;
            GoToNextShelf();
        }

        private void Update()
        {
            stateTimer += Time.deltaTime;

            switch (State)
            {
                case CustomerState.GoingToShelf:
                    if (HasArrived())
                    {
                        State = CustomerState.Browsing;
                        stateTimer = 0f;
                        FaceTowards(targetSection != null ? targetSection.transform.position : transform.position);
                    }
                    else if (stateTimer > stuckTimeout)
                    {
                        listIndex++;
                        GoToNextShelf();
                    }
                    break;

                case CustomerState.Browsing:
                    if (stateTimer >= browseTime) DecideAndTake();
                    break;

                case CustomerState.InQueue:
                    UpdateQueue();
                    break;

                case CustomerState.Leaving:
                    if (HasArrived() || stateTimer > stuckTimeout * 2f) Destroy(gameObject);
                    break;
            }

            UpdateBubble();
        }

        // --- Покупки ---

        private void GoToNextShelf()
        {
            while (listIndex < shoppingList.Count)
            {
                ProductData wanted = shoppingList[listIndex];
                ShelfSection section = FindSectionWith(wanted);

                if (section != null)
                {
                    targetSection = section;
                    SetState(CustomerState.GoingToShelf);
                    MoveTo(section.CustomerStandPoint);
                    return;
                }

                Say($"Нет в наличии: {wanted.displayName}");
                StoreStats.OutOfStockMisses++;
                listIndex++;
            }

            // Список обойдён.
            if (basket.Count > 0) JoinQueue();
            else
            {
                StoreStats.LeftEmptyHanded++;
                Leave();
            }
        }

        private void DecideAndTake()
        {
            ProductData wanted = shoppingList[listIndex];

            // Пока шли, товар могли разобрать — ищем другую полку.
            if (targetSection == null || targetSection.Product != wanted || targetSection.IsEmpty)
            {
                GoToNextShelf();
                return;
            }

            int price = PriceManager.PriceOf(wanted);
            float chance = DemandModel.BuyProbability(price, wanted.marketPrice, priceTolerance);

            if (Random.value <= chance)
            {
                if (targetSection.TryTakeOne(out ProductData taken))
                    basket.Add(new BasketItem(taken, price));
            }
            else
            {
                Say("Слишком дорого!");
                StoreStats.PriceRefusals++;
            }

            listIndex++;
            GoToNextShelf();
        }

        private static ShelfSection FindSectionWith(ProductData product)
        {
            ShelfSection best = null;
            foreach (ShelfSection s in ShelfSection.All)
            {
                if (s.Product == product && !s.IsEmpty)
                {
                    best = s;
                    break;
                }
            }
            return best;
        }

        // --- Очередь и оплата ---

        private void JoinQueue()
        {
            if (queue == null)
            {
                Leave();
                return;
            }

            queue.Join(this);
            queueIndex = -1;
            patienceTimer = queuePatience;
            SetState(CustomerState.InQueue);
        }

        private void UpdateQueue()
        {
            int index = queue.IndexOf(this);
            if (index != queueIndex)
            {
                queueIndex = index;
                MoveTo(queue.GetSpot(index));
            }

            if (HasArrived())
                FaceTowards(transform.position + queue.FacingDirection);

            patienceTimer -= Time.deltaTime;
            if (patienceTimer <= 0f)
            {
                Say("Слишком долго!");
                StoreStats.LeftQueue++;
                queue.Leave(this);
                basket.Clear();
                Leave();
            }
        }

        /// <summary>Покупка оплачена — покупатель уходит довольным.</summary>
        public void CompletePurchase()
        {
            if (queue != null) queue.Leave(this);
            basket.Clear();
            Say("Спасибо!");
            Leave();
        }

        private void Leave()
        {
            SetState(CustomerState.Leaving);
            MoveTo(exitPoint);
        }

        // --- Перемещение ---

        private void SetState(CustomerState state)
        {
            State = state;
            stateTimer = 0f;
        }

        private void MoveTo(Vector3 point)
        {
            if (!agent.isOnNavMesh) return;

            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                point = hit.position;

            agent.isStopped = false;
            agent.SetDestination(point);
        }

        private bool HasArrived()
        {
            if (!agent.isOnNavMesh || agent.pathPending) return false;
            return agent.remainingDistance <= agent.stoppingDistance + 0.1f;
        }

        private void FaceTowards(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.2f);
        }

        // --- «Облачко» с репликой над головой ---

        private void Say(string text)
        {
            if (bubble == null) return;
            bubble.text = text;
            bubbleTimer = bubbleTime;
        }

        private void UpdateBubble()
        {
            if (bubble == null) return;

            if (bubbleTimer > 0f)
            {
                bubbleTimer -= Time.deltaTime;
                if (bubbleTimer <= 0f) bubble.text = "";
            }
        }

        private void LateUpdate()
        {
            // Надпись всегда повёрнута к камере игрока.
            Camera cam = Camera.main;
            if (bubble != null && cam != null && bubble.text.Length > 0)
                bubble.transform.rotation = Quaternion.LookRotation(bubble.transform.position - cam.transform.position);
        }

        private void OnDestroy()
        {
            if (queue != null) queue.Leave(this);
        }
    }
}
