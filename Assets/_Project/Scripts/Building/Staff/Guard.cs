using System.Collections;
using System.Collections.Generic;
using NaSvyazi.Core;
using NaSvyazi.Customers;
using NaSvyazi.Progression;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Охранник: стоит у входа. Если мимо идёт вор с товаром, бросается наперерез и задерживает его
    /// с вероятностью 60 % (+15 % за ранг навыка «Мотивация персонала»). Товар возвращается на полки,
    /// как если бы вора задержал игрок. Один вор — одна попытка.
    /// </summary>
    public class Guard : StaffWorker
    {
        public static readonly Vector3 Post = new Vector3(-0.6f, 0f, -8.4f);
        private const float NoticeRadius = 5f;
        private const float CatchRadius = 1.4f;

        protected override string Title => "Охранник";
        protected override string AnimationPrefix => "guard";
        protected override float SpeedMultiplier => 1.5f; // бегает быстрее вора

        private readonly HashSet<Customer> tried = new HashSet<Customer>();

        protected override IEnumerator Work()
        {
            yield return WalkTo(Post, 0.3f);
            FaceTowards(Post + Vector3.back);

            while (true)
            {
                Customer thief = FindThief();
                if (thief == null)
                {
                    if (Vector3.Distance(transform.position, Post) > 0.6f) yield return WalkTo(Post, 0.3f);
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                // Бежим наперерез, пока вор не ушёл или не оказался рядом.
                float t = 0f;
                while (thief != null && thief.IsEscapingThief && t < 6f &&
                       Vector3.Distance(transform.position, thief.transform.position) > CatchRadius)
                {
                    if (agent.isOnNavMesh) agent.SetDestination(thief.transform.position);
                    t += 0.15f;
                    yield return new WaitForSeconds(0.15f);
                }
                if (agent.isOnNavMesh) agent.ResetPath();

                if (thief != null && thief.IsEscapingThief)
                {
                    tried.Add(thief);
                    FaceTowards(thief.transform.position);
                    SetAction(true); // анимация задержания (если положена guard_action)
                    yield return new WaitForSeconds(0.6f);
                    if (thief != null && Random.value < SkillManager.GuardCatchChance) thief.Interact(null);
                    else GameEvents.Notify("Вор вырвался от охранника! Задержите его сами — E");
                    yield return new WaitForSeconds(0.6f);
                    SetAction(false);
                }
            }
        }

        private Customer FindThief()
        {
            foreach (Customer c in FindObjectsByType<Customer>(FindObjectsSortMode.None))
            {
                if (c == null || !c.IsEscapingThief || tried.Contains(c)) continue;
                if (Vector3.Distance(c.transform.position, transform.position) <= NoticeRadius) return c;
            }
            tried.RemoveWhere(c => c == null);
            return null;
        }
    }
}
