using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OniBossManager : MonoBehaviour
{
    private BattleManager battleManager;
    private int countdown = 9;

    // UI elements
    private GameObject countdownUIObj;
    private TextMeshProUGUI countdownText;
    private Canvas countdownCanvas;

    private readonly string[] kanjiNumbers = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

    public void InitForOniBoss()
    {
        battleManager = GetComponent<BattleManager>();
        countdown = 9;
        
        CreateCountdownUI();
        UpdateCountdownUI();
    }

    private void CreateCountdownUI()
    {
        if (countdownUIObj != null) return;

        // 専用Canvasを作成（最前面に表示するため Screen Space Overlay で高 Sorting Order を設定）
        var canvasGo = new GameObject("OniCountdownCanvas");
        countdownCanvas = canvasGo.AddComponent<Canvas>();
        countdownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        countdownCanvas.sortingOrder = 9000;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        countdownUIObj = new GameObject("OniCountdownText");
        countdownUIObj.transform.SetParent(canvasGo.transform, false);

        RectTransform rt = countdownUIObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-40f, 0f); // 右端寄り
        rt.sizeDelta = new Vector2(220f, 220f);

        countdownText = countdownUIObj.AddComponent<TextMeshProUGUI>();
        countdownText.fontSize = 160;
        countdownText.color = new Color(1f, 0.1f, 0.1f, 0.9f);
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.enableWordWrapping = false;

        // AppFont SDF を適用
        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var f in fonts)
        {
            if (f.name.Contains("AppFont") || f.name.Contains("SDF") || f.name.Contains("JP"))
            {
                countdownText.font = f;
                break;
            }
        }

        // アウトラインで視認性向上
        var outline = countdownUIObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(4f, -4f);
    }

    private void UpdateCountdownUI()
    {
        if (countdownText != null)
        {
            int displayNum = Mathf.Clamp(countdown, 0, 9);
            countdownText.text = kanjiNumbers[displayNum];
        }
    }

    public void OnOniTurnAction(System.Action onComplete)
    {
        // Decrement countdown
        countdown--;
        UpdateCountdownUI();

        if (countdown > 0)
        {
            battleManager.AddBattleLog("鬼は不気味な笑みを浮かべている…");
        }
        else
        {
            // countdown == 0
            battleManager.AddBattleLog("<color=#FF0000><b>【死星・零】発動！！</b></color>");
            int damage = 999;
            if (GameManager.Instance != null)
            {
                damage = GameManager.Instance.playerMaxHP * 2; // Maximum HP * 2
                GameManager.Instance.TakeDamage(damage);
            }
            
            if (battleManager.battleUI != null && VFXManager.Instance != null)
            {
                GameObject target = battleManager.battleUI.playerHPText != null ? battleManager.battleUI.playerHPText.gameObject : battleManager.battleUI.gameObject;
                VFXManager.Instance.PlayDamageEffect(target, damage, true);
            }
            
            battleManager.AddBattleLog($"プレイヤーは即死級のダメージ（{damage}）を受けた！");
        }

        // Wait a bit, then end enemy turn
        Invoke(nameof(CallComplete), 1.5f);
        this.onCompleteCallback = onComplete;
    }

    private System.Action onCompleteCallback;

    private void CallComplete()
    {
        if (onCompleteCallback != null)
        {
            var temp = onCompleteCallback;
            onCompleteCallback = null;
            temp.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (countdownUIObj != null) Destroy(countdownUIObj);
        if (countdownCanvas != null) Destroy(countdownCanvas.gameObject);
    }
}
