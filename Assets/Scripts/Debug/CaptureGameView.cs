using System.Collections;
using UnityEngine;

/// <summary>
/// ゲームビューを自動撮影する一時スクリプト（実装確認用）
/// GameManagerオブジェクトにアタッチして使用
/// </summary>
public class CaptureGameView : MonoBehaviour
{
    private IEnumerator Start()
    {
        // 2秒待機して画面初期化を待つ
        yield return new WaitForSeconds(2f);
        string path1 = $"Assets/Screenshots/route_ui_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        ScreenCapture.CaptureScreenshot(path1);
        Debug.Log($"[CaptureGameView] スクリーンショット1撮影: {path1}");

        // 追加で1秒後にもう1枚
        yield return new WaitForSeconds(1f);
        string path2 = $"Assets/Screenshots/battle_ui_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        ScreenCapture.CaptureScreenshot(path2);
        Debug.Log($"[CaptureGameView] スクリーンショット2撮影: {path2}");
    }
}
