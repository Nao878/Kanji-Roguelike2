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
    private Button _fleeButton;

    // ドローボタンAP演出UI
    private Image _drawButtonGlowOverlay;
    private TextMeshProUGUI _drawButtonWarningText;
    private GameObject _drawButtonStrikeline;
    private Coroutine _drawPulseCoroutine;

    // シールドUI
    private Transform shieldContainer;
    private List<GameObject> shieldUIObjects = new List<GameObject>();

    // 店選択UI
    private GameObject shopSelectionPanel;

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

        // ドローボタン復活（AP1消費でドロー）
        if (drawCardButton != null)
        {
            drawCardButton.gameObject.SetActive(true);
            drawCardButton.onClick.AddListener(OnDrawCardClicked);
        }
        else
        {
            // インスペクタ未設定時は動的生成
            drawCardButton = CreateDrawButton();
            if (drawCardButton != null)
                drawCardButton.onClick.AddListener(OnDrawCardClicked);
        }

        // ドローボタンAP演出UI要素を追加
        if (drawCardButton != null)
            SetupDrawButtonAPElements();

        // 「逃げる」ボタンを動的生成
        CreateFleeButton();

        // 各種ボタンにSE（ホバー／クリック）を追加
        AddButtonSE(endTurnButton);
        AddButtonSE(fusionButton);
        AddButtonSE(drawCardButton);

        // プレイヤーHPバーは廃止（シールドシステムに一本化）
        if (playerHPText != null) playerHPText.gameObject.SetActive(false);
        if (enemyHPBar == null && enemyHPText != null)
            enemyHPBar = CreateHPBar(enemyHPText.transform, true);

        // シールドUIを生成
        CreateShieldUI();

        // テキスト要素にOutlineを追加して視認性向上
        AddTextOutline(playerHPText, new Color(0f, 0.2f, 0f, 0.85f));
        AddTextOutline(playerManaText, new Color(0f, 0f, 0.3f, 0.85f));
        AddTextOutline(enemyNameText, new Color(0.3f, 0f, 0f, 0.85f));
        AddTextOutline(enemyHPText, new Color(0.3f, 0f, 0f, 0.85f));
        AddTextOutline(enemyKanjiText, new Color(0.4f, 0f, 0f, 0.9f), 2f);
        AddTextOutline(battleLogText, new Color(0f, 0f, 0f, 0.8f));
    }

    /// <summary>
    /// ボタンにホバー／クリック時のSEを付与
    /// </summary>
    private void AddButtonSE(Button btn)
    {
        if (btn == null) return;

        // クリックSE
        btn.onClick.AddListener(() => {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySE(AudioManager.Instance.seButton44);
        });

        // ホバーSE
        var trigger = btn.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        var entry = new UnityEngine.EventSystems.EventTrigger.Entry();
        entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        entry.callback.AddListener((data) => {
            if (btn.interactable && AudioManager.Instance != null)
                AudioManager.Instance.PlaySE(AudioManager.Instance.seButton44);
        });
        trigger.triggers.Add(entry);
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
        ovhRect.offsetMin = new Vector2(2f, 2f);  // normalFillと同じrect
        ovhRect.offsetMax = new Vector2(-2f, -2f);
        var ovhImg = ovhObj.AddComponent<Image>();
        ovhImg.type = Image.Type.Filled;
        ovhImg.fillMethod = Image.FillMethod.Horizontal;
        ovhImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        ovhImg.color = new Color(1f, 0.85f, 0f, 0.88f);
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
        iconTmp.overflowMode = TextOverflowModes.Overflow;
        iconTmp.enableWordWrapping = false;
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

        UpdateCardPulses();
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

        // カードグロウオーバーレイ（使用可能時の明滅用 - テキストより下のレイヤー）
        var cardGlowGo = new GameObject("CardGlowOverlay");
        cardGlowGo.transform.SetParent(go.transform, false);
        var cardGlowRect = cardGlowGo.AddComponent<RectTransform>();
        cardGlowRect.anchorMin = Vector2.zero;
        cardGlowRect.anchorMax = Vector2.one;
        cardGlowRect.offsetMin = Vector2.zero;
        cardGlowRect.offsetMax = Vector2.zero;
        var cardGlowImg = cardGlowGo.AddComponent<Image>();
        cardGlowImg.color = new Color(1f, 0.9f, 0.5f, 0f);
        cardGlowImg.raycastTarget = false;
        cardCtrl.glowOverlay = cardGlowImg;

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

        // コンボ・相殺バッジを付与
        AttachBattleBadge(go.transform, data);

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

    /// <summary>
    /// カードにコンボ・相殺バッジを付与
    /// </summary>
    private void AttachBattleBadge(Transform cardTransform, KanjiCardData data)
    {
        var bm = GameManager.Instance?.battleManager;
        if (bm == null || bm.battleState != BattleManager.BattleState.PlayerTurn) return;
        if (data == null) return;

        bool isMirrorClash = bm.currentEnemyData != null && data.kanji == bm.currentEnemyData.displayKanji;
        bool isCombo = !string.IsNullOrEmpty(bm.LastComboKanji) && data.kanji == bm.LastComboKanji;

        if (isMirrorClash)
            CreateBadge(cardTransform, "相殺", new Color(1f, 0.15f, 0.15f, 0.92f));
        else if (isCombo)
            CreateBadge(cardTransform, "コンボ", new Color(0.85f, 0.1f, 0.9f, 0.92f));
    }

    /// <summary>
    /// カード上部にバッジを生成
    /// </summary>
    private void CreateBadge(Transform cardTransform, string text, Color bgColor)
    {
        var badgeGo = new GameObject("BattleBadge");
        badgeGo.transform.SetParent(cardTransform, false);

        var rect = badgeGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.85f);
        rect.anchorMax = new Vector2(1f, 1.0f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var bg = badgeGo.AddComponent<Image>();
        bg.color = bgColor;
        bg.raycastTarget = false;

        var textGo = new GameObject("BadgeText");
        textGo.transform.SetParent(badgeGo.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 15f;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (appFont != null) tmp.font = appFont;
        var tmpRect = textGo.GetComponent<RectTransform>();
        tmpRect.anchorMin = Vector2.zero;
        tmpRect.anchorMax = Vector2.one;
        tmpRect.offsetMin = Vector2.zero;
        tmpRect.offsetMax = Vector2.zero;

        // バッジをカードUI最前面に
        badgeGo.transform.SetAsLastSibling();
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
    /// AP不足時の共通エラーフィードバック（赤点滅 + エラーSE）
    /// </summary>
    public void ShowAPError()
    {
        if (VFXManager.Instance != null && playerManaText != null)
            VFXManager.Instance.PlayAPShortageEffect(playerManaText.GetComponent<RectTransform>());
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySE(AudioManager.Instance.seButton38);
    }

    /// <summary>
    /// ドローボタン押下：AP1消費してカードを1枚引く（AP不足時はエラーフィードバック）
    /// </summary>
    private void OnDrawCardClicked()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.hand.Count >= gm.initialHandSize) return;

        if (gm.playerMana < 1)
        {
            ShowAPError();
            return;
        }

        gm.playerMana -= 1;
        // AP減少直後にUI即更新（表示ズレ防止）
        UpdateStatusUI();
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
    /// 「逃げる」ボタンを動的生成（ターン終了ボタンの下に配置）
    /// </summary>
    private void CreateFleeButton()
    {
        Transform parent = endTurnButton?.transform.parent ?? transform;

        var go = new GameObject("FleeButton");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();

        if (drawCardButton != null)
        {
            var src = drawCardButton.GetComponent<RectTransform>();
            float anchorHeight = src.anchorMax.y - src.anchorMin.y;
            float margin = 0.02f;
            // ドローボタンの**上**に配置
            rect.anchorMin = new Vector2(src.anchorMin.x, src.anchorMax.y + margin);
            rect.anchorMax = new Vector2(src.anchorMax.x, src.anchorMax.y + margin + anchorHeight);
            rect.sizeDelta = src.sizeDelta;
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        else if (endTurnButton != null)
        {
            var src = endTurnButton.GetComponent<RectTransform>();
            float anchorHeight = src.anchorMax.y - src.anchorMin.y;
            float margin = 0.02f;
            // ドローがない場合はターン終了の**上**に配置
            rect.anchorMin = new Vector2(src.anchorMin.x, src.anchorMax.y + margin);
            rect.anchorMax = new Vector2(src.anchorMax.x, src.anchorMax.y + margin + anchorHeight);
            rect.sizeDelta = src.sizeDelta;
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(120f, 40f);
            rect.anchoredPosition = new Vector2(60f, 20f);
        }

        // ダークレッド
        var img = go.AddComponent<Image>();
        img.color = new Color(0.45f, 0.12f, 0.12f, 0.92f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = img.color;
        colors.highlightedColor = new Color(0.6f, 0.15f, 0.15f, 1f);
        colors.pressedColor     = new Color(0.3f, 0.08f, 0.08f, 1f);
        colors.disabledColor    = new Color(0.25f, 0.1f, 0.1f, 0.5f);
        btn.colors = colors;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "逃げる\n(カード1枚ロスト)";
        tmp.fontSize = 11f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.7f, 0.7f);
        tmp.raycastTarget = false;
        if (appFont != null) tmp.font = appFont;
        var tmpRect = textGo.GetComponent<RectTransform>();
        tmpRect.anchorMin = Vector2.zero;
        tmpRect.anchorMax = Vector2.one;
        tmpRect.offsetMin = Vector2.zero;
        tmpRect.offsetMax = Vector2.zero;

        btn.onClick.AddListener(OnFleeClicked);
        AddButtonSE(btn);
        _fleeButton = btn;
    }

    /// <summary>
    /// 逃げるボタン押下
    /// </summary>
    private void OnFleeClicked()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.battleManager == null) return;
        gm.battleManager.FleeFromBattle();
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

        // プレイヤーHP廃止 - HP表示を非表示
        if (playerHPText != null) playerHPText.gameObject.SetActive(false);

        if (playerManaText != null)
        {
            playerManaText.gameObject.SetActive(true);
            string buffText = "";
            if (gm.playerAttackBuff > 0) buffText += $" 攻↑+{gm.playerAttackBuff}";
            if (gm.playerDefenseBuff > 0) buffText += $" 防↑+{gm.playerDefenseBuff}";
            playerManaText.text = $"AP: {gm.playerMana}/{gm.playerMaxMana}{buffText}";
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
                string enemyStatus = gm.battleManager.enemyIsStunned ? "<color=#FFD700>[スタン]</color> " : "";
                foreach (var effect in gm.battleManager.enemyStatusEffects)
                {
                    if (effect.Type == StatusType.Poison) enemyStatus += $"<color=#9932CC>[毒{effect.Duration}T]</color> ";
                    if (effect.Type == StatusType.Regen) enemyStatus += $"<color=#32CD32>[癒{effect.Duration}T]</color> ";
                }
                enemyHPBar.SetStatusIcon(enemyStatus.Trim());
            }
        }

        // シールドコンテナが未生成なら再作成（遅延初期化対応）
        if (shieldContainer == null) CreateShieldUI();
        UpdateShieldUI();

        UpdateDrawButtonAPState();
        UpdateCardPulses();
    }

    /// <summary>
    /// シールドUIコンテナを生成（HPバーの下に3枚の裏向きカード）
    /// </summary>
    private void CreateShieldUI()
    {
        // MainCanvas（BattlePanel親）を優先して配置
        Canvas canvas = null;
        // playerHPTextのCanvasを優先（同じ描画階層に配置）
        if (playerHPText != null)
            canvas = playerHPText.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            // FindObjectsByTypeで全Canvas取得、MainCanvasを探す
            var allCanvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvas)
                if (c.name == "MainCanvas" || c.isRootCanvas && c.sortingOrder == 0) { canvas = c; break; }
            if (canvas == null && allCanvas.Length > 0) canvas = allCanvas[0];
        }
        if (canvas == null) return;

        var containerGo = new GameObject("ShieldContainer");
        containerGo.transform.SetParent(canvas.transform, false);

        var containerRect = containerGo.AddComponent<RectTransform>();
        // 画面左上に小型シールドを並べる（HPバー廃止により上に移動）
        containerRect.anchorMin = new Vector2(0f, 1f);
        containerRect.anchorMax = new Vector2(0f, 1f);
        containerRect.pivot = new Vector2(0f, 1f);
        containerRect.anchoredPosition = new Vector2(8f, -60f);
        containerRect.sizeDelta = new Vector2(500f, 60f);

        var hLayout = containerGo.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        hLayout.spacing = 4f;
        hLayout.childAlignment = TextAnchor.MiddleLeft;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;
        hLayout.padding = new RectOffset(2, 2, 2, 2);

        shieldContainer = containerGo.transform;
    }

    /// <summary>
    /// シールドUI更新（最大10個表示、超過分は「+N」テキストで表示）
    /// </summary>
    public void UpdateShieldUI()
    {
        if (shieldContainer == null) return;

        foreach (var obj in shieldUIObjects)
            if (obj != null) Destroy(obj);
        shieldUIObjects.Clear();

        var gm = GameManager.Instance;
        if (gm == null) return;

        const int maxDisplayShields = 10;
        int actualCount = gm.shields.Count;
        int displayCount = Mathf.Min(actualCount, maxDisplayShields);
        int excessCount = actualCount - displayCount;

        for (int i = 0; i < displayCount; i++)
        {
            var shieldGo = new GameObject($"Shield_{i}");
            shieldGo.transform.SetParent(shieldContainer, false);

            var rect = shieldGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(38f, 50f);

            var le = shieldGo.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredWidth = 38f;
            le.preferredHeight = 50f;
            le.minWidth = 38f;
            le.minHeight = 50f;

            var bg = shieldGo.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.1f, 0.3f, 0.7f, 0.92f);

            var border = shieldGo.AddComponent<Outline>();
            border.effectColor = new Color(0.4f, 0.7f, 1f, 0.9f);
            border.effectDistance = new Vector2(1.5f, -1.5f);

            var textGo = new GameObject("ShieldText");
            textGo.transform.SetParent(shieldGo.transform, false);
            var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "?";
            tmp.fontSize = 22f;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(0.7f, 0.9f, 1f, 1f);
            tmp.raycastTarget = false;
            if (appFont != null) tmp.font = appFont;
            var tmpRect = textGo.GetComponent<RectTransform>();
            tmpRect.anchorMin = Vector2.zero;
            tmpRect.anchorMax = Vector2.one;
            tmpRect.offsetMin = Vector2.zero;
            tmpRect.offsetMax = Vector2.zero;

            shieldUIObjects.Add(shieldGo);
        }

        // 超過分を「+N」テキストで表示
        if (excessCount > 0)
        {
            var excessGo = new GameObject("ShieldExcess");
            excessGo.transform.SetParent(shieldContainer, false);
            var excessRect = excessGo.AddComponent<RectTransform>();
            excessRect.sizeDelta = new Vector2(48f, 50f);
            var exLe = excessGo.AddComponent<UnityEngine.UI.LayoutElement>();
            exLe.preferredWidth = 48f;
            exLe.preferredHeight = 50f;
            var exTmp = excessGo.AddComponent<TMPro.TextMeshProUGUI>();
            exTmp.text = $"+{excessCount}";
            exTmp.fontSize = 22f;
            exTmp.fontStyle = TMPro.FontStyles.Bold;
            exTmp.alignment = TMPro.TextAlignmentOptions.Left;
            exTmp.color = new Color(0.6f, 0.9f, 1f, 1f);
            exTmp.raycastTarget = false;
            if (appFont != null) exTmp.font = appFont;
            shieldUIObjects.Add(excessGo);
        }

        // シールドが0枚の場合は「シールド0」と赤く表示
        if (actualCount == 0)
        {
            var emptyGo = new GameObject("ShieldZero");
            emptyGo.transform.SetParent(shieldContainer, false);
            var emptyRect = emptyGo.AddComponent<RectTransform>();
            emptyRect.sizeDelta = new Vector2(90f, 50f);
            var emLe = emptyGo.AddComponent<UnityEngine.UI.LayoutElement>();
            emLe.preferredWidth = 90f;
            emLe.preferredHeight = 50f;
            var emTmp = emptyGo.AddComponent<TMPro.TextMeshProUGUI>();
            emTmp.text = "盾 0枚";
            emTmp.fontSize = 18f;
            emTmp.fontStyle = TMPro.FontStyles.Bold;
            emTmp.alignment = TMPro.TextAlignmentOptions.Left;
            emTmp.color = new Color(1f, 0.4f, 0.4f, 0.9f);
            emTmp.raycastTarget = false;
            if (appFont != null) emTmp.font = appFont;
            shieldUIObjects.Add(emptyGo);
        }
    }

    /// <summary>
    /// ドローボタンにAP演出UI要素（グロウ・横線・警告テキスト）を追加
    /// </summary>
    private void SetupDrawButtonAPElements()
    {
        var parent = drawCardButton.transform;

        // ホワイトグロウオーバーレイ（明滅用）
        var glowGo = new GameObject("GlowOverlay");
        glowGo.transform.SetParent(parent, false);
        var glowRect = glowGo.AddComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = Vector2.zero;
        glowRect.offsetMax = Vector2.zero;
        _drawButtonGlowOverlay = glowGo.AddComponent<Image>();
        _drawButtonGlowOverlay.color = new Color(1f, 1f, 1f, 0f);
        _drawButtonGlowOverlay.raycastTarget = false;
        glowGo.transform.SetAsLastSibling();

        // 取り消し横線
        var strikeGo = new GameObject("APStrikeline");
        strikeGo.transform.SetParent(parent, false);
        var strikeRect = strikeGo.AddComponent<RectTransform>();
        strikeRect.anchorMin = new Vector2(0.05f, 0.42f);
        strikeRect.anchorMax = new Vector2(0.95f, 0.58f);
        strikeRect.offsetMin = Vector2.zero;
        strikeRect.offsetMax = Vector2.zero;
        var strikeImg = strikeGo.AddComponent<Image>();
        strikeImg.color = new Color(1f, 0.25f, 0.25f, 0.9f);
        strikeImg.raycastTarget = false;
        _drawButtonStrikeline = strikeGo;

        // 「行動値不足」警告テキスト
        var warnGo = new GameObject("APWarningText");
        warnGo.transform.SetParent(parent, false);
        var warnRect = warnGo.AddComponent<RectTransform>();
        warnRect.anchorMin = new Vector2(0f, 0f);
        warnRect.anchorMax = new Vector2(1f, 0.42f);
        warnRect.offsetMin = new Vector2(2f, 2f);
        warnRect.offsetMax = new Vector2(-2f, 0f);
        _drawButtonWarningText = warnGo.AddComponent<TextMeshProUGUI>();
        _drawButtonWarningText.text = "行動値不足";
        _drawButtonWarningText.fontSize = 10f;
        _drawButtonWarningText.color = new Color(1f, 0.3f, 0.3f, 1f);
        _drawButtonWarningText.fontStyle = TMPro.FontStyles.Bold;
        _drawButtonWarningText.alignment = TextAlignmentOptions.Center;
        _drawButtonWarningText.raycastTarget = false;
        if (appFont != null) _drawButtonWarningText.font = appFont;

        _drawButtonStrikeline.SetActive(false);
        _drawButtonWarningText.gameObject.SetActive(false);
    }

    /// <summary>
    /// ドローボタンのAP状態に応じたUI（明滅・横線・警告）を更新
    /// </summary>
    private void UpdateDrawButtonAPState()
    {
        if (drawCardButton == null) return;
        var gm = GameManager.Instance;
        bool playerTurn = gm?.battleManager != null &&
                          gm.battleManager.battleState == BattleManager.BattleState.PlayerTurn;
        bool hasAP = gm != null && gm.playerMana >= 1;
        bool apShort = !hasAP;
        bool canPulse = playerTurn && hasAP;

        // AP不足UI表示切り替え
        if (_drawButtonStrikeline != null) _drawButtonStrikeline.SetActive(apShort);
        if (_drawButtonWarningText != null) _drawButtonWarningText.gameObject.SetActive(apShort);

        // ボタン半透明化
        var img = drawCardButton.GetComponent<Image>();
        if (img != null)
        {
            var c = img.color;
            c.a = apShort ? 0.5f : 0.92f;
            img.color = c;
        }

        // 明滅コルーチン制御
        if (canPulse)
        {
            if (_drawPulseCoroutine == null)
                _drawPulseCoroutine = StartCoroutine(PulseDrawButton());
        }
        else
        {
            if (_drawPulseCoroutine != null)
            {
                StopCoroutine(_drawPulseCoroutine);
                _drawPulseCoroutine = null;
            }
            if (_drawButtonGlowOverlay != null)
            {
                var c = _drawButtonGlowOverlay.color;
                c.a = 0f;
                _drawButtonGlowOverlay.color = c;
            }
        }
    }

    /// <summary>
    /// ドローボタンをふわっと光らせる（AP1以上・プレイヤーターン時）
    /// </summary>
    private System.Collections.IEnumerator PulseDrawButton()
    {
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(1.8f, 2.5f));

            float dur = 0.45f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                if (_drawButtonGlowOverlay != null)
                {
                    var c = _drawButtonGlowOverlay.color;
                    c.a = Mathf.Lerp(0f, 0.28f, elapsed / dur);
                    _drawButtonGlowOverlay.color = c;
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.15f);

            elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                if (_drawButtonGlowOverlay != null)
                {
                    var c = _drawButtonGlowOverlay.color;
                    c.a = Mathf.Lerp(0.28f, 0f, elapsed / dur);
                    _drawButtonGlowOverlay.color = c;
                }
                yield return null;
            }

            if (_drawButtonGlowOverlay != null)
            {
                var c = _drawButtonGlowOverlay.color;
                c.a = 0f;
                _drawButtonGlowOverlay.color = c;
            }
        }
    }

    /// <summary>
    /// 手札カードの使用可能状態に応じて明滅演出を開始／停止
    /// </summary>
    private void UpdateCardPulses()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        bool playerTurn = gm.battleManager != null &&
                          gm.battleManager.battleState == BattleManager.BattleState.PlayerTurn;

        foreach (var card in handCards)
        {
            if (card == null) continue;
            bool canUse = false;
            if (playerTurn && card.cardData != null)
            {
                int cost = GameManager.GetCardAPCost(card.cardData);
                canUse = cost == 0 || gm.playerMana >= cost;
            }

            if (canUse) card.StartGlowPulse();
            else card.StopGlowPulse();
        }
    }

    /// <summary>
    /// 店カード使用時：5枚のランダムカードを選択UIで表示
    /// </summary>
    public void ShowShopSelection()
    {
        if (shopSelectionPanel != null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        // 全カードから5枚ランダムに候補を作成
        var allCards = Resources.FindObjectsOfTypeAll<KanjiCardData>();
        var candidates = new System.Collections.Generic.List<KanjiCardData>(allCards);
        // シャッフル
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
        }
        // 最大5枚
        int count = Mathf.Min(5, candidates.Count);

        // オーバーレイパネル
        shopSelectionPanel = new GameObject("ShopSelectionPanel");
        shopSelectionPanel.transform.SetParent(canvas.transform, false);
        shopSelectionPanel.transform.SetAsLastSibling();

        var overlayRect = shopSelectionPanel.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var overlayBg = shopSelectionPanel.AddComponent<UnityEngine.UI.Image>();
        overlayBg.color = new Color(0f, 0f, 0f, 0.7f);

        // タイトル
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(shopSelectionPanel.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.7f);
        titleRect.anchorMax = new Vector2(0.9f, 0.88f);
        titleRect.offsetMin = Vector2.zero; titleRect.offsetMax = Vector2.zero;
        var titleTmp = titleGo.AddComponent<TMPro.TextMeshProUGUI>();
        titleTmp.text = "★ 店 ★\n1枚選んで手札に加えよう！";
        titleTmp.fontSize = 28f;
        titleTmp.alignment = TMPro.TextAlignmentOptions.Center;
        titleTmp.color = new Color(1f, 0.92f, 0.3f);
        if (appFont != null) titleTmp.font = appFont;

        // カードパネル（横一列）
        var cardRowGo = new GameObject("CardRow");
        cardRowGo.transform.SetParent(shopSelectionPanel.transform, false);
        var cardRowRect = cardRowGo.AddComponent<RectTransform>();
        cardRowRect.anchorMin = new Vector2(0.05f, 0.25f);
        cardRowRect.anchorMax = new Vector2(0.95f, 0.68f);
        cardRowRect.offsetMin = Vector2.zero; cardRowRect.offsetMax = Vector2.zero;

        var hLayout = cardRowGo.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        hLayout.spacing = 12f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;

        for (int i = 0; i < count; i++)
        {
            var cardData = candidates[i];
            var cardGo = new GameObject($"ShopCard_{cardData.kanji}");
            cardGo.transform.SetParent(cardRowGo.transform, false);

            var crect = cardGo.AddComponent<RectTransform>();
            crect.sizeDelta = new Vector2(110f, 150f);

            var cbg = cardGo.AddComponent<UnityEngine.UI.Image>();
            cbg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);

            var cOutline = cardGo.AddComponent<Outline>();
            cOutline.effectColor = new Color(0.8f, 0.7f, 0.2f, 0.9f);
            cOutline.effectDistance = new Vector2(3f, -3f);

            var kanjiGo = new GameObject("Kanji");
            kanjiGo.transform.SetParent(cardGo.transform, false);
            var kTmp = kanjiGo.AddComponent<TMPro.TextMeshProUGUI>();
            kTmp.text = cardData.kanji;
            kTmp.fontSize = 44f;
            kTmp.alignment = TMPro.TextAlignmentOptions.Center;
            kTmp.color = Color.white;
            kTmp.raycastTarget = false;
            if (appFont != null) kTmp.font = appFont;
            var kRect = kanjiGo.GetComponent<RectTransform>();
            kRect.anchorMin = new Vector2(0f, 0.35f); kRect.anchorMax = new Vector2(1f, 0.88f);
            kRect.offsetMin = Vector2.zero; kRect.offsetMax = Vector2.zero;

            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(cardGo.transform, false);
            var dTmp = descGo.AddComponent<TMPro.TextMeshProUGUI>();
            dTmp.text = cardData.effectType.ToString();
            dTmp.fontSize = 12f;
            dTmp.alignment = TMPro.TextAlignmentOptions.Center;
            dTmp.color = new Color(0.8f, 0.8f, 0.8f);
            dTmp.raycastTarget = false;
            if (appFont != null) dTmp.font = appFont;
            var dRect = descGo.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0f, 0f); dRect.anchorMax = new Vector2(1f, 0.32f);
            dRect.offsetMin = new Vector2(3f, 3f); dRect.offsetMax = new Vector2(-3f, 0f);

            // クリックで選択
            var btn = cardGo.AddComponent<UnityEngine.UI.Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.9f, 0.85f, 0.3f, 1f);
            btn.colors = btnColors;
            var capturedCard = cardData;
            var capturedPanel = shopSelectionPanel;
            btn.onClick.AddListener(() =>
            {
                var gm2 = GameManager.Instance;
                if (gm2 != null)
                {
                    if (gm2.hand.Count < gm2.initialHandSize)
                        gm2.hand.Add(capturedCard);
                    else
                        gm2.drawPile.Add(capturedCard);
                    gm2.AddToInventory(capturedCard);
                    gm2.battleManager?.AddBattleLog($"<color=#FFD700>『{capturedCard.kanji}』を手に入れた！</color>");
                }
                Destroy(capturedPanel);
                shopSelectionPanel = null;
                UpdateHandUI();
                UpdateStatusUI();
            });
        }
    }
}
