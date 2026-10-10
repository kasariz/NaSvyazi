using System.Collections;
using System.Collections.Generic;
using NaSvyazi.Products;
using NaSvyazi.Progression;
using NaSvyazi.Store;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Выкладчик товара. Следит за полками зала: когда на секции остаётся меньше половины товара
    /// (или она опустела), берёт этот же товар из коробки и докладывает. Коробки в зале берёт
    /// сам, за коробками на складе или в зоне доставки уходит через дверь склада.
    /// Пустые секции, которые ещё ни разу не заполнялись, не трогает — что где лежит, решает игрок.
    /// Навык «Расторопный выкладчик»: быстрее ходит и выкладывает, уносит больше за раз.
    /// </summary>
    public class Merchandiser : StaffWorker
    {
        private const float RestockThreshold = 0.5f;

        protected override string Title => "Выкладчик";
        protected override float SpeedMultiplier => SkillManager.MerchandiserSpeedMultiplier;
        private int CarryLimit => 6 + 3 * SkillManager.Rank("merch_speed");

        private GameObject carryProp;

        protected override IEnumerator Work()
        {
            carryProp = PrimitiveProp(PrimitiveType.Cube, new Vector3(0.38f, 0.26f, 0.3f), new Color(0.78f, 0.62f, 0.43f));
            AttachProp(carryProp, new Vector3(0f, 1.05f, 0.35f), Vector3.zero);
            carryProp.SetActive(false);

            while (true)
            {
                if (!FindJob(out ShelfSection shelf, out ProductBox box, out ProductData product))
                {
                    yield return WalkTo(StorageDoorHall + new Vector3(1.2f, 0f, -0.8f), 0.6f);
                    yield return new WaitForSeconds(1.5f);
                    continue;
                }

                // За товаром: в зале — к коробке, иначе — через дверь склада.
                if (InSalon(box.transform.position)) yield return WalkTo(box.transform.position, 0.9f);
                else yield return TripToStorage(Vector3.Distance(StorageDoorHall, box.transform.position));

                int carried = 0;
                if (box != null && !box.IsHeld)
                {
                    int need = shelf != null ? shelf.Capacity - shelf.Count : 0;
                    int take = Mathf.Min(need, CarryLimit);
                    for (int i = 0; i < take; i++)
                    {
                        if (!box.TryTakeOne(out ProductData taken) || taken != product) break;
                        carried++;
                    }
                }
                if (carried == 0) { yield return new WaitForSeconds(0.5f); continue; }

                carryProp.SetActive(true);
                if (shelf != null) yield return WalkTo(shelf.CustomerStandPoint, 0.3f);

                // Выкладка по одной штуке.
                while (carried > 0 && shelf != null && shelf.CanPlace(product))
                {
                    FaceTowards(shelf.transform.position);
                    shelf.TryPlace(product);
                    Audio.SoundManager.Play(Audio.Sfx.Place, 0.25f);
                    carried--;
                    yield return new WaitForSeconds(0.45f / SpeedMultiplier);
                }

                // Остаток (игрок успел заполнить полку) — на другую секцию с тем же товаром или обратно в коробку.
                if (carried > 0) carried = PlaceElsewhere(product, carried);
                if (carried > 0) carried = ReturnToBox(product, carried);
                carryProp.SetActive(false);
                yield return new WaitForSeconds(0.3f);
            }
        }

        /// <summary>Самая пустая секция зала с известным товаром и ближайшая коробка с этим товаром.</summary>
        private bool FindJob(out ShelfSection shelf, out ProductBox box, out ProductData product)
        {
            shelf = null; box = null; product = null;
            float worst = RestockThreshold;

            ProductBox[] boxes = FindObjectsByType<ProductBox>(FindObjectsSortMode.None);
            foreach (ShelfSection s in ShelfSection.All)
            {
                if (s == null || !s.isActiveAndEnabled || s.Capacity == 0) continue;
                ProductData p = s.Product != null ? s.Product : s.LastProduct;
                if (p == null) continue;

                float fill = (float)s.Count / s.Capacity;
                if (fill >= worst && !s.IsEmpty) continue;
                if (!s.CanPlace(p)) continue;

                ProductBox source = NearestBox(boxes, p, s.transform.position);
                if (source == null) continue;

                worst = fill;
                shelf = s; box = source; product = p;
                if (fill <= 0f) break; // пустая — в первую очередь
            }
            return shelf != null;
        }

        private static ProductBox NearestBox(ProductBox[] boxes, ProductData p, Vector3 from)
        {
            ProductBox best = null;
            float bestD = float.MaxValue;
            foreach (ProductBox b in boxes)
            {
                if (b == null || b.IsHeld || b.IsEmpty || b.Product != p) continue;
                float d = (b.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        private static int PlaceElsewhere(ProductData p, int count)
        {
            foreach (ShelfSection s in ShelfSection.All)
                while (count > 0 && s != null && s.isActiveAndEnabled && s.Product == p && s.CanPlace(p) && s.TryPlace(p))
                    count--;
            return count;
        }

        private static int ReturnToBox(ProductData p, int count)
        {
            foreach (ProductBox b in FindObjectsByType<ProductBox>(FindObjectsSortMode.None))
                while (count > 0 && b != null && !b.IsHeld && b.CanAccept(p) && b.TryPutOne(p))
                    count--;
            return count;
        }
    }
}
