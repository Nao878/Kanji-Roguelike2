using System.Collections;
using UnityEngine;

/// <summary>
/// Playモード開始時に自動バトル→スクリーンショットを撮影するデバッグスクリプト。
/// 確認後は削除してください。
/// </summary>
public class AutoBattleScreenshot : MonoBehaviour
{
    [SerializeField] private float battleStartDelay = 2.5f;
    [SerializeField] private float screenshotDelay = 4.0f;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(battleStartDelay);

        var gm = GameManager.Instance;
        if (gm != null && gm.battleManager != null && gm.currentState == GameState.Field)
        {
            gm.battleManager.StartRandomBattle();
            Debug.Log("[AutoBattleScreenshot] ランダムバトル自動開始");
        }

        yield return new WaitForSeconds(screenshotDelay);

        string dir = "Assets/Screenshots/";
        string path = $"{dir}TASK_VERIFY_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        ScreenCapture.CaptureScreenshot(path, 2);
        Debug.Log($"[AutoBattleScreenshot] Gameビュースクリーンショット撮影完了: {path}");
    }
}
