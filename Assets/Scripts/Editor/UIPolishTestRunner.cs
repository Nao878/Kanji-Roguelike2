using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;

/// <summary>
/// UIポリッシュ実装の自動テスト:
/// Playモード開始→バトル→カードタップ→スクリーンショット
/// </summary>
[InitializeOnLoad]
public static class UIPolishTestRunner
{
    private const string PREF_KEY = "UIPolishTestRunner_Active";
    private static double nextPhaseTime;
    private static int phase;

    static UIPolishTestRunner()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Test/UI Polish Screenshot")]
    public static void Run()
    {
        EditorPrefs.SetBool(PREF_KEY, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(PREF_KEY, false))
        {
            EditorPrefs.SetBool(PREF_KEY, false);
            phase = 0;
            nextPhaseTime = EditorApplication.timeSinceStartup + 3.5;
            EditorApplication.update += Step;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Step;
        }
    }

    private static void Step()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < nextPhaseTime) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        switch (phase)
        {
            case 0: // バトル開始
            {
                var enemies = Resources.LoadAll<EnemyData>("");
                if (enemies.Length == 0) { Done(); return; }
                gm.InitializeBattleDeck();
                gm.ChangeState(GameState.Battle);
                gm.battleManager?.StartBattle(enemies[0]);
                Debug.Log($"[UIPolishTest] Phase0: バトル開始 enemy={enemies[0].enemyName}");
                phase = 1;
                nextPhaseTime = now + 4.5; // トランジション待ち
                break;
            }
            case 1: // オーバーヒールテスト：HP強制設定
            {
                gm.playerHP = gm.playerMaxHP + 5; // オーバーヒール状態に設定
                gm.battleManager?.battleUI?.UpdateStatusUI();
                Debug.Log($"[UIPolishTest] Phase1: オーバーヒール設定 HP={gm.playerHP}/{gm.playerMaxHP}");
                phase = 2;
                nextPhaseTime = now + 0.5;
                break;
            }
            case 2: // 手札の最初のカードをタップして予測ボタン表示
            {
                var cards = Object.FindObjectsByType<CardController>(FindObjectsSortMode.None);
                if (cards.Length > 0)
                {
                    var attackCard = cards.FirstOrDefault(c => c.cardData != null &&
                        (c.cardData.effectType == CardEffectType.Attack ||
                         c.cardData.effectType == CardEffectType.AttackAll));
                    var target = attackCard ?? cards[0];
                    var ev = new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left
                    };
                    target.OnPointerClick(ev);
                    Debug.Log($"[UIPolishTest] Phase2: カードタップ kanji={target.cardData?.kanji}");
                }
                phase = 3;
                nextPhaseTime = now + 0.6;
                break;
            }
            case 3: // スクリーンショット撮影
            {
                System.IO.Directory.CreateDirectory("Assets/Screenshots");
                string path = $"Assets/Screenshots/ui_polish_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log($"[UIPolishTest] Phase3: スクリーンショット撮影 → {path}");
                phase = 4;
                nextPhaseTime = now + 1.0;
                break;
            }
            default:
                Done();
                break;
        }
    }

    private static void Done()
    {
        EditorApplication.update -= Step;
        Debug.Log("[UIPolishTest] テスト完了");
    }
}
