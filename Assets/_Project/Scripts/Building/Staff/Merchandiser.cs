using System.Collections;
using NaSvyazi.Audio;
using NaSvyazi.Products;
using NaSvyazi.Progression;
using NaSvyazi.Store;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Выкладчик товара. Следит за полками зала: когда на секции остаётся меньше половины товара
    /// (или она опустела), идёт за коробкой с этим же товаром — в зал, на склад или в зону доставки
    /// (через дверь склада, пешком), — несёт её к полке и выкладывает.
    /// Опустевшую коробку сразу выбрасывает; если в коробке что-то осталось — относит её на свободное
    /// место складского стеллажа (или на свободный участок пола склада).
    /// Пустые секции, которые ещё ни разу не заполнялись, не трогает — что где лежит, решает игрок.
    /// Навык «Расторопный выкладчик»: быстрее ходит и выкладывает.
    /// </summary>
    public class Merchandiser : StaffWorker
    {
        private const float RestockThreshold = 0.5f;
        private static readonly Vector3 BoxHalfSize = new Vector3(0.21f, 0.16f, 0.16f);

        protected override string Title => "Выкладчик";
        protected override string AnimationPrefix => "merch";
        protected override float SpeedMultiplier => SkillManager.MerchandiserSpeedMultiplier;

        private ProductBox carried;

        protected override IEnumerator Work()
        {
            while (true)
            {
                if (!FindJob(out ShelfSection shelf, out ProductBox box, out ProductData product))
                {
                    yield return WalkTo(StorageDoorHall + new Vector3(1.2f, 0f, -0.8f), 0.6f);
                    yield return new WaitForSeconds(1.5f);
                    continue;
                }

                // 1. Дойти до коробки и взять её в руки.
                yield return WalkTo(box.transform.position, 0.9f);
                if (box == null || box.IsHeld || box.IsEmpty) continue;
                FaceTowards(box.transform.position);
                box.OnPickedUp(Hands);
                carried = box;
                SoundManager.Play(Sfx.Pickup, 0.3f);
                yield return new WaitForSeconds(0.3f / SpeedMultiplier);

                // 2. Отнести к полке и выложить.
                if (shelf != null) yield return WalkTo(shelf.CustomerStandPoint, 0.3f);
                while (carried != null && !carried.IsEmpty && shelf != null && shelf.CanPlace(product))
                {
                    FaceTowards(shelf.transform.position);
                    if (!carried.TryTakeOne(out ProductData item)) break;
                    shelf.TryPlace(item);
                    SoundManager.Play(Sfx.Place, 0.25f);
                    yield return new WaitForSeconds(0.45f / SpeedMultiplier);
                }

                // 3. Пустую коробку — сразу в утиль, непустую — на склад.
                if (carried != null && carried.IsEmpty)
                {
                    Destroy(carried.gameObject);
                    carried = null;
                }
                else if (carried != null)
                {
                    yield return StoreBox();
                }
                yield return new WaitForSeconds(0.3f);
            }
        }

        /// <summary>Отнести коробку на свободное место стеллажа склада, иначе — на свободный участок пола склада.</summary>
        private IEnumerator StoreBox()
        {
            StorageRack rack = FreeRack();
            if (rack != null)
            {
                yield return WalkTo(rack.transform.position, 1.1f);
                if (carried != null && rack.FreeSlots > 0 && rack.TryStore(carried))
                {
                    SoundManager.Play(Sfx.Pickup, 0.3f);
                    carried = null;
                    yield break;
                }
            }

            Vector3 spot = FreeFloorSpot();
            yield return WalkTo(spot, 0.8f);
            if (carried == null) yield break;
            carried.transform.SetParent(null, true);
            carried.transform.SetPositionAndRotation(spot + Vector3.up * (BoxHalfSize.y + 0.02f), Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            carried.OnDropped(Vector3.zero);
            SoundManager.Play(Sfx.Pickup, 0.3f);
            carried = null;
        }

        private static StorageRack FreeRack()
        {
            foreach (StorageRack r in StorageRack.All)
                if (r != null && r.isActiveAndEnabled && r.GetComponent<HandCart>() == null &&
                    InStorage(r.transform.position) && r.FreeSlots > 0)
                    return r;
            return null;
        }

        /// <summary>Свободный участок пола в открытой части склада (сетка 0,6 м).</summary>
        private static Vector3 FreeFloorSpot()
        {
            int stage = ExpansionManager.Instance != null ? ExpansionManager.Instance.StorageStage : 0;
            for (int i = 0; i <= stage && i < LayoutPlan.Storage.Length; i++)
                foreach (Rect a in LayoutPlan.Storage[i].areas)
                    for (float z = a.yMax - 0.5f; z > a.yMin + 0.4f; z -= 0.6f)
                        for (float x = a.xMin + 0.5f; x < a.xMax - 0.4f; x += 0.6f)
                        {
                            Vector3 c = new Vector3(x, BoxHalfSize.y + 0.02f, z);
                            if (!Physics.CheckBox(c, BoxHalfSize, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                                return new Vector3(x, 0f, z);
                        }
            return StorageDoorHall + new Vector3(-1.5f, 0f, 0f);
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
                if (fill >= worst) continue;
                if (!s.CanPlace(p)) continue;

                ProductBox source = NearestBox(boxes, p, transform.position);
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
                if (b.GetComponentInParent<HandCart>() != null) continue; // с тележки игрока не берём
                float d = (b.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        /// <summary>Уволили с коробкой в руках — коробка остаётся на полу.</summary>
        private void OnDestroy()
        {
            if (!gameObject.scene.isLoaded) return; // выгрузка сцены — ничего не делаем
            if (carried != null && carried.IsHeld)
            {
                carried.transform.SetParent(null, true);
                carried.OnDropped(Vector3.zero);
            }
        }
    }
}
