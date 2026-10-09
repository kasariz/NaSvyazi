using UnityEngine;

namespace NaSvyazi.Core
{
    /// <summary>
    /// Настройки игрока (громкость, чувствительность мыши, полноэкранный режим).
    /// Хранятся в PlayerPrefs и применяются при запуске.
    /// </summary>
    public static class GameSettings
    {
        private const string VolumeKey = "settings.volume";
        private const string SensitivityKey = "settings.sensitivity";
        private const string FullscreenKey = "settings.fullscreen";

        private static bool loaded;
        private static float volume;
        private static float sensitivity;
        private static bool fullscreen;

        private static void EnsureLoaded()
        {
            if (loaded) return;
            volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            fullscreen = PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
            loaded = true;
        }

        /// <summary>Общая громкость 0…1.</summary>
        public static float MasterVolume
        {
            get { EnsureLoaded(); return volume; }
            set
            {
                EnsureLoaded();
                volume = Mathf.Clamp01(value);
                AudioListener.volume = volume;
                PlayerPrefs.SetFloat(VolumeKey, volume);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Множитель чувствительности мыши 0.2…3.</summary>
        public static float MouseSensitivity
        {
            get { EnsureLoaded(); return sensitivity; }
            set
            {
                EnsureLoaded();
                sensitivity = Mathf.Clamp(value, 0.2f, 3f);
                PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
                PlayerPrefs.Save();
            }
        }

        public static bool Fullscreen
        {
            get { EnsureLoaded(); return fullscreen; }
            set
            {
                EnsureLoaded();
                fullscreen = value;
                Screen.fullScreen = value;
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Применить сохранённые настройки при запуске игры.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnStart()
        {
            loaded = false;
            AudioListener.volume = MasterVolume;
        }
    }
}
