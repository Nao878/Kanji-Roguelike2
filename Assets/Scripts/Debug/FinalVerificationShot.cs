using System.Collections;
using UnityEngine;

/// <summary>
/// 実装完了確認用スクリーンショット（起動後3秒でGameビューを撮影）
/// 確認後は削除してください
/// </summary>
public class FinalVerificationShot : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return new WaitForSeconds(3f);
        string path = $"Assets/Screenshots/VERIFY_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[FinalVerificationShot] Gameビュー撮影完了: {path}");
        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }
}
