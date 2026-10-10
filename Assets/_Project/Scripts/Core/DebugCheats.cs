#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NaSvyazi.Economy;
using NaSvyazi.Events;
using NaSvyazi.Store;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NaSvyazi.Core
{
    /// <summary>
    /// Читы для проверки перед релизом. Работают ТОЛЬКО в редакторе Unity и в Development Build —
    /// в обычную релизную сборку этот код не попадает (директива UNITY_EDITOR || DEVELOPMENT_BUILD).
    /// Создаются автоматически в сцене салона. F1 занят обучением, поэтому читы на F5–F12.
    /// </summary>
    public class DebugCheats : MonoBehaviour
    {
        private bool showHelp = true;
        private GUIStyle style;

        // Живёт всю сессию (и в меню, и в салоне); работает только когда загружен салон.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            GameObject go = new GameObject("DebugCheats");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugCheats>();
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || DayManager.Instance == null) return;

            if (k.f5Key.wasPressedThisFrame && EconomyManager.Instance != null)
                EconomyManager.Instance.SetBalance(EconomyManager.Instance.Balance + 50000); // без налога и отчёта

            if (k.f6Key.wasPressedThisFrame && LevelManager.Instance != null)
            {
                LevelManager lm = LevelManager.Instance;
                int need = lm.IsMaxLevel ? lm.MasteryStep : lm.XpForNextLevel - lm.Xp;
                // AddXp умножает на коэффициент сложности — делим заранее, чтобы уровень взялся ровно.
                LevelManager.AddXp(Mathf.CeilToInt(need / Mathf.Max(0.1f, GameDifficulty.XpMultiplier)) + 1, "Чит: уровень");
            }

            if (k.f7Key.wasPressedThisFrame) ReputationManager.Add(20, "Чит: репутация");
            if (k.f8Key.wasPressedThisFrame && DayManager.Instance != null) DayManager.Instance.DebugSkipToClose();
            if (k.f9Key.wasPressedThisFrame && StoreEventManager.Instance != null) StoreEventManager.Instance.DebugNextEvent();

            if (k.f10Key.wasPressedThisFrame && CleaningManager.Instance != null && Camera.main != null)
            {
                Transform c = Camera.main.transform;
                Vector3 p = c.position + Vector3.ProjectOnPlane(c.forward, Vector3.up).normalized * 2f;
                CleaningManager.Instance.DebugSpawn(p);
            }

            if (k.f11Key.wasPressedThisFrame && EconomyManager.Instance != null)
                EconomyManager.Instance.SetBalance(-1000);

            if (k.f12Key.wasPressedThisFrame) showHelp = !showHelp;
        }

        private void OnGUI()
        {
            if (!showHelp || DayManager.Instance == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
            GUI.Box(new Rect(10, Screen.height - 190, 330, 180),
                "ЧИТЫ (только редактор / Development Build)\n" +
                "F5  +50 000 руб.\n" +
                "F6  +1 уровень (на 8-м — очко мастерства)\n" +
                "F7  +20 репутации\n" +
                "F8  перемотать день к закрытию\n" +
                "F9  следующее событие дня\n" +
                "F10 пятно грязи перед собой\n" +
                "F11 уйти в минус (проверка банкротства)\n" +
                "F12 скрыть подсказку", style);
        }
    }
}
#endif
