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
        private const string MusicKey = "settings.music";
        private const string QualityKey = "settings.quality";
        private const string VSyncKey = "settings.vsync";
        private const string ResolutionWidthKey = "settings.resWidth";
        private const string ResolutionHeightKey = "settings.resHeight";

        private static bool loaded;
        private static float volume;
        private static float sensitivity;
        private static bool fullscreen;
        private static float music;

        private static void EnsureLoaded()
        {
            if (loaded) return;
            volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            fullscreen = PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
            music = PlayerPrefs.GetFloat(MusicKey, 0.35f);
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

        /// <summary>Громкость музыки 0…1.</summary>
        public static float MusicVolume
        {
            get { EnsureLoaded(); return music; }
            set
            {
                EnsureLoaded();
                music = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MusicKey, music);
                PlayerPrefs.Save();
                Audio.SoundManager.SetMusicVolume(music);
            }
        }

        /// <summary>Уровень качества графики (индекс в QualitySettings.names).</summary>
        public static int QualityLevel
        {
            get => PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());
            set
            {
                int level = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
                QualitySettings.SetQualityLevel(level, true);
                QualitySettings.vSyncCount = VSync ? 1 : 0; // смена уровня сбрасывает vSync
                PlayerPrefs.SetInt(QualityKey, level);
                PlayerPrefs.Save();
            }
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt(VSyncKey, 1) == 1;
            set
            {
                QualitySettings.vSyncCount = value ? 1 : 0;
                PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Установить и запомнить разрешение экрана.</summary>
        public static void SetResolution(int width, int height)
        {
            Screen.SetResolution(width, height, Fullscreen);
            PlayerPrefs.SetInt(ResolutionWidthKey, width);
            PlayerPrefs.SetInt(ResolutionHeightKey, height);
            PlayerPrefs.Save();
        }

        /// <summary>Применить сохранённые настройки при запуске игры.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnStart()
        {
            loaded = false;
            AudioListener.volume = MasterVolume;

            if (PlayerPrefs.HasKey(QualityKey)) QualitySettings.SetQualityLevel(QualityLevel, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;

            int w = PlayerPrefs.GetInt(ResolutionWidthKey, 0);
            int h = PlayerPrefs.GetInt(ResolutionHeightKey, 0);
            if (w > 0 && h > 0 && !Application.isEditor) Screen.SetResolution(w, h, Fullscreen);
        }
    }
}
