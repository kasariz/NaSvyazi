using System.Collections.Generic;
using NaSvyazi.Customers;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Очередь к кассе. Первое место — у кассы, следующие выстраиваются
    /// за ним по линии назад. Когда покупатель уходит, остальные продвигаются.
    /// Синяя ось Z объекта должна смотреть на кассу.
    /// </summary>
    public class CheckoutQueue : MonoBehaviour
    {
        [Tooltip("Расстояние между людьми в очереди, м")]
        [SerializeField] private float spacing = 0.9f;

        private readonly List<Customer> line = new List<Customer>();

        public int Count => line.Count;
        public Customer First => line.Count > 0 ? line[0] : null;

        public void Join(Customer customer)
        {
            if (!line.Contains(customer)) line.Add(customer);
        }

        public void Leave(Customer customer) => line.Remove(customer);

        public int IndexOf(Customer customer) => line.IndexOf(customer);

        /// <summary>Мировая позиция места в очереди с номером index (0 — у кассы).</summary>
        public Vector3 GetSpot(int index) => transform.position - transform.forward * (spacing * index);

        /// <summary>Направление взгляда стоящих в очереди (на кассу).</summary>
        public Vector3 FacingDirection => transform.forward;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.8f);
            for (int i = 0; i < 5; i++)
                Gizmos.DrawWireSphere(GetSpot(i) + Vector3.up * 0.1f, 0.2f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.6f);
        }
    }
}
