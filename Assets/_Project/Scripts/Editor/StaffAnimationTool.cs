using System.IO;
using UnityEditor;
using UnityEngine;

namespace NaSvyazi.EditorTools
{
    /// <summary>
    /// Подготовка анимаций персонала (например, из Mixamo — «Without Skin», формат FBX).
    /// Файлы кладутся в Assets/_Project/Resources/Animations/Staff с именами:
    ///   merch_idle, merch_walk            — выкладчик с коробкой в руках;
    ///   cleaner_idle, cleaner_walk, cleaner_action — уборщица (action — мытьё пола);
    ///   guard_idle, guard_action          — охранник (action — задержание, удар).
    /// Команда переводит их в Humanoid (тогда они подходят ко всем моделям покупателей)
    /// и зацикливает ожидание, ходьбу и мытьё.
    /// </summary>
    public static class StaffAnimationTool
    {
        private const string Folder = "Assets/_Project/Resources/Animations/Staff";

        [MenuItem("На связи/Подготовить анимации персонала", priority = 15)]
        private static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;

                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                bool loop = name.EndsWith("_idle") || name.EndsWith("_walk") || name.StartsWith("cleaner_action");

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;

                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation c in clips)
                {
                    c.loopTime = loop;
                    c.lockRootRotation = true;
                    c.lockRootHeightY = true;
                    c.lockRootPositionXZ = true; // сотрудник двигается навигацией, а не анимацией
                    c.keepOriginalOrientation = true;
                    c.keepOriginalPositionY = true;
                    c.keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                count++;
            }

            EditorUtility.DisplayDialog("Анимации персонала",
                count > 0
                    ? $"Подготовлено файлов: {count}. Запустите игру — сотрудники подхватят анимации сами."
                    : $"Положите FBX-анимации в\n{Folder}\nс именами merch_idle, merch_walk, cleaner_idle, " +
                      "cleaner_walk, cleaner_action, guard_idle, guard_action.", "OK");
        }
    }
}
