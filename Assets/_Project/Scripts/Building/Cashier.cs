using NaSvyazi.Progression;
using NaSvyazi.Store;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Нанятый кассир: сам сканирует товары, принимает оплату и консультирует по тарифам.
    /// Работает медленнее игрока и в консультациях иногда ошибается.
    /// Игрок может в любой момент обслужить покупателя сам.
    /// </summary>
    public class Cashier : MonoBehaviour
    {
        [SerializeField] private CashRegister register;

        [SerializeField] private float scanInterval = 1.1f;
        [SerializeField] private float paymentDelay = 1.6f;
        [SerializeField] private float consultationDelay = 4f;

        [Tooltip("Вероятность, что кассир подберёт клиенту самый выгодный тариф")]
        [Range(0f, 1f)] [SerializeField] private float consultationAccuracy = 0.75f;

        private RegisterState lastState;
        private float timer;

        private void Update()
        {
            if (register == null) return;

            if (register.State != lastState)
            {
                lastState = register.State;
                timer = 0f;
            }

            // Навык «Обучение кассира» ускоряет работу.
            timer += Time.deltaTime * SkillManager.CashierSpeedMultiplier;

            switch (register.State)
            {
                case RegisterState.Scanning:
                    if (timer >= scanInterval)
                    {
                        timer = 0f;
                        register.ScanNextAuto();
                    }
                    break;

                case RegisterState.AwaitingPayment:
                    if (timer >= paymentDelay) register.AutoAcceptPayment();
                    break;

                case RegisterState.Consulting:
                    if (timer >= consultationDelay) register.AutoConsult(SkillManager.TariffExpert ? 1f : consultationAccuracy);
                    break;
            }
        }
    }
}
