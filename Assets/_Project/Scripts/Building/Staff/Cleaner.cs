using System.Collections;
using NaSvyazi.Progression;
using NaSvyazi.Store;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Уборщица: ходит со шваброй к ближайшему пятну грязи и отмывает его.
    /// Швабра держится как настоящая: руки у пояса справа, черенок наклонён вперёд-вниз,
    /// насадка на полу перед уборщицей; при мытье насадка ходит из стороны в сторону.
    /// Навык «Чистота — залог успеха» ускоряет уборку.
    /// </summary>
    public class Cleaner : StaffWorker
    {
        private const float StickLength = 1.35f;

        protected override string Title => "Уборщица";
        protected override string AnimationPrefix => "cleaner";
        protected override float SpeedMultiplier => SkillManager.CleanerSpeedMultiplier;

        private Transform stick, head;
        private float swayPhase;
        private bool scrubbing;

        protected override IEnumerator Work()
        {
            BuildMop();

            while (true)
            {
                DirtSpot spot = CleaningManager.Instance != null ? CleaningManager.Instance.Nearest(transform.position) : null;
                if (spot == null)
                {
                    yield return WalkTo(StorageDoorHall + new Vector3(0.9f, 0f, 0.6f), 0.6f);
                    yield return new WaitForSeconds(2f);
                    continue;
                }

                yield return WalkTo(spot.transform.position, 0.75f);
                scrubbing = true;
                SetAction(true);
                while (spot != null && !spot.IsCleaned)
                {
                    FaceTowards(spot.transform.position);
                    if (spot.Stroke()) break;
                    yield return new WaitForSeconds(0.45f / SpeedMultiplier);
                }
                scrubbing = false;
                SetAction(false);
                yield return new WaitForSeconds(0.3f);
            }
        }

        protected override void Update()
        {
            base.Update();
            if (stick == null) return;

            // Хват у правого бока на уровне пояса, насадка на полу впереди.
            if (scrubbing) swayPhase += Time.deltaTime * 11f * SpeedMultiplier;
            float sway = scrubbing ? Mathf.Sin(swayPhase) * 0.28f : 0f;

            Vector3 grip = transform.TransformPoint(new Vector3(0.22f, 0.95f, 0.22f));
            Vector3 floor = transform.TransformPoint(new Vector3(0.05f + sway, 0.03f, 0.85f));
            Vector3 dir = (grip - floor).normalized;

            // Черенок от насадки вверх, проходит через руку и чуть выступает над ней.
            Vector3 top = floor + dir * StickLength;
            stick.position = (floor + top) * 0.5f;
            stick.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            stick.localScale = new Vector3(0.03f, StickLength * 0.5f, 0.03f);

            head.position = floor;
            head.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
        }

        private void BuildMop()
        {
            stick = PrimitiveProp(PrimitiveType.Cylinder, Vector3.one, new Color(0.86f, 0.18f, 0.55f)).transform;
            head = PrimitiveProp(PrimitiveType.Cube, new Vector3(0.36f, 0.06f, 0.13f), new Color(0.82f, 0.82f, 0.78f)).transform;
            foreach (Transform t in new[] { stick, head })
            {
                t.SetParent(transform, true);
                Destroy(t.GetComponent<Collider>());
            }
        }
    }
}
