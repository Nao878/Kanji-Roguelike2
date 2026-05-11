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

        // 専用Canvasを作成（最前面に表示）
        var canvasGo = new GameObject("OniCountdownCanvas");
        countdownCanvas = canvasGo.AddComponent<Canvas>();
        countdownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        countdownCanvas.sortingOrder = 9000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        // 赤い背景コンテナ（敵キャラクター頭上・右寄りに配置）
        var containerGo = new GameObject("OniCountdownContainer");
        containerGo.transform.SetParent(canvasGo.transform, false);
        var containerRect = containerGo.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.60f, 0.58f);
        containerRect.anchorMax = new Vector2(0.90f, 0.80f);
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;
        var containerImg = containerGo.AddComponent<Image>();
        containerImg.color = new Color(0.70f, 0.05f, 0.05f, 0.88f);
        var containerOutline = containerGo.AddComponent<Outline>();
        containerOutline.effectColor = new Color(1f, 0.3f, 0.3f, 0.95f);
        containerOutline.effectDistance = new Vector2(4f, -4f);

        // カウントダウンテキスト（赤背景の子要素）
        countdownUIObj = new GameObject("OniCountdownText");
        countdownUIObj.transform.SetParent(containerGo.transform, false);

        var rt = countdownUIObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        countdownText = countdownUIObj.AddComponent<TextMeshProUGUI>();
        countdownText.fontSize = 100;
        countdownText.color = new Color(1f, 0.96f, 0.88f, 1f);
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.enableWordWrapping = false;

        // AppFont SDF を適用（BattleUIのフォントを優先的に取得）
        var bui = GetComponent<BattleManager>()?.battleUI;
        if (bui != null && bui.appFont != null)
        {
            countdownText.font = bui.appFont;
        }
        else
        {
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in fonts)
            {
                if (f.name.Contains("AppFont") || f.name.Contains("NotoSans") || f.name.Contains("JP"))
                {
                    countdownText.font = f;
                    break;
                }
            }
        }
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
            // countdown == 0 → 全シールド消滅 → ゲームオーバー
            battleManager.AddBattleLog("<color=#FF0000><b>【死星・零】発動！！全シールドが消滅！</b></color>");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.shields.Clear();
                battleManager.battleUI?.UpdateShieldUI();
                GameManager.Instance.playerHP = 0;
                GameManager.Instance.ChangeState(GameState.GameOver);
            }

            if (battleManager.battleUI != null && VFXManager.Instance != null)
            {
                GameObject target = battleManager.battleUI.playerManaText != null ? battleManager.battleUI.playerManaText.gameObject : battleManager.battleUI.gameObject;
                VFXManager.Instance.PlayDamageEffect(target, 999, true);
            }
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
        if (countdownCanvas != null) Destroy(countdownCanvas.gameObject);
    }
}
