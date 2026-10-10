using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Interaction;
using NaSvyazi.Player;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>Что продают соседи.</summary>
    public enum NeighborOffer
    {
        Coffee, // кофейня «Зерно»: герой какое-то время двигается быстрее
        Flowers // цветочный «Вера»: букет для салона — прибавка к репутации (раз в день)
    }

    /// <summary>
    /// Прилавок соседнего магазина: E — купить. Кофе ускоряет героя на пару минут,
    /// букет поднимает репутацию салона (один раз за игровой день).
    /// </summary>
    public class NeighborShop : MonoBehaviour, IInteractable
    {
        [SerializeField] private NeighborOffer offer;
        [SerializeField] private int price = 150;

        [Header("Кофе")]
        [SerializeField] private float speedBonus = 1.3f;
        [SerializeField] private float duration = 120f;

        [Header("Цветы")]
        [SerializeField] private int reputationBonus = 3;

        private static float coffeeLeft;
        private static int flowersDay = -1;

        private void Awake()
        {
            // Статические таймеры сбрасываются при загрузке сцены (новая игра / «Продолжить»).
            coffeeLeft = 0f;
            flowersDay = -1;
        }

        public string GetPrompt(PlayerInteractor interactor)
        {
            switch (offer)
            {
                case NeighborOffer.Coffee:
                    return coffeeLeft > 0f
                        ? $"Ещё кофе — {EconomyManager.Format(price)} (бодрость продлится)"
                        : $"Купить кофе — {EconomyManager.Format(price)} (быстрее ходить {duration / 60f:0} мин)";
                default:
                    return BoughtFlowersToday
                        ? "Букет на сегодня уже куплен"
                        : $"Купить букет для салона — {EconomyManager.Format(price)} (+{reputationBonus} к репутации)";
            }
        }

        private static bool BoughtFlowersToday =>
            DayManager.Instance != null && flowersDay == DayManager.Instance.Day;

        public bool CanInteract(PlayerInteractor interactor) =>
            offer == NeighborOffer.Coffee || !BoughtFlowersToday;

        public void Interact(PlayerInteractor interactor)
        {
            EconomyManager economy = EconomyManager.Instance;
            if (economy == null || !economy.TrySpend(price, offer == NeighborOffer.Coffee ? "Кофе" : "Букет для салона"))
            {
                GameEvents.Notify("Недостаточно денег");
                SoundManager.Play(Sfx.Error);
                return;
            }

            SoundManager.Play(Sfx.Purchase, 0.6f);
            if (offer == NeighborOffer.Coffee)
            {
                coffeeLeft = duration;
                PlayerController.SpeedMultiplier = speedBonus;
                GameEvents.Notify($"Кофе выпит! {duration / 60f:0} минуты вы двигаетесь быстрее.");
            }
            else
            {
                flowersDay = DayManager.Instance != null ? DayManager.Instance.Day : 0;
                ReputationManager.Add(reputationBonus, "букет в салоне");
                GameEvents.Notify($"Свежий букет на прилавке: +{reputationBonus} к репутации.");
            }
        }

        private void Update()
        {
            // Таймер бодрости ведёт только кофейня (один экземпляр в сцене).
            if (offer != NeighborOffer.Coffee || coffeeLeft <= 0f) return;

            coffeeLeft -= Time.deltaTime;
            if (coffeeLeft <= 0f)
            {
                PlayerController.SpeedMultiplier = 1f;
                GameEvents.Notify("Кофе выветрился");
            }
        }

        private void OnDestroy()
        {
            if (offer == NeighborOffer.Coffee)
            {
                coffeeLeft = 0f;
                PlayerController.SpeedMultiplier = 1f;
            }
        }

        /// <summary>Сколько секунд бодрости осталось (для интерфейса).</summary>
        public static float CoffeeLeft => coffeeLeft;
    }
}
