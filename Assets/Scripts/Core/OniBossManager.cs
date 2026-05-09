using UnityEngine;
using TMPro;

public class OniBossManager : MonoBehaviour
{
    private BattleManager battleManager;
    private int countdown = 9;
    
    // UI elements
    private GameObject countdownUIObj;
    private TextMeshProUGUI countdownText;

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

        // Try to find the EnemyArea to attach the UI
        if (battleManager.battleUI != null && battleManager.battleUI.enemyArea != null)
        {
            countdownUIObj = new GameObject("OniCountdownText");
            countdownUIObj.transform.SetParent(battleManager.battleUI.enemyArea.transform, false);
            
            RectTransform rt = countdownUIObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(250, 50); // 敵の横（右側）に配置
            rt.sizeDelta = new Vector2(200, 200);

            countdownText = countdownUIObj.AddComponent<TextMeshProUGUI>();
            countdownText.fontSize = 150;
            countdownText.color = Color.red;
            countdownText.alignment = TextAlignmentOptions.Center;
            countdownText.enableWordWrapping = false;
            
            // AppFont SDF を適用
            TMP_FontAsset appFont = null;
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in fonts)
            {
                if (f.name.Contains("AppFont") || f.name.Contains("SDF") || f.name.Contains("JP"))
                {
                    appFont = f;
                    break;
                }
            }
            if (appFont != null)
            {
                countdownText.font = appFont;
            }
            
            // 最前面に表示する
            countdownUIObj.transform.SetAsLastSibling();
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
        if (countdownUIObj != null)
        {
            Destroy(countdownUIObj);
        }
    }
}
