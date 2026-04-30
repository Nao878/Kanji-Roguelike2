using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 戦闘画面UI管理（CardControllerベースのドラッグ＆ドロップ対応）
/// </summary>
public class BattleUI : MonoBehaviour
{
    [Header("プレイヤー情報")]
    public TextMeshProUGUI playerHPText;
    public TextMeshProUGUI playerManaText;

    [Header("敵情報")]
    public TextMeshProUGUI enemyNameText;
    public TextMeshProUGUI enemyHPText;
    public TextMeshProUGUI enemyKanjiText;
    public GameObject enemyArea;

    [Header("手札エリア")]
    public Transform handArea;

    [Header("操作ボタン")]
    public Button endTurnButton;
    public Button fusionButton;
    public Button drawCardButton;

    [Header("バトルログ")]
    public TextMeshProUGUI battleLogText;

    [Header("フォント")]
    public TMP_FontAsset appFont;

    [Header("HPバー（省略時は自動生成）")]
    public HPBarController playerHPBar;
    public HPBarController enemyHPBar;

    private List<CardController> handCards = new List<CardController>();

    private void Start()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
            // ターン終了ボタンをモノトーンに
            var etImg = endTurnButton.GetComponent<Image>();
            if (etImg != null) etImg.color = new Color(0.28f, 0.28f, 0.28f, 0.92f);
            var etColors = endTurnButton.colors;
            etColors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            etColors.pressedColor     = new Color(0.18f, 0.18f, 0.18f, 1f);
            endTurnButton.colors = etColors;
        }

        if (fusionButton != null)
        {
            fusionButton.onClick.AddListener(OnFusionClicked);
        }

        if (drawCardButton == null)
            drawCardButton = CreateDrawButton();
        if (drawCardButton != null)
            drawCardButton.onClick.AddListener(OnDrawCardClicked);

        if (playerHPBar == null && playerHPText != null)
            playerHPBar = CreateHPBar(playerHPText.transform, false);
        if (enemyHPBar == null && enemyHPText != null)
            enemyHPBar = CreateHPBar(enemyHPText.transform, true);

        // テキスト要素にOutlineを追加して視認性向上
        AddTextOutline(playerHPText, new Color(0f, 0.2f, 0f, 0.85f));
        AddTextOutline(playerManaText, new Color(0f, 0f, 0.3f, 0.85f));
        AddTextOutline(enemyNameText, new Color(0.3f, 0f, 0f, 0.85f));
        AddTextOutline(enemyHPText, new Color(0.3f, 0f, 0f, 0.85f));
        AddTextOutline(enemyKanjiText, new Color(0.4f, 0f, 0f, 0.9f), 2f);
        AddTextOutline(battleLogText, new Color(0f, 0f, 0f, 0.8f));
    }

    /// <summary>
    /// テキストにOutlineを追加（縁取りで視認性向上）
    /// </summary>
    private void AddTextOutline(Component text, Color outlineColor, float distance = 1.2f)
    {
        if (text == null) return;
        if (text.GetComponent<Outline>() != null) return;
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(distance, -distance);
    }

    /// <summary>
    /// HPバーUIをコードで動的生成
    /// </summary>
    private HPBarController CreateHPBar(Transform attachTo, bool isEnemy)
    {
        if (attachTo == null) return null;

        var barObj = new GameObject("HPBar");
        barObj.transform.SetParent(attachTo, false);

        var barRect = barObj.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -2f);
        barRect.sizeDelta = new Vector2(0f, 14f);

        var bgImg = barObj.AddComponent<Image>();
        bgImg.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);

        // 遅延バー（格ゲー風：ダメージ後に遅れて減る）
        var delayObj = new GameObject("DelayFill");
        delayObj.transform.SetParent(barObj.transform, false);
        var delayRect = delayObj.AddComponent<RectTransform>();
        delayRect.anchorMin = new Vector2(0f, 0f);
        delayRect.anchorMax = new Vector2(1f, 1f);
        delayRect.offsetMin = new Vector2(2f, 2f);
        delayRect.offsetMax = new Vector2(-2f, -2f);
        var delayImg = delayObj.AddComponent<Image>();
        delayImg.type = Image.Type.Filled;
        delayImg.fillMethod = Image.FillMethod.Horizontal;
        delayImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        delayImg.color = isEnemy ? new Color(0.6f, 0.05f, 0.05f) : new Color(0.85f, 0.1f, 0.1f);

        var fillObj = new GameObject("NormalFill");
        fillObj.transform.SetParent(barObj.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        var fillImg = fillObj.AddComponent<Image>();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.color = isEnemy ? new Color(0.85f, 0.25f, 0.25f) : new Color(0.2f, 0.8f, 0.2f);

        var ovhObj = new GameObject("OverhealFill");
        ovhObj.transform.SetParent(barObj.transform, false);
        var ovhRect = ovhObj.AddComponent<RectTransform>();
        ovhRect.anchorMin = new Vector2(0f, 0f);
        ovhRect.anchorMax = new Vector2(1f, 1f);
        ovhRect.offsetMin = new Vector2(2f, 1f);
        ovhRect.offsetMax = new Vector2(-2f, 0f);
        var ovhImg = ovhObj.AddComponent<Image>();
        ovhImg.type = Image.Type.Filled;
        ovhImg.fillMethod = Image.FillMethod.Horizontal;
        ovhImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        ovhImg.color = new Color(1f, 0.85f, 0f, 0.85f);
        ovhObj.SetActive(false);

        var iconGo = new GameObject("StatusIcon");
        iconGo.transform.SetParent(barObj.transform, false);
        var iconRect = iconGo.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(1f, 0.5f);
        iconRect.anchorMax = new Vector2(1f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(6f, 0f);
        iconRect.sizeDelta = new Vector2(90f, 18f);
        var iconTmp = iconGo.AddComponent<TextMeshProUGUI>();
        iconTmp.fontSize = 11f;
        iconTmp.color = new Color(1f, 0.9f, 0.1f);
        iconTmp.alignment = TextAlignmentOptions.Left;
        if (appFont != null) iconTmp.font = appFont;
        iconGo.SetActive(false);

        var ctrl = barObj.AddComponent<HPBarController>();
        ctrl.normalBar = fillImg;
        ctrl.delayBar  = delayImg;
        ctrl.overhealBar = ovhImg;
        ctrl.statusIcon = iconTmp;
        if (!isEnemy)
        {
            ctrl.normalColor = new Color(0.2f, 0.8f, 0.2f);
            ctrl.overhealColor = new Color(1f, 0.85f, 0f);
        }
        return ctrl;
    }

    /// <summary>
    /// 手札UIを更新（CardControllerベース）
    /// </summary>
    public void UpdateHandUI()
    {
        // 合体ボタンをクリア
        CardController.ClearAllFusionButtons();

        // 既存のカードUIをクリア
        foreach (var card in handCards)
        {
            if (card != null) Destroy(card.gameObject);
        }
        handCards.Clear();

        var gm = GameManager.Instance;
        if (gm == null) return;

        // 手札のカードを表示
        for (int i = 0; i < gm.hand.Count; i++)
        {
            var card = gm.hand[i];
            var cardCtrl = CreateDraggableCard(card);
            if (cardCtrl != null) handCards.Add(cardCtrl);
        }
    }

    /// <summary>
    /// ドラッグ可能なカードUIを生成
    /// </summary>
    private CardController CreateDraggableCard(KanjiCardData data)
    {
        if (handArea == null || data == null) return null;

        // カードオブジェクト作成
        var go = new GameObject($"Card_{data.kanji}");
        go.transform.SetParent(handArea, false);
        go.tag = "Card";

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100f, 140f);

        // CanvasGroupを追加（ドラッグ時のレイキャスト制御用）
        var canvasGroup = go.AddComponent<CanvasGroup>();

        // カード背景（ダークグレー #222222）
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.133f, 0.133f, 0.133f, 0.95f);

        // 属性ボーダー：Outlineコンポーネントで枠取り
        var elemOutline = go.AddComponent<Outline>();
        elemOutline.effectColor = GetElementBorderColor(data != null ? data.element : CardElement.None);
        elemOutline.effectDistance = new Vector2(4f, -4f);
        elemOutline.useGraphicAlpha = false;

        // CardControllerコンポーネント
        var cardCtrl = go.AddComponent<CardController>();
        cardCtrl.cardBackground = bg;
        cardCtrl.canvasGroup = canvasGroup;
        cardCtrl.appFont = appFont;

        // 漢字テキスト
        var kanjiGo = new GameObject("KanjiText");
        kanjiGo.transform.SetParent(go.transform, false);
        var kanjiText = kanjiGo.AddComponent<TextMeshProUGUI>();
        kanjiText.fontSize = 42;
        kanjiText.alignment = TextAlignmentOptions.Center;
        kanjiText.color = Color.white;
        kanjiText.raycastTarget = false;
        if (appFont != null) kanjiText.font = appFont;
        var kanjiRect = kanjiGo.GetComponent<RectTransform>();
        kanjiRect.anchorMin = new Vector2(0, 0.4f);
        kanjiRect.anchorMax = new Vector2(1, 0.9f);
        kanjiRect.offsetMin = Vector2.zero;
        kanjiRect.offsetMax = Vector2.zero;
        cardCtrl.kanjiText = kanjiText;

        // コストテキスト
        var costGo = new GameObject("CostText");
        costGo.transform.SetParent(go.transform, false);
        var costText = costGo.AddComponent<TextMeshProUGUI>();
        costText.fontSize = 18;
        costText.alignment = TextAlignmentOptions.TopLeft;
        costText.color = new Color(0.4f, 0.7f, 1f);
        costText.raycastTarget = false;
        if (appFont != null) costText.font = appFont;
        var costRect = costGo.GetComponent<RectTransform>();
        costRect.anchorMin = new Vector2(0, 0.85f);
        costRect.anchorMax = new Vector2(0.3f, 1f);
        costRect.offsetMin = new Vector2(5f, 0);
        costRect.offsetMax = new Vector2(0, -2f);
        cardCtrl.costText = costText;

        // 効果テキスト
        var descGo = new GameObject("DescText");
        descGo.transform.SetParent(go.transform, false);
        var descText = descGo.AddComponent<TextMeshProUGUI>();
        descText.fontSize = 14;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(0.9f, 0.9f, 0.9f);
        descText.raycastTarget = false;
        if (appFont != null) descText.font = appFont;
        var descRect = descGo.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 0);
        descRect.anchorMax = new Vector2(1, 0.35f);
        descRect.offsetMin = new Vector2(3f, 3f);
        descRect.offsetMax = new Vector2(-3f, 0);
        cardCtrl.descriptionText = descText;

        // 合成プレビュー（頭上に半透明テキスト）
        var previewBgGo = new GameObject("FusionPreview");
        previewBgGo.transform.SetParent(go.transform, false);
        var previewBgRect = previewBgGo.AddComponent<RectTransform>();
        previewBgRect.anchorMin = new Vector2(0.15f, 0.92f);
        previewBgRect.anchorMax = new Vector2(0.85f, 1.35f);
        previewBgRect.offsetMin = Vector2.zero;
        previewBgRect.offsetMax = Vector2.zero;
        var previewBg = previewBgGo.AddComponent<Image>();
        previewBg.color = new Color(1f, 0.9f, 0.2f, 0.4f);
        previewBg.raycastTarget = false;

        var previewTextGo = new GameObject("PreviewText");
        previewTextGo.transform.SetParent(previewBgGo.transform, false);
        var previewText = previewTextGo.AddComponent<TextMeshProUGUI>();
        previewText.fontSize = 28;
        previewText.alignment = TextAlignmentOptions.Center;
        previewText.color = new Color(1f, 0.95f, 0.5f, 0.85f);
        previewText.raycastTarget = false;
        if (appFont != null) previewText.font = appFont;
        var previewTextRect = previewTextGo.GetComponent<RectTransform>();
        previewTextRect.anchorMin = Vector2.zero;
        previewTextRect.anchorMax = Vector2.one;
        previewTextRect.offsetMin = Vector2.zero;
        previewTextRect.offsetMax = Vector2.zero;

        cardCtrl.fusionPreviewObj = previewBgGo;
        cardCtrl.fusionPreviewText = previewText;

        // コールバック設定
        cardCtrl.onCardUsed = () => UpdateStatusUI();
        cardCtrl.onHandChanged = () =>
        {
            // 少し遅延してUI更新（Destroyの完了を待つ）
            StartCoroutine(DelayedUpdateHand());
        };

        // セットアップ
        cardCtrl.Setup(data);

        return cardCtrl;
    }

    /// <summary>
    /// 属性に応じたボーダーカラーを返す
    /// </summary>
    private Color GetElementBorderColor(CardElement element)
    {
        switch (element)
        {
            case CardElement.Fire:  return new Color(1f, 0.35f, 0.1f, 0.9f);
            case CardElement.Water: return new Color(0.15f, 0.55f, 1f, 0.9f);
            case CardElement.Wood:  return new Color(0.15f, 0.75f, 0.25f, 0.9f);
            case CardElement.Earth: return new Color(0.7f, 0.5f, 0.2f, 0.9f);
            case CardElement.Metal: return new Color(0.75f, 0.75f, 0.8f, 0.9f);
            case CardElement.Sun:   return new Color(1f, 0.85f, 0.1f, 0.9f);
            case CardElement.Moon:  return new Color(0.6f, 0.6f, 1f, 0.9f);
            default:                return new Color(0.25f, 0.25f, 0.25f, 0.7f);
        }
    }

    private System.Collections.IEnumerator DelayedUpdateHand()
    {
        yield return null; // 1フレーム待つ
        UpdateHandUI();
        UpdateStatusUI();
    }

    /// <summary>
    /// ターン終了ボタン
    /// </summary>
    private void OnEndTurnClicked()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.battleManager == null) return;

        gm.battleManager.EndPlayerTurn();
        UpdateHandUI();
        UpdateStatusUI();
    }

    /// <summary>
    /// ドローボタンを動的生成（ターン終了ボタンの真上に配置・モノトーン）
    /// </summary>
    private Button CreateDrawButton()
    {
        Transform parent = endTurnButton?.transform.parent ?? transform;

        var go = new GameObject("DrawCardButton");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();

        if (endTurnButton != null)
        {
            var src = endTurnButton.GetComponent<RectTransform>();
            // EndTurnButtonと同じアンカー範囲でY方向にシフト
            float anchorHeight = src.anchorMax.y - src.anchorMin.y;
            float margin = 0.02f; // アンカー比率でのマージン
            rect.anchorMin = new Vector2(src.anchorMin.x, src.anchorMax.y + margin);
            rect.anchorMax = new Vector2(src.anchorMax.x, src.anchorMax.y + margin + anchorHeight);
            rect.sizeDelta = src.sizeDelta; // ストレッチなので(0,0)
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(120f, 40f);
            rect.anchoredPosition = new Vector2(60f, 60f);
        }

        // モノトーン（グレー）カラー
        var img = go.AddComponent<Image>();
        img.color = new Color(0.32f, 0.32f, 0.32f, 0.92f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = img.color;
        colors.highlightedColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        colors.pressedColor     = new Color(0.2f, 0.2f, 0.2f, 1f);
        colors.disabledColor    = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        btn.colors = colors;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "ドロー\n(AP:1)";
        tmp.fontSize = 13f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (appFont != null) tmp.font = appFont;
        var tmpRect = textGo.GetComponent<RectTransform>();
        tmpRect.anchorMin = Vector2.zero;
        tmpRect.anchorMax = Vector2.one;
        tmpRect.offsetMin = Vector2.zero;
        tmpRect.offsetMax = Vector2.zero;

        return btn;
    }

    /// <summary>
    /// ドローボタン押下：AP1消費してカードを1枚引く（AP不足時はエフェクト）
    /// </summary>
    private void OnDrawCardClicked()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.hand.Count >= gm.initialHandSize) return;

        if (gm.playerMana < 1)
        {
            // AP不足フィードバック
            if (VFXManager.Instance != null && playerManaText != null)
                VFXManager.Instance.PlayAPShortageEffect(playerManaText.GetComponent<UnityEngine.RectTransform>());
            return;
        }

        gm.playerMana -= 1;
        gm.DrawFromDeck(1);
        UpdateHandUI();
        UpdateStatusUI();
    }

    /// <summary>
    /// 合体ボタン
    /// </summary>
    private void OnFusionClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameState.Fusion);
        }
    }

    /// <summary>
    /// 戦闘開始時に敵の表示を完全リセット
    /// 前回の戦闘でSetActive(false)された状態を復元する
    /// </summary>
    public void ResetEnemyDisplay()
    {
        // 敵エリア全体をアクティブに
        if (enemyArea != null)
        {
            enemyArea.SetActive(true);

            // Imageのalpha値をリセット
            var img = enemyArea.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = Mathf.Max(c.a, 0.15f); // 元の背景透過度を維持
                img.color = c;
            }

            // CanvasGroupがあればリセット
            var cg = enemyArea.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable = true;
            }

            // Enemyタグの確認
            if (!enemyArea.CompareTag("Enemy"))
            {
                try { enemyArea.tag = "Enemy"; }
                catch { Debug.LogWarning("[BattleUI] Enemyタグの設定に失敗"); }
            }
        }

        // 敵漢字テキストのalpha値をリセット
        if (enemyKanjiText != null)
        {
            enemyKanjiText.alpha = 1f;
            enemyKanjiText.color = new Color(0.9f, 0.3f, 0.3f, 1f);
        }

        // 敵名テキストのalpha値をリセット
        if (enemyNameText != null)
        {
            enemyNameText.alpha = 1f;
        }

        // 敵HPテキストのalpha値をリセット
        if (enemyHPText != null)
        {
            enemyHPText.alpha = 1f;
        }

        // 敵HPバーを即時リセット（アニメーションなし）
        if (enemyHPBar != null)
        {
            var bm = GameManager.Instance?.battleManager;
            if (bm?.currentEnemyData != null)
                enemyHPBar.SetHPImmediate(bm.enemyCurrentHP, bm.currentEnemyData.maxHP);
        }

        Debug.Log("[BattleUI] 敵表示リセット完了");
    }

    /// <summary>
    /// ステータスUIを更新
    /// </summary>
    public void UpdateStatusUI()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (playerHPText != null) playerHPText.text = $"HP: {gm.playerHP}/{gm.playerMaxHP}";
        if (playerManaText != null) playerManaText.text = $"マナ: {gm.playerMana}/{gm.playerMaxMana}";

        if (drawCardButton != null)
            drawCardButton.interactable = gm.playerMana >= 1 && gm.hand.Count < gm.initialHandSize;

        if (playerHPBar != null)
        {
            playerHPBar.SetHP(gm.playerHP, gm.playerMaxHP);
            string buffText = "";
            if (gm.playerAttackBuff > 0) buffText += $"攻↑+{gm.playerAttackBuff} ";
            if (gm.playerDefenseBuff > 0) buffText += $"防↑+{gm.playerDefenseBuff}";
            playerHPBar.SetStatusIcon(buffText.Trim());
        }

        if (gm.battleManager != null && gm.battleManager.currentEnemyData != null)
        {
            var enemy = gm.battleManager.currentEnemyData;
            if (enemyNameText != null) enemyNameText.text = enemy.enemyName;
            if (enemyHPText != null) enemyHPText.text = $"HP: {gm.battleManager.enemyCurrentHP}/{enemy.maxHP}";
            if (enemyKanjiText != null) enemyKanjiText.text = enemy.displayKanji;

            if (enemyHPBar != null)
            {
                enemyHPBar.SetHP(gm.battleManager.enemyCurrentHP, enemy.maxHP);
                enemyHPBar.SetStatusIcon(gm.battleManager.enemyIsStunned ? "[スタン]" : "");
            }
        }
    }
}
