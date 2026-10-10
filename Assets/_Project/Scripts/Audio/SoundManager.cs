using System.Collections.Generic;
using UnityEngine;

namespace NaSvyazi.Audio
{
    /// <summary>Звуковые эффекты игры.</summary>
    public enum Sfx
    {
        Scan,     // писк сканера штрихкодов
        Purchase, // звон кассы при оплате
        Notify,   // мягкий сигнал уведомления
        Place,    // товар поставлен на полку
        Pickup,   // взяли/бросили коробку
        Click,    // нажатие кнопки интерфейса
        DoorBell, // звонок входной двери (пришёл покупатель)
        Error     // ошибка / недостаточно денег
    }

    /// <summary>
    /// Звуковая система. Все звуки синтезируются при запуске из простых волн
    /// (синус, треугольник, шум) с огибающими — внешние аудиофайлы не нужны,
    /// поэтому нет вопросов с лицензиями, а размер сборки не растёт.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        private const int SampleRate = 44100;

        [Range(0f, 1f)] [SerializeField] private float effectsVolume = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float footstepsVolume = 0.25f;

        [Tooltip("Фоновая музыка (синтезируется кодом)")]
        [SerializeField] private bool playMusic = true;

        private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        private AudioSource effects;
        private AudioSource steps;
        private AudioSource music;
        private AudioClip stepClip;
        private System.Random noise;

        private void Awake()
        {
            Instance = this;
            noise = new System.Random(12345);

            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            effects.spatialBlend = 0f;

            steps = gameObject.AddComponent<AudioSource>();
            steps.playOnAwake = false;
            steps.spatialBlend = 0f;

            GenerateClips();

            if (playMusic)
            {
                music = gameObject.AddComponent<AudioSource>();
                music.playOnAwake = false;
                music.loop = true;
                music.spatialBlend = 0f;
                music.clip = GenerateMusic();
                music.volume = Core.GameSettings.MusicVolume;
                music.Play();
            }
        }

        /// <summary>Изменить громкость музыки (из настроек).</summary>
        public static void SetMusicVolume(float volume)
        {
            if (Instance != null && Instance.music != null) Instance.music.volume = volume;
        }

        /// <summary>
        /// Спокойный эмбиент для торгового зала: четыре аккорда (Cmaj7 – Am7 – Fmaj7 – G6)
        /// мягкими синусоидами с плавной атакой, по 4 секунды; луп длиной 16 секунд.
        /// </summary>
        private static AudioClip GenerateMusic()
        {
            const int rate = 22050;
            const float chordLength = 4f;
            float[][] chords =
            {
                new[] { 130.81f, 164.81f, 196.00f, 246.94f }, // Cmaj7
                new[] { 110.00f, 130.81f, 164.81f, 196.00f }, // Am7
                new[] { 87.31f, 110.00f, 130.81f, 164.81f },  // Fmaj7
                new[] { 98.00f, 123.47f, 146.83f, 164.81f }   // G6
            };

            int perChord = Mathf.RoundToInt(chordLength * rate);
            float[] data = new float[perChord * chords.Length];

            for (int c = 0; c < chords.Length; c++)
            {
                for (int i = 0; i < perChord; i++)
                {
                    float t = (float)i / rate;
                    // Плавная атака и затухание — аккорды перетекают друг в друга без щелчков.
                    float env = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((chordLength - t) / 0.9f);
                    float sample = 0f;
                    foreach (float f in chords[c])
                    {
                        float tremolo = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                        sample += Mathf.Sin(2f * Mathf.PI * f * t) * tremolo
                                  + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 2f * t);
                    }
                    // Бас — основной тон аккорда октавой ниже.
                    sample += 0.8f * Mathf.Sin(2f * Mathf.PI * chords[c][0] * 0.5f * t);
                    data[c * perChord + i] = sample * env * 0.06f;
                }
            }

            AudioClip clip = AudioClip.Create("music_ambient", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDestroy()
        {
            // Если в сцене остался другой менеджер звука (например, при выгрузке меню), переключаемся на него.
            if (Instance == this)
            {
                Instance = null;
                foreach (SoundManager other in FindObjectsByType<SoundManager>(FindObjectsSortMode.None))
                    if (other != this) Instance = other;
            }
        }

        /// <summary>Проиграть эффект (если звуковая система есть на сцене).</summary>
        public static void Play(Sfx sfx, float volume = 1f)
        {
            if (Instance == null) return;
            if (Instance.clips.TryGetValue(sfx, out AudioClip clip))
                Instance.effects.PlayOneShot(clip, volume * Instance.effectsVolume);
        }

        /// <summary>Шаг игрока — каждый раз немного другой высоты, чтобы не звучать механически.</summary>
        public static void PlayStep()
        {
            if (Instance == null || Instance.stepClip == null) return;
            Instance.steps.pitch = Random.Range(0.85f, 1.15f);
            Instance.steps.PlayOneShot(Instance.stepClip, Instance.footstepsVolume);
        }

        // ----------------------------------------------------------------
        // Синтез
        // ----------------------------------------------------------------

        private void GenerateClips()
        {
            // Сканер: короткий высокий «бип».
            clips[Sfx.Scan] = Make("scan", Tone(1850f, 0.09f, Wave.Square, 0.003f, 0.02f, 0.35f));

            // Касса: две «колокольные» ноты ми–ля.
            float[] a = Bell(1318.5f, 0.35f, 0.5f);
            float[] b = Bell(1760f, 0.45f, 0.5f);
            clips[Sfx.Purchase] = Make("purchase", Mix(a, b, 0.07f));

            // Уведомление: мягкий двойной сигнал.
            clips[Sfx.Notify] = Make("notify", Mix(Bell(880f, 0.3f, 0.25f), Bell(1320f, 0.35f, 0.2f), 0.09f));

            // Товар на полку: короткий щелчок с низким призвуком.
            clips[Sfx.Place] = Make("place", Mix(Noise(0.025f, 0.25f), Tone(320f, 0.06f, Wave.Sine, 0.001f, 0.05f, 0.4f), 0f));

            // Коробка: глухой удар.
            clips[Sfx.Pickup] = Make("pickup", Mix(Tone(150f, 0.12f, Wave.Sine, 0.002f, 0.1f, 0.6f), Noise(0.04f, 0.15f), 0f));

            // Кнопка интерфейса.
            clips[Sfx.Click] = Make("click", Tone(1200f, 0.025f, Wave.Triangle, 0.001f, 0.02f, 0.3f));

            // Дверной звонок «дин-дон».
            clips[Sfx.DoorBell] = Make("doorbell", Mix(Bell(987.8f, 0.6f, 0.4f), Bell(784f, 0.8f, 0.4f), 0.35f));

            // Ошибка: два низких сигнала.
            float[] e1 = Tone(220f, 0.12f, Wave.Square, 0.005f, 0.03f, 0.2f);
            clips[Sfx.Error] = Make("error", Mix(e1, e1, 0.16f));

            // Шаг: приглушённый шум.
            stepClip = Make("step", LowPass(Noise(0.07f, 0.35f), 0.08f));
        }

        private enum Wave { Sine, Square, Triangle }

        /// <summary>Тон с огибающей атака–затухание.</summary>
        private static float[] Tone(float freq, float duration, Wave wave, float attack, float release, float volume)
        {
            int n = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float phase = t * freq % 1f;
                float s;
                switch (wave)
                {
                    case Wave.Square: s = phase < 0.5f ? 1f : -1f; break;
                    case Wave.Triangle: s = 4f * Mathf.Abs(phase - 0.5f) - 1f; break;
                    default: s = Mathf.Sin(2f * Mathf.PI * freq * t); break;
                }

                float env = 1f;
                if (t < attack) env = t / attack;
                else if (t > duration - release) env = Mathf.Clamp01((duration - t) / release);
                data[i] = s * env * volume;
            }
            return data;
        }

        /// <summary>«Колокольчик»: синус с обертоном и экспоненциальным затуханием.</summary>
        private static float[] Bell(float freq, float duration, float volume)
        {
            int n = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 7f) * Mathf.Clamp01(t / 0.004f);
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t);
                data[i] = s * env * volume * 0.75f;
            }
            return data;
        }

        private float[] Noise(float duration, float volume)
        {
            int n = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 60f);
                data[i] = ((float)noise.NextDouble() * 2f - 1f) * env * volume;
            }
            return data;
        }

        /// <summary>Простейший фильтр нижних частот (сглаживание).</summary>
        private static float[] LowPass(float[] data, float factor)
        {
            float prev = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                prev += factor * (data[i] - prev);
                data[i] = prev * 3f;
            }
            return data;
        }

        /// <summary>Наложить второй звук на первый со сдвигом offset секунд.</summary>
        private static float[] Mix(float[] first, float[] second, float offset)
        {
            int shift = Mathf.RoundToInt(offset * SampleRate);
            int n = Mathf.Max(first.Length, shift + second.Length);
            float[] data = new float[n];
            for (int i = 0; i < first.Length; i++) data[i] += first[i];
            for (int i = 0; i < second.Length; i++) data[i + shift] += second[i];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            return data;
        }

        private static AudioClip Make(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
