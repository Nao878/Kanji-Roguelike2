using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

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
    public TMP_FontAsset appFont;

    private List<GameObject> inventoryItems = new List<GameObject>();
    private List<GameObject> deckItems = new List<GameObject>();

    private void Awake()
    {
        BuildUIIfNeeded();
    }

    /// <summary>
    /// UI参照が未設定なら全パーツをコードで生成する
    /// </summary>
    public void BuildUIIfNeeded()
    {
        if (inventoryScrollContent != null && deckScrollContent != null) return;

        // ── タイトル ──────────────────────────────────────────────────────
        var titleGo = MakeText(transform, "デッキ編成", 26f, new Color(1f, 0.9f, 0.5f), "Title");
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.92f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // ── 左パネル（インベントリ）──────────────────────────────────────
        var leftBg = MakePanel(transform, new Vector2(0f, 0.08f), new Vector2(0.5f, 0.92f),
                               new Color(0.1f, 0.1f, 0.18f, 0.85f), "InventoryPanel");

        var invHeader = MakeText(leftBg.transform, "インベントリ", 16f, new Color(0.7f, 0.85f, 1f), "Header");
        { var r = invHeader.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0f, 0.92f); r.anchorMax = new Vector2(1f, 1f); r.offsetMin = r.offsetMax = Vector2.zero; }

        var icGo = MakeText(leftBg.transform, "所持品: 0枚", 13f, Color.white, "InvCount");
        inventoryCountText = icGo.GetComponent<TextMeshProUGUI>();
        { var r = icGo.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0f, 0.84f); r.anchorMax = new Vector2(1f, 0.92f); r.offsetMin = r.offsetMax = Vector2.zero; }

        inventoryScrollContent = MakeScrollView(leftBg.transform,
            new Vector2(0f, 0.02f), new Vector2(1f, 0.84f));

        // ── 右パネル（デッキ）──────────────────────────────────────────────
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

        // ── ボタン行 ────────────────────────────────────────────────────
        var btnBg = new GameObject("Buttons");
        btnBg.transform.SetParent(transform, false);
        var btnRect = btnBg.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0f, 0f);
        btnRect.anchorMax = new Vector2(1f, 0.08f);
        btnRect.offsetMin = new Vector2(16f, 6f);
        btnRect.offsetMax = new Vector2(-16f, -4f);

        autoFillButton  = MakeButton(btnBg.transform, "自動補填",
            new Vector2(0f, 0f), new Vector2(0.44f, 1f), new Color(0.25f, 0.45f, 0.75f));
        saveAndExitButton = MakeButton(btnBg.transform, "保存して戻る",
            new Vector2(0.56f, 0f), new Vector2(1f, 1f), new Color(0.15f, 0.6f, 0.25f));
    }

    // ── UI生成ヘルパー ──────────────────────────────────────────────────────

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
    }

    private void OnEnable()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        var gm = GameManager.Instance;
        var dm = DeckManager.Instance ?? gm?.deckManager;
        if (gm == null || dm == null) return;

        // リストをクリア
        ClearList(inventoryItems, inventoryScrollContent);
        ClearList(deckItems, deckScrollContent);

        // インベントリの全カードを表示（デッキに入っていないものだけでなく、全枚数を表示）
        // ただし、デッキ構築は「インベントリから選ぶ」ので、枚数管理が必要
        
        // インベントリを表示
        foreach (var card in gm.inventory)
        {
            CreateInventoryItem(card);
        }

        // デッキを表示
        foreach (var card in dm.currentDeck)
        {
            CreateDeckItem(card);
        }

        UpdateStatusTexts();
    }

    private void ClearList(List<GameObject> list, Transform parent)
    {
        foreach (var item in list) if (item != null) Destroy(item);
        list.Clear();
        // 万が一残っている場合
        foreach (Transform child in parent) Destroy(child.gameObject);
    }

    private void UpdateStatusTexts()
    {
        var gm = GameManager.Instance;
        var dm = DeckManager.Instance ?? gm?.deckManager;
        if (gm == null || dm == null) return;
        
        if (inventoryCountText != null) inventoryCountText.text = $"所持品: {gm.inventory.Count}枚";
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
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => AddToDeck(card));
        inventoryItems.Add(go);
    }

    private void CreateDeckItem(KanjiCardData card)
    {
        var go = CreateBaseCardUI(card, deckScrollContent);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => RemoveFromDeck(card));
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

        var kanjiGo = new GameObject("Kanji");
        kanjiGo.transform.SetParent(go.transform, false);
        var kanjiText = kanjiGo.AddComponent<TextMeshProUGUI>();
        kanjiText.text = data.kanji;
        kanjiText.fontSize = 32;
        kanjiText.alignment = TextAlignmentOptions.Center;
        kanjiText.color = Color.white;
        if (appFont != null) kanjiText.font = appFont;
        var kanjiRect = kanjiGo.GetComponent<RectTransform>();
        kanjiRect.anchorMin = Vector2.zero;
        kanjiRect.anchorMax = Vector2.one;
        kanjiRect.offsetMin = Vector2.zero;
        kanjiRect.offsetMax = Vector2.zero;

        return go;
    }

    private DeckManager GetDM() => DeckManager.Instance ?? GameManager.Instance?.deckManager;

    private void AddToDeck(KanjiCardData card)
    {
        var dm = GetDM();
        if (dm == null) return;
        int countInInventory = GameManager.Instance.inventory.Count(c => c.cardId == card.cardId);
        int countInDeck = dm.currentDeck.Count(c => c.cardId == card.cardId);

        if (countInDeck < countInInventory)
        {
            if (dm.AddCardToDeck(card)) RefreshUI();
        }
        else
        {
            Debug.Log("[DeckManagementUI] そのカードはこれ以上持っていません");
        }
    }

    private void RemoveFromDeck(KanjiCardData card)
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
}
