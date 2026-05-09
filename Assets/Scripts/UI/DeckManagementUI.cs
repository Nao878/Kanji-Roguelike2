using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Linq;

/// <summary>
/// ドラッグ＆ドロップ対応のカードコンポーネント
/// </summary>
public class DraggableCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public KanjiCardData cardData;
    public bool isDeckItem;
    public DeckManagementUI uiRef;

    private Vector2 originalPosition;
    private Transform originalParent;
    private int originalSiblingIndex;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = transform.position;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        // Canvasの最前面に移動
        transform.SetParent(uiRef.transform, true);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        bool droppedOnTarget = false;
        foreach (var result in results)
        {
            if (isDeckItem && result.gameObject.name == "InventoryPanel")
            {
                uiRef.RemoveFromDeck(cardData);
                droppedOnTarget = true;
                break;
            }
            else if (!isDeckItem && result.gameObject.name == "DeckPanel")
            {
                if (uiRef.CanAddToDeck(cardData))
                {
                    uiRef.AddToDeck(cardData);
                }
                droppedOnTarget = true;
                break;
            }
        }

        if (!droppedOnTarget)
        {
            // 元の位置に戻す
            transform.SetParent(originalParent, false);
            transform.SetSiblingIndex(originalSiblingIndex);
            transform.position = originalPosition;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;

        if (isDeckItem)
        {
            uiRef.RemoveFromDeck(cardData);
        }
        else
        {
            if (uiRef.CanAddToDeck(cardData))
            {
                uiRef.AddToDeck(cardData);
            }
        }
    }
}

/// <summary>
/// デッキ編成UI
/// インベントリ（全所持カード）から20枚を選んでデッキを構築する
/// </summary>
public class DeckManagementUI : MonoBehaviour
{
    [Header("UI参照: インベントリ側")]
    public Transform inventoryScrollContent;
    public TextMeshProUGUI inventoryCountText;

    [Header("UI参照: デッキ側")]
    public Transform deckScrollContent;
    public TextMeshProUGUI deckCountText;
    public TextMeshProUGUI deckValidationText;

    [Header("UI参照: その他")]
    public Button saveAndExitButton;
    public Button autoFillButton;
    public Button closeButton;
    public TMP_FontAsset appFont;

    private List<GameObject> inventoryItems = new List<GameObject>();
    private List<GameObject> deckItems = new List<GameObject>();

    private void Awake()
    {
        AutoDetectFont();
        BuildUIIfNeeded();
    }

    private void AutoDetectFont()
    {
        if (appFont != null) return;
        var fieldManager = FindObjectOfType<FieldManager>();
        if (fieldManager != null && fieldManager.appFont != null)
        {
            appFont = fieldManager.appFont;
            return;
        }
        var audioManager = FindObjectOfType<AudioManager>();
        if (audioManager != null && audioManager.appFont != null)
        {
            appFont = audioManager.appFont;
            return;
        }
        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var f in fonts)
        {
            if (f.name.Contains("AppFont") || f.name.Contains("SDF") || f.name.Contains("JP"))
            { appFont = f; break; }
        }
    }

    private void ApplyFontToAll()
    {
        if (appFont == null) return;
        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
            tmp.font = appFont;
    }

    public void BuildUIIfNeeded()
    {
        if (inventoryScrollContent != null && deckScrollContent != null) return;

        var titleGo = MakeText(transform, "デッキ編成", 26f, new Color(1f, 0.9f, 0.5f), "Title");
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.92f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        var leftBg = MakePanel(transform, new Vector2(0f, 0.08f), new Vector2(0.5f, 0.92f),
                               new Color(0.1f, 0.1f, 0.18f, 0.85f), "InventoryPanel");

        var invHeader = MakeText(leftBg.transform, "インベントリ", 16f, new Color(0.7f, 0.85f, 1f), "Header");
        { var r = invHeader.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0f, 0.92f); r.anchorMax = new Vector2(1f, 1f); r.offsetMin = r.offsetMax = Vector2.zero; }

        var icGo = MakeText(leftBg.transform, "所持品: 0枚", 13f, Color.white, "InvCount");
        inventoryCountText = icGo.GetComponent<TextMeshProUGUI>();
        { var r = icGo.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0f, 0.84f); r.anchorMax = new Vector2(1f, 0.92f); r.offsetMin = r.offsetMax = Vector2.zero; }

        inventoryScrollContent = MakeScrollView(leftBg.transform,
            new Vector2(0f, 0.02f), new Vector2(1f, 0.84f));

        var rightBg = MakePanel(transform, new Vector2(0.5f, 0.08f), new Vector2(1f, 0.92f),
                                new Color(0.1f, 0.18f, 0.1f, 0.85f), "DeckPanel");

        var deckHeader = MakeText(rightBg.transform, "デッキ", 16f, new Color(0.7f, 1f, 0.7f), "Header");
        { var r = deckHeader.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0f, 0.92f); r.anchorMax = new Vector2(1f, 1f); r.offsetMin = r.offsetMax = Vector2.zero; }

        var dcGo = MakeText(rightBg.transform, "デッキ: 0 / 20枚", 13f, Color.white, "DeckCount");
        deckCountText = dcGo.GetComponent<TextMeshProUGUI>();
        { var r = dcGo.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0f, 0.84f); r.anchorMax = new Vector2(0.65f, 0.92f); r.offsetMin = r.offsetMax = Vector2.zero; }

        var dvGo = MakeText(rightBg.transform, "", 12f, Color.yellow, "DeckVal");
        deckValidationText = dvGo.GetComponent<TextMeshProUGUI>();
        deckValidationText.enableWordWrapping = false;
        { var r = dvGo.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0.55f, 0.84f); r.anchorMax = new Vector2(1f, 0.92f); r.offsetMin = r.offsetMax = Vector2.zero; }

        deckScrollContent = MakeScrollView(rightBg.transform,
            new Vector2(0f, 0.02f), new Vector2(1f, 0.84f));

        var btnBg = new GameObject("Buttons");
        btnBg.transform.SetParent(transform, false);
        var btnRect = btnBg.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0f, 0f);
        btnRect.anchorMax = new Vector2(1f, 0.08f);
        btnRect.offsetMin = new Vector2(16f, 6f);
        btnRect.offsetMax = new Vector2(-16f, -4f);

        autoFillButton  = MakeButton(btnBg.transform, "自動補填",
            new Vector2(0f, 0f), new Vector2(0.30f, 1f), new Color(0.25f, 0.45f, 0.75f));
        saveAndExitButton = MakeButton(btnBg.transform, "保存して戻る",
            new Vector2(0.33f, 0f), new Vector2(0.66f, 1f), new Color(0.15f, 0.6f, 0.25f));
        closeButton = MakeButton(btnBg.transform, "閉じる",
            new Vector2(0.69f, 0f), new Vector2(1f, 1f), new Color(0.55f, 0.15f, 0.15f));
    }

    private GameObject MakeText(Transform parent, string text, float size, Color color, string name = "Text")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        if (appFont != null) tmp.font = appFont;
        return go;
    }

    private GameObject MakePanel(Transform parent, Vector2 aMin, Vector2 aMax, Color color, string name = "Panel")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = aMin; rect.anchorMax = aMax;
        rect.offsetMin = new Vector2(4f, 4f);
        rect.offsetMax = new Vector2(-4f, -4f);
        go.AddComponent<Image>().color = color;
        
        // レイキャストのターゲットになるよう設定
        go.GetComponent<Image>().raycastTarget = true;
        
        return go;
    }

    private Transform MakeScrollView(Transform parent, Vector2 aMin, Vector2 aMax)
    {
        var svGo = new GameObject("ScrollView");
        svGo.transform.SetParent(parent, false);
        var svRect = svGo.AddComponent<RectTransform>();
        svRect.anchorMin = aMin; svRect.anchorMax = aMax;
        svRect.offsetMin = new Vector2(4f, 4f);
        svRect.offsetMax = new Vector2(-4f, -4f);
        svGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

        var vpGo = new GameObject("Viewport");
        vpGo.transform.SetParent(svGo.transform, false);
        var vpRect = vpGo.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero; vpRect.offsetMax = Vector2.zero;
        vpGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        var mask = vpGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(vpGo.transform, false);
        var contentRect = contentGo.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot   = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        var glg = contentGo.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(80f, 110f);
        glg.spacing  = new Vector2(5f, 5f);
        glg.padding  = new RectOffset(5, 5, 5, 5);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;
        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = svGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.viewport  = vpRect;
        scroll.content   = contentRect;

        return contentRect.transform;
    }

    private Button MakeButton(Transform parent, string label, Vector2 aMin, Vector2 aMax, Color color)
    {
        var go = new GameObject($"Btn_{label}");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = aMin; rect.anchorMax = aMax;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
        var btn = go.AddComponent<Button>();

        var tGo = new GameObject("Label");
        tGo.transform.SetParent(go.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 15f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (appFont != null) tmp.font = appFont;
        var tr = tGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;

        return btn;
    }

    private void Start()
    {
        if (saveAndExitButton != null) saveAndExitButton.onClick.AddListener(OnSaveAndExit);
        if (autoFillButton != null) autoFillButton.onClick.AddListener(OnAutoFill);
        if (closeButton != null) closeButton.onClick.AddListener(OnClose);
    }

    private void OnEnable()
    {
        AutoDetectFont();
        RefreshUI();
        ApplyFontToAll();
    }

    public void RefreshUI()
    {
        var gm = GameManager.Instance;
        var dm = DeckManager.Instance ?? gm?.deckManager;
        if (gm == null || dm == null) return;

        ClearList(inventoryItems, inventoryScrollContent);
        ClearList(deckItems, deckScrollContent);

        // インベントリの中で、まだデッキに入っていないカードを抽出
        var availableCards = new List<KanjiCardData>(gm.inventory);
        foreach (var card in dm.currentDeck)
        {
            var match = availableCards.FirstOrDefault(c => c.cardId == card.cardId);
            if (match != null) availableCards.Remove(match);
        }

        foreach (var card in availableCards)
        {
            CreateInventoryItem(card);
        }

        foreach (var card in dm.currentDeck)
        {
            CreateDeckItem(card);
        }

        UpdateStatusTexts(availableCards.Count);
    }

    private void ClearList(List<GameObject> list, Transform parent)
    {
        foreach (var item in list) if (item != null) Destroy(item);
        list.Clear();
        foreach (Transform child in parent) Destroy(child.gameObject);
    }

    private void UpdateStatusTexts(int availableCount)
    {
        var gm = GameManager.Instance;
        var dm = DeckManager.Instance ?? gm?.deckManager;
        if (gm == null || dm == null) return;
        
        if (inventoryCountText != null) inventoryCountText.text = $"未編成: {availableCount}枚";
        if (deckCountText != null) deckCountText.text = $"デッキ: {dm.currentDeck.Count} / {dm.maxDeckSize}枚";

        if (deckValidationText != null)
        {
            if (dm.currentDeck.Count < dm.minDeckSize)
            {
                deckValidationText.text = $"あと <color=red>{dm.minDeckSize - dm.currentDeck.Count}枚</color> 必要です";
                saveAndExitButton.interactable = false;
            }
            else if (dm.currentDeck.Count > dm.maxDeckSize)
            {
                deckValidationText.text = $"枚数が多すぎます！ (<color=red>{dm.currentDeck.Count}</color>)";
                saveAndExitButton.interactable = false;
            }
            else
            {
                deckValidationText.text = "<color=green>デッキ枚数OK!</color>";
                saveAndExitButton.interactable = true;
            }
        }
    }

    private void CreateInventoryItem(KanjiCardData card)
    {
        var go = CreateBaseCardUI(card, inventoryScrollContent);
        var draggable = go.AddComponent<DraggableCardUI>();
        draggable.cardData = card;
        draggable.isDeckItem = false;
        draggable.uiRef = this;
        inventoryItems.Add(go);
    }

    private void CreateDeckItem(KanjiCardData card)
    {
        var go = CreateBaseCardUI(card, deckScrollContent);
        var draggable = go.AddComponent<DraggableCardUI>();
        draggable.cardData = card;
        draggable.isDeckItem = true;
        draggable.uiRef = this;
        deckItems.Add(go);
    }

    private GameObject CreateBaseCardUI(KanjiCardData data, Transform parent)
    {
        var go = new GameObject($"Card_{data.kanji}");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(80, 110);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        go.AddComponent<CanvasGroup>(); // For drag and drop raycast blocking

        var kanjiGo = new GameObject("Kanji");
        kanjiGo.transform.SetParent(go.transform, false);
        var kanjiText = kanjiGo.AddComponent<TextMeshProUGUI>();
        kanjiText.text = data.kanji;
        kanjiText.fontSize = 32;
        kanjiText.alignment = TextAlignmentOptions.Center;
        kanjiText.color = Color.white;
        kanjiText.raycastTarget = false;
        if (appFont != null) kanjiText.font = appFont;
        var kanjiRect = kanjiGo.GetComponent<RectTransform>();
        kanjiRect.anchorMin = Vector2.zero;
        kanjiRect.anchorMax = Vector2.one;
        kanjiRect.offsetMin = Vector2.zero;
        kanjiRect.offsetMax = Vector2.zero;

        return go;
    }

    private DeckManager GetDM() => DeckManager.Instance ?? GameManager.Instance?.deckManager;

    public bool CanAddToDeck(KanjiCardData card)
    {
        var dm = GetDM();
        return dm != null && dm.currentDeck.Count < dm.maxDeckSize;
    }

    public void AddToDeck(KanjiCardData card)
    {
        if (!CanAddToDeck(card)) return;
        var dm = GetDM();
        if (dm != null)
        {
            if (dm.AddCardToDeck(card)) RefreshUI();
        }
    }

    public void RemoveFromDeck(KanjiCardData card)
    {
        GetDM()?.RemoveCardFromDeck(card);
        RefreshUI();
    }

    private void OnAutoFill()
    {
        GetDM()?.AutoFillDeck(GameManager.Instance.inventory);
        RefreshUI();
    }

    private void OnSaveAndExit()
    {
        if (GetDM()?.IsDeckValid() == true)
        {
            GameManager.Instance.ChangeState(GameState.Field);
        }
    }

    private void OnClose()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(GameState.Field);
    }
}
