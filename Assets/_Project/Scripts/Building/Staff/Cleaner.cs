using System.Collections;
using NaSvyazi.Progression;
using NaSvyazi.Store;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Уборщица: ходит со шваброй к ближайшему пятну грязи и отмывает его.
    /// Навык «Чистота — залог успеха» ускоряет уборку.
    /// </summary>
    public class Cleaner : StaffWorker
    {
        protected override string Title => "Уборщица";
        protected override float SpeedMultiplier => SkillManager.CleanerSpeedMultiplier;

        private Transform mop;
        private float swayPhase;
        private bool scrubbing;

        protected override IEnumerator Work()
        {
            mop = BuildMop();

            while (true)
            {
                DirtSpot spot = CleaningManager.Instance != null ? CleaningManager.Instance.Nearest(transform.position) : null;
                if (spot == null)
                {
                    yield return WalkTo(StorageDoorHall + new Vector3(0.9f, 0f, 0.6f), 0.6f);
                    yield return new WaitForSeconds(2f);
                    continue;
                }

                yield return WalkTo(spot.transform.position, 0.7f);
                scrubbing = true;
                while (spot != null && !spot.IsCleaned)
                {
                    FaceTowards(spot.transform.position);
                    if (spot.Stroke()) break;
                    yield return new WaitForSeconds(0.45f / SpeedMultiplier);
                }
                scrubbing = false;
                yield return new WaitForSeconds(0.3f);
            }
        }

        protected override void Update()
        {
            base.Update();
            if (mop == null) return;
            // Во время уборки швабра ходит из стороны в сторону.
            swayPhase += Time.deltaTime * (scrubbing ? 12f : 0f);
            float sway = scrubbing ? Mathf.Sin(swayPhase) * 25f : 0f;
            mop.localRotation = Quaternion.Euler(30f, sway, 0f);
        }

        private Transform BuildMop()
        {
            GameObject root = new GameObject("Mop");
            GameObject stick = PrimitiveProp(PrimitiveType.Cylinder, new Vector3(0.03f, 0.65f, 0.03f), new Color(0.86f, 0.18f, 0.55f));
            stick.transform.SetParent(root.transform, false);
            stick.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            GameObject head = PrimitiveProp(PrimitiveType.Cube, new Vector3(0.34f, 0.06f, 0.12f), new Color(0.82f, 0.82f, 0.78f));
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, -0.85f, 0f);
            return AttachProp(root, new Vector3(0.25f, 1.0f, 0.4f), new Vector3(30f, 0f, 0f));
        }
    }
}
