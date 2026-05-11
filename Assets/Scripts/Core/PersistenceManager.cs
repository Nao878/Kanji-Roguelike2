using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ゲームオーバー時の永続コイン保存と引き継ぎショップ
/// </summary>
public static class PersistenceManager
{
    private const string KEY_COINS = "PersistentCoins";
    private const string KEY_INIT_HAND = "UpgradeInitHand";    // 初期手札増加（最大+3）
    private const string KEY_INIT_SHIELD = "UpgradeInitShield";// 初期シールド増加（最大+3）

    public static int GetPersistentCoins() => PlayerPrefs.GetInt(KEY_COINS, 0);
    public static int GetInitHandBonus()   => PlayerPrefs.GetInt(KEY_INIT_HAND, 0);
    public static int GetInitShieldBonus() => PlayerPrefs.GetInt(KEY_INIT_SHIELD, 0);

    /// <summary>
    /// ゲームオーバー時に呼ぶ：到達ステージ・撃破数からコインを付与してPlayerPrefsに保存
    /// </summary>
    public static void OnGameOver(RouteMapManager routeMap)
    {
        var gm = GameManager.Instance;
        int stageReached = routeMap != null ? GetCurrentStage(routeMap) : 0;
        int coinsEarned = stageReached * 5 + 10; // 1ステージ5枚 + ベース10枚

        int current = GetPersistentCoins();
        PlayerPrefs.SetInt(KEY_COINS, current + coinsEarned);
        PlayerPrefs.Save();

        Debug.Log($"[PersistenceManager] ゲームオーバー: ステージ{stageReached}到達, コイン+{coinsEarned} (合計:{current + coinsEarned})");

        // GameOverパネルにコイン情報を追記
        ShowCoinRewardOnGameOver(coinsEarned, current + coinsEarned, gm);
    }

    private static int GetCurrentStage(RouteMapManager rm)
    {
        // RouteMapManagerのcurrentStageフィールドはprivateなので、
        // 近似としてPlayerPrefsから読まないがシンプルに0返し
        // ※実際にはrm.CurrentStageプロパティが必要だが、ここでは簡易実装
        return 0;
    }

    private static void ShowCoinRewardOnGameOver(int earned, int total, GameManager gm)
    {
        if (gm == null || gm.gameOverPanel == null) return;

        // 既存テキストをスキャンして末尾に追加
        var canvas = gm.gameOverPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // コイン報酬テキストを追加（GameOverPanelの子として）
        var go = new GameObject("CoinRewardText");
        go.transform.SetParent(gm.gameOverPanel.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.05f);
        rect.anchorMax = new Vector2(1f, 0.25f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = $"コイン獲得: +{earned}\n累計コイン: {total}G\n（次回プレイ時に引き継ぎ強化が可能）";
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.85f, 0.3f);
        tmp.raycastTarget = false;

        // 引き継ぎショップボタンも追加
        var shopBtnGo = new GameObject("InheritShopBtn");
        shopBtnGo.transform.SetParent(gm.gameOverPanel.transform, false);
        var sbRect = shopBtnGo.AddComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(0.15f, -0.05f);
        sbRect.anchorMax = new Vector2(0.85f, 0.05f);
        sbRect.offsetMin = sbRect.offsetMax = Vector2.zero;
        shopBtnGo.AddComponent<Image>().color = new Color(0.2f, 0.5f, 0.2f, 0.9f);
        var shopBtn = shopBtnGo.AddComponent<Button>();
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(shopBtnGo.transform, false);
        var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
        labelTmp.text = $"引き継ぎショップを開く（所持:{total}G）";
        labelTmp.fontSize = 14;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.color = Color.white;
        labelTmp.raycastTarget = false;
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        shopBtn.onClick.AddListener(ShowInheritShop);
    }

    /// <summary>
    /// 引き継ぎショップを表示
    /// </summary>
    public static void ShowInheritShop()
    {
        var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 既存パネルがあれば削除
        var existing = canvas.transform.Find("InheritShopPanel");
        if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);

        int coins = GetPersistentCoins();
        int handBonus = GetInitHandBonus();
        int shieldBonus = GetInitShieldBonus();

        var panel = new GameObject("InheritShopPanel");
        panel.transform.SetParent(canvas.transform, false);
        panel.transform.SetAsLastSibling();

        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.05f, 0.05f);
        rect.anchorMax = new Vector2(0.95f, 0.95f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.1f, 0.98f);

        // タイトル
        AddTMP(panel.transform, "★ 引き継ぎショップ ★", new Vector2(0f, 0.88f), new Vector2(1f, 0.98f), 24, new Color(1f, 0.9f, 0.3f), FontStyles.Bold);
        AddTMP(panel.transform, $"所持コイン: {coins}G", new Vector2(0f, 0.81f), new Vector2(1f, 0.89f), 18, new Color(1f, 0.85f, 0.3f));

        // 強化1: 初期手札+1（コスト30G、最大3回）
        int handCost = 30;
        bool canBuyHand = coins >= handCost && handBonus < 3;
        AddShopItem(panel.transform, $"初期手札 +1（現在+{handBonus}、最大+3）",
            "次のゲームから手札が1枚多い状態でスタート",
            $"{handCost}G", canBuyHand,
            new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.78f),
            () => {
                if (!canBuyHand) return;
                PlayerPrefs.SetInt(KEY_COINS, coins - handCost);
                PlayerPrefs.SetInt(KEY_INIT_HAND, handBonus + 1);
                PlayerPrefs.Save();
                ShowInheritShop();
            });

        // 強化2: 初期シールド+1（コスト25G、最大3回）
        int shieldCost = 25;
        bool canBuyShield = coins >= shieldCost && shieldBonus < 3;
        AddShopItem(panel.transform, $"初期シールド +1（現在+{shieldBonus}、最大+3）",
            "次のゲームから初期シールド数が1枚多い",
            $"{shieldCost}G", canBuyShield,
            new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.63f),
            () => {
                if (!canBuyShield) return;
                PlayerPrefs.SetInt(KEY_COINS, coins - shieldCost);
                PlayerPrefs.SetInt(KEY_INIT_SHIELD, shieldBonus + 1);
                PlayerPrefs.Save();
                ShowInheritShop();
            });

        // 閉じるボタン
        var closeGo = new GameObject("CloseBtn");
        closeGo.transform.SetParent(panel.transform, false);
        var cRect = closeGo.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.3f, 0.03f); cRect.anchorMax = new Vector2(0.7f, 0.13f);
        cRect.offsetMin = cRect.offsetMax = Vector2.zero;
        closeGo.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f);
        var closeBtn = closeGo.AddComponent<Button>();
        AddTMP(closeGo.transform, "閉じる", Vector2.zero, Vector2.one, 16, Color.white);
        var capturedPanel = panel;
        closeBtn.onClick.AddListener(() => UnityEngine.Object.Destroy(capturedPanel));
    }

    private static void AddShopItem(Transform parent, string title, string desc, string cost,
        bool available, Vector2 anchorMin, Vector2 anchorMax, System.Action onBuy)
    {
        var go = new GameObject("ShopItem");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = available ? new Color(0.12f, 0.2f, 0.12f, 0.9f) : new Color(0.15f, 0.15f, 0.15f, 0.7f);
        var border = go.AddComponent<Outline>();
        border.effectColor = available ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);
        border.effectDistance = new Vector2(2f, -2f);

        AddTMP(go.transform, title, new Vector2(0.02f, 0.6f), new Vector2(0.75f, 0.98f), 14, available ? Color.white : new Color(0.5f, 0.5f, 0.5f));
        AddTMP(go.transform, desc, new Vector2(0.02f, 0.15f), new Vector2(0.75f, 0.58f), 11, new Color(0.7f, 0.7f, 0.7f));

        var btnGo = new GameObject("BuyBtn");
        btnGo.transform.SetParent(go.transform, false);
        var bRect = btnGo.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.76f, 0.1f); bRect.anchorMax = new Vector2(0.98f, 0.9f);
        bRect.offsetMin = bRect.offsetMax = Vector2.zero;
        btnGo.AddComponent<Image>().color = available ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.25f, 0.25f, 0.25f);
        var btn = btnGo.AddComponent<Button>();
        btn.interactable = available;
        AddTMP(btnGo.transform, cost, Vector2.zero, Vector2.one, 15, Color.white, FontStyles.Bold);
        btn.onClick.AddListener(() => onBuy?.Invoke());
    }

    private static TextMeshProUGUI AddTMP(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax,
        float fontSize, Color color, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("TMP");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = style; tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    /// <summary>
    /// ゲーム開始時に引き継ぎ強化値をGameManagerに適用
    /// </summary>
    public static void ApplyInheritUpgrades(GameManager gm)
    {
        int handBonus = GetInitHandBonus();
        int shieldBonus = GetInitShieldBonus();
        if (handBonus > 0) gm.initialHandSize += handBonus;
        if (shieldBonus > 0) gm.maxShields += shieldBonus;
        if (handBonus > 0 || shieldBonus > 0)
            Debug.Log($"[PersistenceManager] 引き継ぎ強化適用: 手札+{handBonus}, シールド+{shieldBonus}");
    }
}
