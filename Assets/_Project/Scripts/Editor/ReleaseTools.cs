using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NaSvyazi.EditorTools
{
    /// <summary>
    /// Подготовка релиза: настройки проекта (название, версия, иконка, режим окна)
    /// и сборка для Windows в один клик — папка с игрой и готовый zip-архив для загрузки в магазин.
    /// </summary>
    public static class ReleaseTools
    {
        /// <summary>Версия релиза. Перед следующим релизом увеличьте (0.1.1, 0.2.0 …).</summary>
        public const string Version = "0.3.0";

        private const string ProductName = "На связи";
        private const string CompanyName = "NaSvyazi";
        private const string ExeName = "NaSvyazi";
        private const string IconPath = "Assets/_Project/Textures/T_AppIcon.png";
        private const string BuildsFolder = "Builds";

        [MenuItem("На связи/Релиз/1. Настроить проект для релиза", priority = 40)]
        private static void ConfigureMenu()
        {
            Configure();
            Debug.Log($"[Релиз] Проект настроен: «{ProductName}» {Version}, иконка, полноэкранный режим. " +
                      "Теперь — «На связи → Релиз → 2. Собрать для Windows».");
        }

        [MenuItem("На связи/Релиз/2. Собрать для Windows", priority = 41)]
        private static void BuildWindows()
        {
            if (!EditorUtility.DisplayDialog("Сборка релиза",
                    $"Собрать «{ProductName}» {Version} для Windows (64 бит)? Это займёт несколько минут.",
                    "Собрать", "Отмена"))
                return;

            Configure();

            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Релиз] В списке сцен сборки пусто — выполните итерацию 7.");
                return;
            }

            string name = $"NaSvyazi_{Version}_Windows";
            string folder = Path.Combine(BuildsFolder, name);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, ExeName + ".exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Релиз] Сборка не удалась: {report.summary.result}. Подробности — выше в консоли.");
                return;
            }

            // Служебные папки Unity не нужны игрокам («DoNotShip» / «DontShip»).
            foreach (string dir in Directory.GetDirectories(folder))
                if (dir.Contains("DoNotShip") || dir.Contains("DontShip"))
                    Directory.Delete(dir, true);

            string zip = Path.Combine(BuildsFolder, name + ".zip");
            if (TryZip(folder, zip))
            {
                long mb = new FileInfo(zip).Length / (1024 * 1024);
                Debug.Log($"[Релиз] Готово: {Path.GetFullPath(zip)} ({mb} МБ). Внутри — {ExeName}.exe и папка с данными.");
                EditorUtility.RevealInFinder(zip);
            }
            else
            {
                Debug.Log($"[Релиз] Сборка готова: {Path.GetFullPath(folder)}. Упакуйте папку в zip вручную " +
                          "(ПКМ → Отправить → Сжатая ZIP-папка).");
                EditorUtility.RevealInFinder(folder);
            }
        }

        /// <summary>Упаковать папку в zip средствами Windows (PowerShell Compress-Archive).</summary>
        private static bool TryZip(string folder, string zip)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) return false;
            try
            {
                if (File.Exists(zip)) File.Delete(zip);
                string source = Path.GetFullPath(folder).Replace("'", "''");
                string target = Path.GetFullPath(zip).Replace("'", "''");

                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"Compress-Archive -Path '{source}\\*' -DestinationPath '{target}' -Force\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(info))
                {
                    EditorUtility.DisplayProgressBar("Сборка релиза", "Упаковка в zip…", 0.9f);
                    process.WaitForExit();
                }
                return File.Exists(zip);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Релиз] Не удалось упаковать в zip: {e.Message}");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Настройки проекта для релизной сборки.</summary>
        private static void Configure()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.bundleVersion = Version;

            // Окно: полноэкранный режим без рамки, можно переключить в настройках игры.
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.visibleInBackground = false;
            PlayerSettings.forceSingleInstance = true;

            // Mono не требует установки компилятора C++ — сборка работает «из коробки».
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            // Тёмный фон заставки в цвет интерфейса.
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.06f, 0.07f, 0.09f);

            Texture2D icon = CreateIcon();
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            AssetDatabase.SaveAssets();
        }

        private static string[] EnabledScenes()
        {
            List<string> list = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.enabled && File.Exists(s.path)) list.Add(s.path);
            return list.ToArray();
        }

        /// <summary>
        /// Иконка игры: розовый квадрат со скруглёнными углами и белыми «делениями сигнала» —
        /// символ связи. Рисуется кодом и сохраняется в PNG 512×512.
        /// </summary>
        private static Texture2D CreateIcon()
        {
            if (!File.Exists(IconPath))
            {
                const int size = 512, radius = 110;
                Color background = new Color(0.86f, 0.18f, 0.55f, 1f);
                Color bar = Color.white;
                Color clear = new Color(0f, 0f, 0f, 0f);

                // Четыре столбика сигнала, растущие слева направо.
                int[] heights = { 110, 175, 240, 305 };
                const int barWidth = 62, gap = 30, baseline = 112;
                int startX = (size - (heights.Length * barWidth + (heights.Length - 1) * gap)) / 2;

                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        if (!InsideRoundedSquare(x, y, size, radius))
                        {
                            tex.SetPixel(x, y, clear);
                            continue;
                        }

                        Color c = background;
                        for (int i = 0; i < heights.Length; i++)
                        {
                            int x0 = startX + i * (barWidth + gap);
                            if (x >= x0 && x < x0 + barWidth && y >= baseline && y < baseline + heights[i])
                                c = bar;
                        }
                        tex.SetPixel(x, y, c);
                    }

                Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
                File.WriteAllBytes(IconPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(IconPath);

                TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
                if (importer != null)
                {
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        private static bool InsideRoundedSquare(int x, int y, int size, int radius)
        {
            int cx = Mathf.Clamp(x, radius, size - 1 - radius);
            int cy = Mathf.Clamp(y, radius, size - 1 - radius);
            int dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
