using System.Collections;
using UnityEngine;

/// <summary>
/// APシステム動作確認スクリプト。Playモード後3秒で戦闘→AP0にして赤点滅スクリーンショット。
/// 確認後は削除してください。
/// </summary>
public class APSystemVerifier : MonoBehaviour
{
    private IEnumerator Start()
    {
        // 初期化待ち
        yield return new WaitForSeconds(3f);

        var gm = GameManager.Instance;
        if (gm == null || gm.battleManager == null) yield break;

        // バトル開始
        if (gm.currentState == GameState.Field)
        {
            gm.battleManager.StartRandomBattle();
            Debug.Log("[APVerifier] バトル自動開始");
        }

        // バトル開始演出完了待ち
        yield return new WaitForSeconds(5f);

        // AP表示確認スクリーンショット（ターン開始直後＝AP満タン）
        ScreenCapture.CaptureScreenshot("Assets/Screenshots/ap_full_state.png", 2);
        Debug.Log($"[APVerifier] AP満タン状態スクリーンショット: AP={gm.playerMana}/{gm.playerMaxMana}");

        // APを0に設定してエラーフィードバックをトリガー
        yield return new WaitForSeconds(1f);
        gm.playerMana = 0;
        var battleUI = gm.battleManager?.battleUI;
        if (battleUI != null) battleUI.ShowAPError();
        Debug.Log("[APVerifier] AP=0 設定 → ShowAPError() 呼び出し");

        // 赤点滅中にスクリーンショット
        yield return new WaitForSeconds(0.3f);
        ScreenCapture.CaptureScreenshot("Assets/Screenshots/ap_error_feedback.png", 2);
        Debug.Log("[APVerifier] AP不足エラーフィードバック中スクリーンショット撮影完了");

        // 後始末
        Destroy(gameObject);
    }
}
