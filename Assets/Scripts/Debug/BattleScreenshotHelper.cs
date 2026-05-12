using UnityEngine;

/// <summary>
/// Gameビュー撮影用一時スクリプト。BattlePanelが有効な状態でF12キーを押すとスクショ保存。
/// </summary>
public class BattleScreenshotHelper : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            string path = $"Assets/Screenshots/battle_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[Screenshot] Saved: {path}");
        }
    }
}
