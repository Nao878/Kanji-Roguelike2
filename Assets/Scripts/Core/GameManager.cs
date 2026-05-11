using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゲーム全体の状態管理（シングルトン）
/// 2D見下ろし型フィールド探索 + 消費型インベントリ方式
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("ゲーム設定")]
    public int playerMaxHP = 50;
    public int playerStartMana = 3;
    public int initialHandSize = 5;
    public int startGold = 50;
    public int fusionCost = 20;
    public int inventoryMaxSize = 30;

    [Header("現在の状態")]
    public GameState currentState = GameState.Field;
    public int playerHP;
    public int playerMana;
    public int playerMaxMana;
    public int playerAttackBuff = 0;
    public int playerDefenseBuff = 0;
    public int playerGold = 0;

    [Header("インベントリ（消費型）")]
    public List<KanjiCardData> inventory = new List<KanjiCardData>();

    [Header("バトル用デッキ（循環システム）")]
    public List<KanjiCardData> drawPile = new List<KanjiCardData>();
    public List<KanjiCardData> discardPile = new List<KanjiCardData>();
    public List<KanjiCardData> hand = new List<KanjiCardData>();

    [Header("参照")]
    public KanjiFusionDatabase fusionDatabase;
    public BattleManager battleManager;
    public MapManager mapManager;
    public FieldManager fieldManager;
    public KanjiFusionEngine fusionEngine;
    public DeckManager deckManager;

    [Header("UI参照")]
    public GameObject mapPanel;
    public GameObject battlePanel;
    public GameObject fusionPanel;
    public GameObject shopPanel;
    public GameObject dojoPanel;
    public GameObject fieldPanel;
    public GameObject deckEditPanel;
    public FusionSelectionUI fusionSelectionUI;

    [Header("ゲームオーバー")]
    public GameObject gameOverPanel;

    [Header("シールドシステム")]
    public List<KanjiCardData> shields = new List<KanjiCardData>();
    public int maxShields = 7;

    [Header("ルートマップ（Slay the Spire型）")]
    public RouteMapManager routeMapManager;

    public void ShowFusionSelectionUI(List<int> resultIds, System.Action<int> onSelected)
    {
        if (fusionSelectionUI != null)
        {
            fusionSelectionUI.ShowSelection(resultIds, onSelected);
        }
    }

    // 合成レシピDictionary（高速検索用、複数結果対応）
    private Dictionary<(int, int), List<int>> fusionRecipeDict = new Dictionary<(int, int), List<int>>();
    // 3枚合体用
    private Dictionary<(int, int, int), List<int>> fusionRecipeDict3 = new Dictionary<(int, int, int), List<int>>();
    // カードIDからカードデータへのマッピング
    private Dictionary<int, KanjiCardData> allCardsDict = new Dictionary<int, KanjiCardData>();
    // 分解用逆引き: 結果ID -> 素材IDList
    private Dictionary<int, List<int>> decomposeDict = new Dictionary<int, List<int>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // シーン開始時の完全初期化（リトライ後も含む）
        playerHP = playerMaxHP;
        currentState = GameState.Field;
        playerMana = playerStartMana;
        playerMaxMana = playerStartMana;
        playerGold = startGold;
        playerAttackBuff = 0;
        playerDefenseBuff = 0;

        drawPile.Clear();
        discardPile.Clear();
        hand.Clear();

        // GameOverPanelを確実に非表示
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    [Header("ヘルプ")]
    public GameObject helpPanel;
    public Button helpButton;

    private void Start()
    {
        InitializeGame();
        WireHelpButton();
        RemoveHelpPanelCloseButton();

        // TitleScreenManagerが未設定なら自動追加（チュートリアルシステム）
        if (GetComponent<TitleScreenManager>() == null)
            gameObject.AddComponent<TitleScreenManager>();
    }

    private void RemoveHelpPanelCloseButton()
    {
        if (helpPanel == null) return;
        var buttons = helpPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (var btn in buttons)
        {
            bool isClose = false;
            var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmp != null)
            {
                string t = tmp.text.Trim();
                if (t == "×" || t == "✕" || t == "x" || t == "X" || t == "✗" || t == "close")
                    isClose = true;
            }
            var img = btn.GetComponent<Image>();
            if (img != null && img.color.r > 0.6f && img.color.g < 0.4f && img.color.b < 0.4f)
                isClose = true;
            if (isClose)
            {
                Debug.Log("[GameManager] HelpPanelの赤いバツボタンを削除しました");
                Destroy(btn.gameObject);
                break;
            }
        }
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.hKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame))
            ToggleHelpPanel();
#else
        if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.F1))
            ToggleHelpPanel();
#endif
    }

    public void ToggleHelpPanel()
    {
        if (helpPanel != null)
        {
            bool isActive = !helpPanel.activeSelf;
            helpPanel.SetActive(isActive);
            Time.timeScale = isActive ? 0f : 1f;
        }
    }

    /// <summary>
    /// ？ボタンにToggleHelpPanelを自動紐付け＆安全な右上隅へ再配置
    /// </summary>
    private void WireHelpButton()
    {
        if (helpButton != null)
        {
            helpButton.onClick.RemoveAllListeners();
            helpButton.onClick.AddListener(ToggleHelpPanel);
            RepositionHelpButton(helpButton);
            Debug.Log("[GameManager] helpButtonをToggleHelpPanelに紐付けました");
            return;
        }

        // インスペクタ未設定時は ? テキストを持つボタンを自動検索
        var buttons = FindObjectsOfType<Button>(true);
        foreach (var btn in buttons)
        {
            var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmp != null && (tmp.text.Trim() == "?" || tmp.text.Trim() == "？"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ToggleHelpPanel);
                helpButton = btn;
                RepositionHelpButton(btn);
                Debug.Log("[GameManager] ？ボタンを自動検出してToggleHelpPanelに紐付けました");
                return;
            }
            // UnityEngine.UI.Text も確認
            var legacyTxt = btn.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (legacyTxt != null && (legacyTxt.text.Trim() == "?" || legacyTxt.text.Trim() == "？"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ToggleHelpPanel);
                helpButton = btn;
                RepositionHelpButton(btn);
                Debug.Log("[GameManager] ？ボタン(Legacy)を自動検出してToggleHelpPanelに紐付けました");
                return;
            }
        }
        Debug.LogWarning("[GameManager] ？ボタンが見つかりませんでした。インスペクタでhelpButtonを設定してください。");
    }

    /// <summary>
    /// ？ボタンを画面右上の安全な隅へ再配置（他UIとの被りを防ぐ）
    /// </summary>
    private void RepositionHelpButton(Button btn)
    {
        if (btn == null) return;
        var rect = btn.GetComponent<RectTransform>();
        if (rect == null) return;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot     = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(62f, 62f);
        rect.anchoredPosition = new Vector2(-10f, -10f);
    }

    /// <summary>
    /// ゲーム初期化
    /// </summary>
    public void InitializeGame()
    {
        playerHP = playerMaxHP;
        playerMaxMana = playerStartMana;
        playerMana = 0; // 戦闘開始時のStartPlayerTurnで加算されるため初期値は0
        playerAttackBuff = 0;
        playerDefenseBuff = 0;
        playerGold = startGold;

        // 引き継ぎ強化をGameManagerに適用（手札・シールド初期値ボーナス）
        PersistenceManager.ApplyInheritUpgrades(this);

        // DeckManagerが未設定なら動的生成
        if (deckManager == null)
        {
            var dmGo = new GameObject("DeckManager");
            deckManager = dmGo.AddComponent<DeckManager>();
            Debug.Log("[GameManager] DeckManagerを動的生成しました");
        }

        // RouteMapManagerが未設定なら自動追加（Slay the Spire型マップ）
        if (routeMapManager == null)
        {
            routeMapManager = gameObject.AddComponent<RouteMapManager>();
        }

        Debug.Log($"[GameManager] ゲーム初期化完了 HP:{playerHP} マナ:{playerMana} インベントリ:{inventory.Count}枚");

        // 合成レシピDictionaryを初期化
        InitializeFusionRecipes();

        // 【強制パッチ】全カードのコスト一律1 & Draw系カードの効果値を2に引き上げ
        EnforceCardBalancePatches();

        // 初期インベントリを図鑑に登録
        if (EncyclopediaManager.Instance != null && inventory != null)
        {
            foreach (var card in inventory)
            {
                EncyclopediaManager.Instance.UnlockCard(card.cardId);
            }
        }

        // デッキを初期化（自動生成）
        if (deckManager != null)
        {
            deckManager.AutoFillDeck(inventory);
        }

        // リトライボタンの初期化
        if (gameOverPanel != null)
        {
            var btn = gameOverPanel.GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ResetGame);
            }
        }

        InitializeDeckEditPanel();
        UpdateHelpPanelText();
        ChangeState(GameState.Field);
    }

    /// <summary>
    /// HelpPanelのテキストをAPシステム説明に更新
    /// </summary>
    private void UpdateHelpPanelText()
    {
        if (helpPanel == null) return;
        var texts = helpPanel.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
        foreach (var t in texts)
        {
            if (t.text.Length > 30 && !t.text.Contains("理解した"))
            {
                t.text =
                    "<b>【行動値 (AP)】</b>\n" +
                    "カードを使ったりドローするにはAPが必要です。\n" +
                    "ターン開始時にAPが<color=#00FFFF>3</color>回復します。\n\n" +
                    "<b>AP消費：1</b>  攻撃・回復・特殊カード\n" +
                    "<b>AP消費：0</b>  バフ・防御・ドローカード\n\n" +
                    "<b>【合体で 1 MORE!】</b>\n" +
                    "漢字を合体させると<color=#FFD700>APが1回復</color>します！\n" +
                    "AP不足でもコンボを繋げるのが攻略の鍵！\n\n" +
                    "<b>【ドロー】</b>\n" +
                    "「ドロー(AP:1)」ボタンで1枚引けます。\n\n" +
                    "<b>【シールド】</b>\n" +
                    "青いカードがシールド。ダメージを1回防ぎ、\n" +
                    "破壊されると手札に加わります（シールドトリガー）。\n\n" +
                    "<b>操作方法：</b>カードを敵にドラッグ→攻撃\n" +
                    "カード同士をドラッグ→合体（漢字合成）";
                break;
            }
        }
    }

    /// <summary>
    /// デッキ編成パネルが未設定なら動的生成
    /// </summary>
    private void InitializeDeckEditPanel()
    {
        if (deckEditPanel != null) return;

        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("DeckEditPanel");
        go.transform.SetParent(canvas.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.08f, 0.97f);
        go.AddComponent<DeckManagementUI>();

        deckEditPanel = go;
        go.SetActive(false);
        Debug.Log("[GameManager] DeckEditPanelを動的生成しました");
    }

    /// <summary>
    /// ゲームステートを変更
    /// </summary>
    public void ChangeState(GameState newState)
    {
        currentState = newState;
        Debug.Log($"[GameManager] ステート変更: {newState}");

        // UIパネルの表示切替
        bool useRouteMap = (routeMapManager != null);
        if (fieldPanel != null) fieldPanel.SetActive(newState == GameState.Field && !useRouteMap);
        if (mapPanel != null) mapPanel.SetActive(false);
        if (battlePanel != null) battlePanel.SetActive(newState == GameState.Battle);
        if (fusionPanel != null) fusionPanel.SetActive(newState == GameState.Fusion);
        if (shopPanel != null) shopPanel.SetActive(newState == GameState.Shop);
        if (dojoPanel != null) dojoPanel.SetActive(newState == GameState.Dojo);
        if (deckEditPanel != null) deckEditPanel.SetActive(newState == GameState.DeckEdit);
        if (gameOverPanel != null) gameOverPanel.SetActive(newState == GameState.GameOver);

        switch (newState)
        {
            case GameState.Field:
                if (routeMapManager != null)
                    routeMapManager.ShowMap();
                else if (fieldManager != null)
                    fieldManager.ShowField();
                break;
            case GameState.Battle:
                // BattleManagerがStartBattleを呼び出す
                break;
            case GameState.Fusion:
                break;
            case GameState.GameOver:
                Debug.Log("[GameManager] ゲームオーバー！");
                PersistenceManager.OnGameOver(routeMapManager);
                if (gameOverPanel != null)
                    gameOverPanel.SetActive(true);
                break;
        }
    }

    /// <summary>
    /// デッキからランダムに手札を引く
    /// </summary>
    /// <summary>
    /// 山札からカードを引く（循環システム対応）
    /// </summary>
    public void DrawFromDeck(int count)
    {
        int drawn = 0;
        for (int i = 0; i < count; i++)
        {
            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0)
                {
                    Debug.Log("[GameManager] 引けるカードがありません");
                    break;
                }
                ShuffleDiscardIntoDrawPile();
            }

            if (drawPile.Count > 0)
            {
                var card = drawPile[0];
                drawPile.RemoveAt(0);
                hand.Add(card);
                drawn++;
            }
        }

        Debug.Log($"[GameManager] 手札引き: {drawn}枚（山札残:{drawPile.Count}枚 捨て札:{discardPile.Count}枚）");
    }

    /// <summary>
    /// 捨て札をシャッフルして山札に戻す
    /// </summary>
    public void ShuffleDiscardIntoDrawPile()
    {
        if (discardPile.Count == 0) return;

        drawPile.AddRange(discardPile);
        discardPile.Clear();

        // フィッシャー–イェーツのシャッフル
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = drawPile[i];
            drawPile[i] = drawPile[j];
            drawPile[j] = temp;
        }
        Debug.Log("[GameManager] 捨て札をシャッフルして山札に戻しました");
    }


    /// <summary>
    /// カードのAPコストを返す（Attack/AttackAll/Heal/Special = 1, その他 = 0）
    /// </summary>
    public static int GetCardAPCost(KanjiCardData card)
    {
        if (card == null) return 0;
        switch (card.effectType)
        {
            case CardEffectType.Attack:
            case CardEffectType.AttackAll:
            case CardEffectType.Heal:
            case CardEffectType.Special:
            case CardEffectType.Stun:
                return 1;
            default:
                return 0;
        }
    }

    /// <summary>
    /// カードを使用（APコスト消費・循環システム：捨て札へ移動）
    /// AP不足の場合はfalseを返す
    /// </summary>
    public bool UseCard(KanjiCardData card)
    {
        int apCost = GetCardAPCost(card);
        if (apCost > 0 && playerMana < apCost)
        {
            Debug.Log($"[GameManager] AP不足！ 必要:{apCost} 現在:{playerMana}");
            return false;
        }
        playerMana -= apCost;
        hand.Remove(card);
        discardPile.Add(card); // 捨て札へ

        Debug.Log($"[GameManager] カード使用: {card.kanji}（AP-{apCost} 残:{playerMana}）");
        return true;
    }


    /// <summary>
    /// インベントリにカードを追加（上限チェック付き）
    /// </summary>
    public bool AddToInventory(KanjiCardData card)
    {
        if (card == null) return false;
        if (inventory.Count >= inventoryMaxSize)
        {
            Debug.Log($"[GameManager] インベントリが満杯（{inventoryMaxSize}枚）！ 『{card.kanji}』を追加できません");
            return false;
        }
        inventory.Add(card);
        Debug.Log($"[GameManager] インベントリに『{card.kanji}』を追加（{inventory.Count}/{inventoryMaxSize}）");

        // 図鑑に登録
        if (EncyclopediaManager.Instance != null)
        {
            EncyclopediaManager.Instance.UnlockCard(card.cardId);
        }

        return true;
    }

    /// <summary>
    /// プレイヤーにダメージ（シールドが命綱：シールド0枚でダメージを受けるとゲームオーバー）
    /// </summary>
    public void TakeDamage(int damage)
    {
        int actualDamage = Mathf.Max(0, damage - playerDefenseBuff);
        if (actualDamage <= 0) return;

        // シールドトリガー：ダメージをシールドで受け止める
        if (shields.Count > 0)
        {
            var brokenShield = shields[shields.Count - 1];
            shields.RemoveAt(shields.Count - 1);

            if (hand.Count < initialHandSize)
            {
                hand.Add(brokenShield);
                Debug.Log($"[GameManager] シールドトリガー！『{brokenShield.kanji}』が手札に加わった！");
                battleManager?.AddBattleLog($"<color=#00FFFF><b>シールドトリガー！『{brokenShield.kanji}』が手札に加わった！</b></color>");
            }
            else
            {
                drawPile.Insert(0, brokenShield);
                Debug.Log($"[GameManager] シールドトリガー！手札満杯のため『{brokenShield.kanji}』を山札の底へ");
                battleManager?.AddBattleLog($"<color=#00FFFF>シールドトリガー！手札満杯のため『{brokenShield.kanji}』を山札の底へ。</color>");
            }

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySE(AudioManager.Instance.seButton50);

            battleManager?.battleUI?.UpdateShieldUI();
            battleManager?.battleUI?.UpdateHandUI();
            return; // シールドがダメージを完全吸収
        }

        // シールドが0枚 → ゲームオーバー
        Debug.Log($"[GameManager] シールド0枚！ {actualDamage}ダメージでゲームオーバー");
        battleManager?.AddBattleLog($"<color=#FF2222><b>シールドが尽きた！</b></color>");
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySE(AudioManager.Instance.seButton38);
        playerHP = 0; // 既存のゲームオーバー検出との互換性を維持
        ChangeState(GameState.GameOver);
    }

    /// <summary>
    /// プレイヤーを回復 → シールド1枚追加（HPシステム廃止のためシールドで代替）
    /// </summary>
    public void Heal(int amount)
    {
        if (shields.Count >= maxShields)
        {
            Debug.Log($"[GameManager] シールドが上限（{maxShields}枚）に達しているため追加不可");
            return;
        }
        KanjiCardData newShield = null;
        if (drawPile.Count > 0) { newShield = drawPile[0]; drawPile.RemoveAt(0); }
        else if (discardPile.Count > 0) { newShield = discardPile[discardPile.Count - 1]; discardPile.RemoveAt(discardPile.Count - 1); }
        else if (inventory.Count > 0) { newShield = inventory[UnityEngine.Random.Range(0, inventory.Count)]; }

        if (newShield != null)
        {
            shields.Add(newShield);
            Debug.Log($"[GameManager] 回復→シールド追加 『{newShield.kanji}』（合計{shields.Count}枚）");
            battleManager?.AddBattleLog($"<color=#00AAFF>シールドが1枚追加された！（合計{shields.Count}枚）</color>");
            battleManager?.battleUI?.UpdateShieldUI();
        }
    }

    /// <summary>
    /// ターン開始時のリセット（APを全回復して手札補充）
    /// </summary>
    public void StartPlayerTurn()
    {
        playerDefenseBuff = 0;
        playerMana += playerStartMana; // APをターン開始時に加算（上限なし・累積）

        // 手札補充：手札上限 - 現在の手札枚数だけドロー（差分ドロー方式）
        int drawCount = Mathf.Max(0, initialHandSize - hand.Count);
        if (drawCount > 0)
        {
            DrawFromDeck(drawCount);
        }

        Debug.Log($"[GameManager] プレイヤーターン開始 AP:{playerMana} 手札:{hand.Count}枚");
    }


    /// <summary>
    /// 合成レシピDictionaryを初期化
    /// </summary>
    /// <summary>
    /// 戦闘開始時のデッキ準備
    /// </summary>
    public void InitializeBattleDeck()
    {
        drawPile.Clear();
        discardPile.Clear();
        // hand はクリアしない（手札持ち越しシステム）

        var sourceCards = (deckManager != null) ? deckManager.currentDeck : inventory;

        // 手札に既にあるカードを除いてdrawPileを構築
        var handSet = new System.Collections.Generic.HashSet<KanjiCardData>(hand);
        foreach (var card in sourceCards)
        {
            if (!handSet.Contains(card))
                drawPile.Add(card);
        }

        // 初期シャッフル
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = drawPile[i];
            drawPile[i] = drawPile[j];
            drawPile[j] = temp;
        }

        // シールドを山札の先頭から3枚セット
        shields.Clear();
        for (int i = 0; i < maxShields && drawPile.Count > 0; i++)
        {
            shields.Add(drawPile[0]);
            drawPile.RemoveAt(0);
        }

        Debug.Log($"[GameManager] バトル用デッキ準備完了（手札持ち越し:{hand.Count}枚）: drawPile={drawPile.Count}枚 シールド:{shields.Count}枚");
    }

    public void InitializeFusionRecipes()
    {
        fusionRecipeDict.Clear();
        fusionRecipeDict3.Clear();
        allCardsDict.Clear();
        decomposeDict.Clear();

        // 全カードアセットを検索してDictionaryに登録
        var allCards = Resources.LoadAll<KanjiCardData>("");
        foreach (var card in allCards)
        {
            if (!allCardsDict.ContainsKey(card.cardId))
            {
                allCardsDict[card.cardId] = card;
            }
        }

        // インベントリ内のカードも登録
        foreach (var card in inventory)
        {
            if (!allCardsDict.ContainsKey(card.cardId))
            {
                allCardsDict[card.cardId] = card;
            }
        }

        // FusionDatabaseからレシピを読み込み
        if (fusionDatabase != null && fusionDatabase.recipes != null)
        {
            foreach (var recipe in fusionDatabase.recipes)
            {
                if (recipe.material1 == null || recipe.material2 == null || recipe.result == null) continue;

                int resultId = recipe.result.cardId;

                // 結果カードも登録
                if (!allCardsDict.ContainsKey(resultId))
                {
                    allCardsDict[resultId] = recipe.result;
                }

                if (recipe.IsTwoMaterial)
                {
                    int id1 = recipe.material1.cardId;
                    int id2 = recipe.material2.cardId;
                    var key = (Mathf.Min(id1, id2), Mathf.Max(id1, id2));
                    if (!fusionRecipeDict.ContainsKey(key)) fusionRecipeDict[key] = new List<int>();
                    fusionRecipeDict[key].Add(resultId);

                    // 分解用逆引き
                    decomposeDict[resultId] = new List<int> { id1, id2 };
                }
                else if (recipe.IsThreeMaterial)
                {
                    int id1 = recipe.material1.cardId;
                    int id2 = recipe.material2.cardId;
                    int id3 = recipe.material3.cardId;
                    var ids = new int[] { id1, id2, id3 };
                    System.Array.Sort(ids);
                    var key = (ids[0], ids[1], ids[2]);
                    if (!fusionRecipeDict3.ContainsKey(key)) fusionRecipeDict3[key] = new List<int>();
                    fusionRecipeDict3[key].Add(resultId);

                    decomposeDict[resultId] = new List<int> { id1, id2, id3 };
                }
            }
        }

        Debug.Log($"[GameManager] 合成レシピ初期化完了: 2枚:{fusionRecipeDict.Count} 3枚:{fusionRecipeDict3.Count} カード:{allCardsDict.Count}");
    }

    /// <summary>
    /// 全カードのコストを一律1に強制、Draw系カードのeffectValueを2以上に引き上げ
    /// </summary>
    private void EnforceCardBalancePatches()
    {
        int costPatched = 0;
        int drawPatched = 0;

        foreach (var card in allCardsDict.Values)
        {
            if (card == null) continue;

            // APコスト設定：攻撃・治癒・特殊系=1、それ以外=0
            int targetCost = GetCardAPCost(card);

            if (card.cost != targetCost)
            {
                card.cost = targetCost;
                costPatched++;
            }

            // Draw系カードの効果値を最低2に引き上げ
            if (card.effectType == CardEffectType.Draw && card.effectValue < 2)
            {
                card.effectValue = 2;
                drawPatched++;
            }
        }

        // インベントリ内のカードにも適用
        foreach (var card in inventory)
        {
            if (card == null) continue;
            card.cost = GetCardAPCost(card);
            if (card.effectType == CardEffectType.Draw && card.effectValue < 2)
                card.effectValue = 2;
        }

        if (costPatched > 0 || drawPatched > 0)
            Debug.Log($"[GameManager] カードバランスパッチ適用: コスト修正{costPatched}枚, Drawバフ{drawPatched}枚");
    }
    /// <summary>
    /// 2枚合成結果を高速検索（最初の1件、見つからなければ-1）
    /// </summary>
    public int FindFusionResult(int id1, int id2)
    {
        var key = (Mathf.Min(id1, id2), Mathf.Max(id1, id2));
        if (fusionRecipeDict.TryGetValue(key, out var results) && results.Count > 0)
        {
            return results[0];
        }
        return -1;
    }

    /// <summary>
    /// 2枚合成の全候補を検索（複数結果対応）
    /// </summary>
    public List<int> FindFusionResults(int id1, int id2)
    {
        var key = (Mathf.Min(id1, id2), Mathf.Max(id1, id2));
        if (fusionRecipeDict.TryGetValue(key, out var results))
        {
            return results;
        }
        return new List<int>();
    }

    /// <summary>
    /// 3枚合成の全候補を検索
    /// </summary>
    public List<int> FindFusionResults3(int id1, int id2, int id3)
    {
        var ids = new int[] { id1, id2, id3 };
        System.Array.Sort(ids);
        var key = (ids[0], ids[1], ids[2]);
        if (fusionRecipeDict3.TryGetValue(key, out var results))
        {
            return results;
        }
        return new List<int>();
    }

    /// <summary>
    /// 分解：結果IDから素材IDリストを取得
    /// </summary>
    public List<int> FindDecomposeMaterials(int resultCardId)
    {
        if (decomposeDict.TryGetValue(resultCardId, out var materials))
        {
            return materials;
        }
        return null;
    }

    /// <summary>
    /// カードIDからカードデータを取得
    /// </summary>
    /// <summary>
    /// 漢字からカードデータを取得
    /// </summary>
    public KanjiCardData GetCardByKanji(string kanji)
    {
        foreach (var card in allCardsDict.Values)
        {
            if (card.kanji == kanji) return card;
        }
        return null;
    }

    public KanjiCardData GetCardById(int cardId)
    {
        if (allCardsDict.TryGetValue(cardId, out KanjiCardData card))
        {
            return card;
        }
        return null;
    }

    /// <summary>
    /// 分解実行：合体済みカードを素材に戻す（インベントリに戻す）
    /// </summary>
    public bool DecomposeCard(KanjiCardData card)
    {
        if (card == null || !card.isFusionResult) return false;

        var materialIds = FindDecomposeMaterials(card.cardId);
        if (materialIds == null || materialIds.Count == 0) return false;

        // 手札とインベントリから合体カードを除去
        hand.Remove(card);
        inventory.Remove(card);

        // 素材カードを手札とインベントリに追加
        foreach (int matId in materialIds)
        {
            var matCard = GetCardById(matId);
            if (matCard != null)
            {
                hand.Add(matCard);
                AddToInventory(matCard);
                Debug.Log($"[GameManager] 分解: 『{matCard.kanji}』をインベントリに追加");
            }
        }

        Debug.Log($"[GameManager] 『{card.kanji}』を分解しました");
        return true;
    }

    // ==== 後方互換用（他スクリプトからの参照用）====
    // deck を inventory のエイリアスとして公開
    public List<KanjiCardData> deck
    {
        get { return (deckManager != null) ? deckManager.currentDeck : inventory; }
        set { if (deckManager != null) deckManager.currentDeck = value; else inventory = value; }
    }

    // discardPile は廃止。参照が残っている場合のための空リスト

    /// <summary>
    /// ゲームをリセット（最初からやり直し）
    /// </summary>
    public void ResetGame()
    {
        Debug.Log("[GameManager] ゲームリセット開始");

        Time.timeScale = 1f;

        // シーンリロードで全オブジェクトが再生成されるため、ここではTimeScaleのみリセット
        // Instance の null 代入は不要（UnityはDestroyされたオブジェクトをnull等価として扱う）
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }

}

/// <summary>
/// ゲームの状態
/// </summary>
public enum GameState
{
    Title,
    Field,   // 2D見下ろしフィールド探索
    Map,     // (旧) Slay the Spire型マップ - 後方互換用に残す
    Battle,
    Fusion,
    Shop,
    Event,
    Dojo,
    DeckEdit,
    GameOver
}
