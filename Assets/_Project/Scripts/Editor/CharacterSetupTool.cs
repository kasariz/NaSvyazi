using System.Collections.Generic;
using System.IO;
using System.Linq;
using NaSvyazi.Customers;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NaSvyazi.EditorTools
{
    /// <summary>
    /// Подключение анимированных персонажей (например, Quaternius Universal Base Characters
    /// + Universal Animation Library, лицензия CC0) вместо капсул.
    ///
    /// Что делает команда:
    /// 1) переводит FBX-модели и анимации в тип Humanoid (общий скелет — анимации подходят ко всем моделям);
    /// 2) находит клипы ожидания (Idle) и ходьбы (Walk), включает им зацикливание;
    /// 3) создаёт Animator Controller с параметром Speed (ожидание ↔ ходьба);
    /// 4) собирает префаб покупателя для каждой модели и передаёт их генератору покупателей.
    /// </summary>
    public static class CharacterSetupTool
    {
        private const string CharactersFolder = "Assets/_Project/Art/Characters";
        private const string AnimationsFolder = "Assets/_Project/Art/Animations";
        private const string ControllerPath = "Assets/_Project/Art/CustomerAnimator.controller";
        private const string VariantsFolder = "Assets/_Project/Prefabs/Customers";
        private const float TargetHeight = 1.72f;

        [MenuItem("На связи/Подключить персонажей (Quaternius)", priority = 14)]
        private static void SetupCharacters()
        {
            EnsureFolder(CharactersFolder);
            EnsureFolder(AnimationsFolder);

            List<string> modelPaths = FindModels(CharactersFolder);
            List<string> animationPaths = FindModels(AnimationsFolder);

            if (modelPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("Персонажи не найдены",
                    $"Положите FBX-модели персонажей в папку\n{CharactersFolder},\n" +
                    $"а FBX с анимациями — в папку\n{AnimationsFolder}\nи запустите команду снова.", "Понятно");
                return;
            }

            // 1. Тип анимации Humanoid для всех моделей и анимаций.
            foreach (string path in modelPaths.Concat(animationPaths))
                MakeHumanoid(path);

            // 2. Клипы ожидания и ходьбы (сначала ищем в библиотеке анимаций, затем в самих моделях).
            List<string> clipSources = animationPaths.Concat(modelPaths).ToList();
            AnimationClip idle = FindClip(clipSources, "idle");
            AnimationClip walk = FindClip(clipSources, "walk");
            if (idle == null || walk == null)
            {
                EditorUtility.DisplayDialog("Анимации не найдены",
                    "Не удалось найти клипы Idle и Walk. Проверьте, что FBX библиотеки анимаций лежат в папке\n" +
                    AnimationsFolder, "Понятно");
                return;
            }

            SetLooping(idle);
            SetLooping(walk);
            idle = ReloadClip(idle);
            walk = ReloadClip(walk);

            // 3. Аниматор.
            AnimatorController controller = CreateController(idle, walk);

            // 4. Префабы покупателей.
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Customer.prefab");
            if (basePrefab == null)
            {
                Debug.LogError("[На связи] Нет базового префаба покупателя — выполните итерацию 4.");
                return;
            }

            EnsureFolder(VariantsFolder);
            List<Customer> variants = new List<Customer>();
            foreach (string path in modelPaths)
            {
                Customer variant = BuildVariant(basePrefab, path, controller);
                if (variant != null) variants.Add(variant);
            }

            if (variants.Count == 0)
            {
                Debug.LogError("[На связи] Ни одна модель не подошла: нужен скелет, который Unity распознаёт как Humanoid.");
                return;
            }

            CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>();
            if (spawner != null)
            {
                SerializedObject so = new SerializedObject(spawner);
                SerializedProperty list = so.FindProperty("customerPrefabs");
                list.arraySize = variants.Count;
                for (int i = 0; i < variants.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = variants[i];
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            }

            Debug.Log($"[На связи] Персонажи подключены: вариантов внешности — {variants.Count}. " +
                      $"Анимации: «{idle.name}», «{walk.name}». Сохраните сцену (Ctrl+S).");
        }

        // ----------------------------------------------------------------

        private static List<string> FindModels(string folder)
        {
            return AssetDatabase.FindAssets("t:Model", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)
                            || p.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)
                            || p.EndsWith(".gltf", System.StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();
        }

        private static void MakeHumanoid(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || importer.animationType == ModelImporterAnimationType.Human) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Поиск клипа по ключевому слову. Предпочтение — самому «простому» имени
        /// (например, «Idle» лучше, чем «Idle_Gun» или «Crouch_Idle»).
        /// </summary>
        private static AnimationClip FindClip(IEnumerable<string> paths, string keyword)
        {
            string[] avoid = { "gun", "rifle", "pistol", "sword", "crouch", "carry", "back", "left", "right",
                               "injured", "talk", "sit", "swim", "jump", "attack", "dead", "death", "torch" };

            AnimationClip best = null;
            int bestScore = int.MaxValue;

            foreach (string path in paths)
            {
                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    string name = clip.name.ToLowerInvariant();
                    if (name.StartsWith("__preview__") || !name.Contains(keyword)) continue;

                    int score = name.Length;
                    foreach (string bad in avoid)
                        if (name.Contains(bad)) score += 100;

                    if (score < bestScore)
                    {
                        best = clip;
                        bestScore = score;
                    }
                }
            }
            return best;
        }

        private static void SetLooping(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

            bool changed = false;
            foreach (ModelImporterClipAnimation c in clips)
            {
                if (c.name == clip.name && !c.loopTime)
                {
                    c.loopTime = true;
                    c.lockRootRotation = true;
                    c.lockRootHeightY = true;
                    c.lockRootPositionXZ = true;
                    changed = true;
                }
            }

            if (!changed) return;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static AnimationClip ReloadClip(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            string name = clip.name;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == name) ?? clip;
        }

        private static AnimatorController CreateController(AnimationClip idle, AnimationClip walk)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idleState = sm.AddState("Idle");
            idleState.motion = idle;
            AnimatorState walkState = sm.AddState("Walk");
            walkState.motion = walk;
            sm.defaultState = idleState;

            AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");

            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.2f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>Префаб покупателя: базовый префаб, у которого капсула заменена моделью.</summary>
        private static Customer BuildVariant(GameObject basePrefab, string modelPath, AnimatorController controller)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (model == null || avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogWarning($"[На связи] Пропущена модель {Path.GetFileName(modelPath)}: не Humanoid.");
                return null;
            }
            if (model.GetComponentInChildren<SkinnedMeshRenderer>() == null) return null;

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Customer_" + Path.GetFileNameWithoutExtension(modelPath);

            // Убираем капсулу и «козырёк».
            foreach (string part in new[] { "Body", "Visor" })
            {
                Transform t = root.transform.Find(part);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.name = "Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // Подгоняем рост под ~1.72 м.
            Bounds bounds = CalculateBounds(instance);
            if (bounds.size.y > 0.01f)
                instance.transform.localScale = Vector3.one * (TargetHeight / bounds.size.y);

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Реплика над головой — чуть выше модели.
            Transform bubble = root.transform.Find("Bubble");
            if (bubble != null) bubble.localPosition = new Vector3(0f, TargetHeight + 0.3f, 0f);

            Customer customer = root.GetComponent<Customer>();
            SerializedObject so = new SerializedObject(customer);
            so.FindProperty("bodyRenderer").objectReferenceValue = null;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = $"{VariantsFolder}/{root.name}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved != null ? saved.GetComponent<Customer>() : null;
        }

        private static Bounds CalculateBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
