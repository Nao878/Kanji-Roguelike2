using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Slay the Spire型縦スクロールルート選択マップ
/// 各ステージでプレイヤーが3つの進路から1つをタップして選択する
/// </summary>
public class RouteMapManager : MonoBehaviour
{
    public enum RouteNodeType
    {
        Battle,        // 雑魚戦闘
        EliteBattle,   // 中堅戦闘
        Shop,          // 店
        OniArea,       // 高難易度・鬼
        TimedBattle,   // 時間制限戦闘
        Lottery,       // 運試し/福引き
        ShieldAdd,     // シールド追加
        CardDuplicate, // カード複製
        Boss           // ステージボス（狼）
    }

    [System.Serializable]
    public class RouteNode
    {
        public RouteNodeType type;
        public int stageIndex;
        public int nodeIndex;
        public List<int> nextNodeIndices = new List<int>();
        public bool isVisited;
        public bool isUnlocked;
    }

    // ============================================
    // 内部状態
    // ============================================
    private List<List<RouteNode>> stages = new List<List<RouteNode>>();
    private int currentStage = 0;
    private bool isInitialized = false;
    private TMP_FontAsset appFont;
    private Canvas mapCanvas;
    private GameObject mapPanel;
    private Coroutine timedBattleCoroutine;

    // 悪玉システム（static: ゲーム進行中で共有）
    public static int AkudamaCount = 0;

    private const int TotalStages = 7; // 0〜5: 通常, 6: ボス

    // ============================================
    // 初期化
    // ============================================

    public void ShowMap()
    {
        if (!isInitialized)
        {
            appFont = FindObjectOfType<BattleUI>()?.appFont;
            if (appFont == null)
            {
                var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (var f in allFonts)
                {
                    if (f.name.Contains("AppFont") || f.name.Contains("JP") || f.name.Contains("Noto"))
                    {
                        appFont = f;
                        break;
                    }
                }
            }
            FindMapCanvas();
            GenerateRouteGraph();
            isInitialized = true;
        }
        RenderMap();
    }

    private void FindMapCanvas()
    {
        var allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in allCanvases)
        {
            if (c.name == "MainCanvas" || c.isRootCanvas)
            {
                mapCanvas = c;
                break;
            }
        }
    }

    private void GenerateRouteGraph()
    {
        stages.Clear();
        currentStage = 0;

        // ステージ0〜5: 各3ノード, ステージ6: ボス(1ノード)
        RouteNodeType[] pool = {
            RouteNodeType.Battle, RouteNodeType.EliteBattle, RouteNodeType.Shop,
            RouteNodeType.OniArea, RouteNodeType.TimedBattle, RouteNodeType.Lottery,
            RouteNodeType.ShieldAdd, RouteNodeType.CardDuplicate
        };

        for (int s = 0; s < TotalStages - 1; s++)
        {
            var stageNodes = new List<RouteNode>();
            var shuffled = new List<RouteNodeType>(pool);
            Shuffle(shuffled);

            int nodeCount = 3;
            for (int n = 0; n < nodeCount; n++)
            {
                var node = new RouteNode
                {
                    type = shuffled[n % shuffled.Count],
                    stageIndex = s,
                    nodeIndex = n,
                    isUnlocked = (s == 0), // ステージ0は全解放
                    isVisited = false
                };

                // 次ステージへの接続(2つ接続)
                bool isFinalMiddle = (s == TotalStages - 2);
                if (!isFinalMiddle)
                {
                    int c1 = n % 3;
                    int c2 = (n + 1) % 3;
                    node.nextNodeIndices.Add(c1);
                    if (c2 != c1) node.nextNodeIndices.Add(c2);
                }
                else
                {
                    node.nextNodeIndices.Add(0); // ボスノード
                }

                stageNodes.Add(node);
            }
            stages.Add(stageNodes);
        }

        // ボスステージ
        var bossStage = new List<RouteNode>
        {
            new RouteNode
            {
                type = RouteNodeType.Boss,
                stageIndex = TotalStages - 1,
                nodeIndex = 0,
                isUnlocked = false
            }
        };
        stages.Add(bossStage);
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    // ============================================
    // マップ描画
    // ============================================

    private void RenderMap()
    {
        if (mapCanvas == null) FindMapCanvas();
        if (mapCanvas == null) return;

        if (mapPanel != null) Destroy(mapPanel);

        mapPanel = new GameObject("RouteMapPanel");
        mapPanel.transform.SetParent(mapCanvas.transform, false);
        // Remove SetAsFirstSibling so it draws on top of BackgroundPanel

        var bg = mapPanel.AddComponent<RectTransform>();
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.offsetMin = Vector2.zero;
        bg.offsetMax = Vector2.zero;
        mapPanel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.08f, 0.97f);

        CreateTMP(mapPanel.transform, "▲ ルート選択 ▲", new Vector2(0f, 0.945f), new Vector2(1f, 1f), 30, new Color(1f, 0.9f, 0.3f), FontStyles.Bold);

        if (AkudamaCount > 0)
            CreateTMP(mapPanel.transform, $"悪玉: {AkudamaCount}個", new Vector2(0.75f, 0.945f), new Vector2(1f, 1f), 18, new Color(0.8f, 0.3f, 1f));

        // ステージを下から上へ描画（最下段が現在のステージ）
        int stageCount = stages.Count;
        float stageH = 0.88f / stageCount;
        float bottomMargin = 0.02f;

        for (int s = 0; s < stageCount; s++)
        {
            float centerY = bottomMargin + (float)s / stageCount * 0.88f + stageH * 0.5f;
            var stageNodes = stages[s];

            for (int n = 0; n < stageNodes.Count; n++)
            {
                var node = stageNodes[n];
                float xStep = 1f / (stageNodes.Count + 1);
                float cx = xStep * (n + 1);

                bool canSelect = !node.isVisited && node.isUnlocked && (s == currentStage);
                DrawNode(node, cx, centerY, canSelect);
            }
        }

        // 現在選択フロアのガイドライン
        float guideY = bottomMargin + (float)currentStage / stageCount * 0.88f;
        var lineGo = new GameObject("GuideArrow");
        lineGo.transform.SetParent(mapPanel.transform, false);
        var lineRect = lineGo.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, guideY);
        lineRect.anchorMax = new Vector2(1f, guideY + 0.005f);
        lineRect.offsetMin = Vector2.zero;
        lineRect.offsetMax = Vector2.zero;
        lineGo.AddComponent<Image>().color = new Color(1f, 0.9f, 0.3f, 0.4f);

        // 「選択してください」を画面上部の固定エリアに配置（ノードと被らないよう）
        CreateTMP(mapPanel.transform, "◀ 選択してください ▶",
            new Vector2(0f, 0.89f), new Vector2(1f, 0.945f),
            21, new Color(1f, 0.85f, 0.3f, 0.9f));
    }

    private void DrawNode(RouteNode node, float cx, float cy, bool selectable)
    {
        float hw = 0.13f, hh = 0.055f;

        var go = new GameObject($"Node_{node.stageIndex}_{node.nodeIndex}");
        go.transform.SetParent(mapPanel.transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(cx - hw, cy - hh);
        rect.anchorMax = new Vector2(cx + hw, cy + hh);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = GetNodeColor(node.type, selectable, node.isVisited);

        var border = go.AddComponent<Outline>();
        border.effectColor = selectable ? new Color(1f, 0.9f, 0.3f, 0.95f) : new Color(0.3f, 0.3f, 0.3f, 0.4f);
        border.effectDistance = new Vector2(3f, -3f);

        string icon = GetNodeIcon(node.type);
        string label = GetNodeLabel(node.type);
        string visLabel = node.isVisited ? "✓" : icon;

        CreateTMP(go.transform, visLabel + "\n" + label,
            Vector2.zero, Vector2.one, 18,
            node.isVisited ? new Color(0.5f, 0.5f, 0.5f) : Color.white);

        if (selectable)
        {
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.9f, 0.5f, 1f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.2f, 1f);
            btn.colors = colors;

            var capturedNode = node;
            btn.onClick.AddListener(() => OnNodeSelected(capturedNode));
        }
    }

    // ============================================
    // ノード選択処理
    // ============================================

    private void OnNodeSelected(RouteNode node)
    {
        if (mapPanel != null) Destroy(mapPanel);

        node.isVisited = true;

        // 次ステージのノードをアンロック
        int nextS = node.stageIndex + 1;
        if (nextS < stages.Count)
        {
            foreach (int idx in node.nextNodeIndices)
            {
                if (idx < stages[nextS].Count)
                    stages[nextS][idx].isUnlocked = true;
            }
        }

        switch (node.type)
        {
            case RouteNodeType.Battle:
                StartBattleEvent(false);
                break;
            case RouteNodeType.EliteBattle:
                StartBattleEvent(true);
                break;
            case RouteNodeType.Shop:
                StartCoroutine(ShowShopUI());
                break;
            case RouteNodeType.OniArea:
                StartOniBattleEvent();
                break;
            case RouteNodeType.TimedBattle:
                StartTimedBattleEvent();
                break;
            case RouteNodeType.Lottery:
                StartCoroutine(ShowLotteryUI());
                break;
            case RouteNodeType.ShieldAdd:
                StartShieldAddEvent();
                break;
            case RouteNodeType.CardDuplicate:
                StartCoroutine(ShowCardDuplicateUI());
                break;
            case RouteNodeType.Boss:
                StartBossEvent();
                break;
        }
    }

    // ============================================
    // 戦闘後コールバック
    // ============================================

    public void OnBattleWon()
    {
        // currentStageを進める（ShowMapはChangeState(Field)から呼ばれる）
        if (currentStage + 1 < stages.Count)
            currentStage++;
        if (timedBattleCoroutine != null) { StopCoroutine(timedBattleCoroutine); timedBattleCoroutine = null; }
    }

    private void ReturnToMap()
    {
        if (currentStage + 1 < stages.Count)
            currentStage++;
        ShowMap();
    }

    // ============================================
    // エリアイベント実装
    // ============================================

    private void StartBattleEvent(bool isElite)
    {
        var gm = GameManager.Instance;
        var bm = gm?.battleManager;
        if (bm == null) return;

        gm.ChangeState(GameState.Battle);

        if (isElite)
        {
            var elite = CreateEnemyData("強敵", "勇", 35, 2, EnemyType.Normal);
            bm.StartBattle(elite);
        }
        else
        {
            if (bm.normalEnemies != null && bm.normalEnemies.Length > 0)
                bm.StartBattle(bm.normalEnemies[Random.Range(0, bm.normalEnemies.Length)]);
            else
            {
                var basic = CreateEnemyData("敵", "火", 20, 1, EnemyType.Normal);
                bm.StartBattle(basic);
            }
        }
    }

    private void StartOniBattleEvent()
    {
        var gm = GameManager.Instance;
        var bm = gm?.battleManager;
        if (bm == null) return;

        var oniData = CreateEnemyData("鬼", "鬼", 80, 2, EnemyType.Boss);
        oniData.componentCount = 10;
        oniData.isOniBoss = true;

        gm.ChangeState(GameState.Battle);
        bm.StartBattle(oniData);
    }

    private void StartTimedBattleEvent()
    {
        var gm = GameManager.Instance;
        var bm = gm?.battleManager;
        if (bm == null) return;

        gm.ChangeState(GameState.Battle);

        EnemyData enemy;
        if (bm.normalEnemies != null && bm.normalEnemies.Length > 0)
            enemy = bm.normalEnemies[Random.Range(0, bm.normalEnemies.Length)];
        else
            enemy = CreateEnemyData("敵", "水", 20, 1, EnemyType.Normal);

        bm.StartBattle(enemy);
        timedBattleCoroutine = StartCoroutine(TimedBattleCountdown(30f));
    }

    private IEnumerator TimedBattleCountdown(float seconds)
    {
        float remaining = seconds;
        Canvas canvas = mapCanvas;
        if (canvas == null) FindMapCanvas(); canvas = mapCanvas;

        GameObject timerGo = null;
        TextMeshProUGUI timerTMP = null;

        if (canvas != null)
        {
            timerGo = new GameObject("TimedBattleTimer");
            timerGo.transform.SetParent(canvas.transform, false);
            timerGo.transform.SetAsLastSibling();
            var r = timerGo.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0.35f, 0.9f);
            r.anchorMax = new Vector2(0.65f, 0.98f);
            r.offsetMin = r.offsetMax = Vector2.zero;
            timerGo.AddComponent<Image>().color = new Color(0.6f, 0f, 0f, 0.88f);
            var tg = new GameObject("Text");
            tg.transform.SetParent(timerGo.transform, false);
            timerTMP = tg.AddComponent<TextMeshProUGUI>();
            timerTMP.fontSize = 20;
            timerTMP.alignment = TextAlignmentOptions.Center;
            timerTMP.color = Color.white;
            timerTMP.raycastTarget = false;
            if (appFont != null) timerTMP.font = appFont;
            var tr = tg.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
        }

        while (remaining > 0)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.currentState != GameState.Battle) break;

            remaining -= Time.deltaTime;
            if (timerTMP != null)
            {
                timerTMP.text = $"⏱ {Mathf.CeilToInt(remaining)}秒";
                timerTMP.color = remaining <= 10 ? new Color(1f, 0.3f, 0.3f) : Color.white;
            }
            yield return null;
        }

        if (timerGo != null) Destroy(timerGo);

        // 時間切れ→敗北
        var gm2 = GameManager.Instance;
        if (gm2 != null && gm2.currentState == GameState.Battle)
        {
            gm2.battleManager?.AddBattleLog("<color=#FF2222><b>時間切れ！強制敗北！</b></color>");
            gm2.ChangeState(GameState.GameOver);
        }
    }

    private IEnumerator ShowShopUI()
    {
        yield return null; // 1フレーム待機

        if (mapCanvas == null) FindMapCanvas();
        if (mapCanvas == null) { ReturnToMap(); yield break; }

        var panel = CreateFullPanel("RouteShop", new Color(0.04f, 0.08f, 0.04f, 0.97f));

        CreateTMP(panel.transform, "★ 店 ★", new Vector2(0f, 0.87f), new Vector2(1f, 0.96f), 30, new Color(1f, 0.9f, 0.3f), FontStyles.Bold);
        CreateTMP(panel.transform, "コイン20G、またはシールド1枚で1枚購入", new Vector2(0.05f, 0.8f), new Vector2(0.95f, 0.88f), 14, new Color(0.8f, 0.8f, 0.8f));

        // カード5枚表示
        var allCards = Resources.FindObjectsOfTypeAll<KanjiCardData>();
        var picks = new List<KanjiCardData>(allCards);
        Shuffle(picks);
        int count = Mathf.Min(5, picks.Count);

        var row = new GameObject("Row");
        row.transform.SetParent(panel.transform, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.02f, 0.4f);
        rowRect.anchorMax = new Vector2(0.98f, 0.78f);
        rowRect.offsetMin = rowRect.offsetMax = Vector2.zero;
        var hLayout = row.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 8; hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childForceExpandWidth = hLayout.childForceExpandHeight = false;

        for (int i = 0; i < count; i++)
        {
            var card = picks[i];
            CreateShopCardBtn(row.transform, card, panel, false);
        }

        // シールド1枚払って購入ボタンエリア
        CreateTMP(panel.transform, "シールド1枚を差し出すと購入可（コスト不要）", new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.4f), 13, new Color(0.7f, 0.7f, 1f));

        var row2 = new GameObject("Row2");
        row2.transform.SetParent(panel.transform, false);
        var r2Rect = row2.AddComponent<RectTransform>();
        r2Rect.anchorMin = new Vector2(0.02f, 0.15f);
        r2Rect.anchorMax = new Vector2(0.98f, 0.32f);
        r2Rect.offsetMin = r2Rect.offsetMax = Vector2.zero;
        var hLayout2 = row2.AddComponent<HorizontalLayoutGroup>();
        hLayout2.spacing = 8; hLayout2.childAlignment = TextAnchor.MiddleCenter;
        hLayout2.childForceExpandWidth = hLayout2.childForceExpandHeight = false;

        for (int i = 0; i < count; i++)
        {
            var card = picks[i];
            CreateShopCardBtn(row2.transform, card, panel, true);
        }

        var closeBtn = CreateBtn(panel.transform, "立ち去る", new Vector2(0.3f, 0.03f), new Vector2(0.7f, 0.13f), new Color(0.25f, 0.25f, 0.25f));
        closeBtn.onClick.AddListener(() => { Destroy(panel); ReturnToMap(); });
    }

    private void CreateShopCardBtn(Transform parent, KanjiCardData card, GameObject panel, bool useShield)
    {
        var gm = GameManager.Instance;
        var go = new GameObject($"ShopCard_{card.kanji}_{(useShield ? "S" : "G")}");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90f, 120f);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 90; le.preferredHeight = 120;
        go.AddComponent<Image>().color = useShield ? new Color(0.1f, 0.2f, 0.4f, 0.95f) : new Color(0.15f, 0.15f, 0.2f, 0.95f);
        var border = go.AddComponent<Outline>();
        border.effectColor = useShield ? new Color(0.4f, 0.6f, 1f) : new Color(0.8f, 0.7f, 0.2f);
        border.effectDistance = new Vector2(2f, -2f);

        CreateTMP(go.transform, card.kanji, new Vector2(0f, 0.4f), new Vector2(1f, 0.9f), 40, Color.white);
        CreateTMP(go.transform, useShield ? "🛡" : "20G", new Vector2(0f, 0f), new Vector2(1f, 0.38f), 13, useShield ? new Color(0.5f, 0.8f, 1f) : new Color(1f, 0.85f, 0.3f));

        var btn = go.AddComponent<Button>();
        var capturedCard = card;
        var capturedPanel = panel;
        btn.onClick.AddListener(() =>
        {
            var gm2 = GameManager.Instance;
            if (gm2 == null) return;
            if (useShield)
            {
                if (gm2.shields.Count == 0) return;
                gm2.shields.RemoveAt(gm2.shields.Count - 1);
            }
            else
            {
                if (gm2.playerGold < 20) return;
                gm2.playerGold -= 20;
            }
            gm2.AddToInventory(capturedCard);
            Destroy(capturedPanel);
            ReturnToMap();
        });
    }

    private IEnumerator ShowLotteryUI()
    {
        yield return null;

        if (mapCanvas == null) FindMapCanvas();
        if (mapCanvas == null) { ReturnToMap(); yield break; }

        var panel = CreateFullPanel("LotteryPanel", new Color(0.04f, 0.02f, 0.08f, 0.97f));

        CreateTMP(panel.transform, "★ 運試し ★", new Vector2(0f, 0.87f), new Vector2(1f, 0.97f), 30, new Color(1f, 0.8f, 0.1f), FontStyles.Bold);
        CreateTMP(panel.transform, "5つの中からランダムに報酬/災いが決まる！", new Vector2(0.05f, 0.8f), new Vector2(0.95f, 0.88f), 14, new Color(0.8f, 0.8f, 0.8f));

        bool hasSpun = false;

        var spinBtn = CreateBtn(panel.transform, "ルーレットを回す！", new Vector2(0.2f, 0.6f), new Vector2(0.8f, 0.73f), new Color(0.5f, 0.1f, 0.7f));
        spinBtn.onClick.AddListener(() =>
        {
            if (hasSpun) return;
            hasSpun = true;
            spinBtn.interactable = false;
            SpinLottery(panel);
        });

        if (AkudamaCount >= 2)
        {
            var rerollBtn = CreateBtn(panel.transform, $"悪玉2個消費してリロール（所持:{AkudamaCount}個）",
                new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.6f), new Color(0.3f, 0.1f, 0.5f));
            rerollBtn.onClick.AddListener(() =>
            {
                AkudamaCount -= 2;
                Destroy(panel);
                StartCoroutine(ShowLotteryUI());
            });
        }

        var leaveBtn = CreateBtn(panel.transform, "立ち去る", new Vector2(0.3f, 0.03f), new Vector2(0.7f, 0.13f), new Color(0.25f, 0.25f, 0.25f));
        leaveBtn.onClick.AddListener(() => { Destroy(panel); ReturnToMap(); });
    }

    private void SpinLottery(GameObject panel)
    {
        string[] names = { "シールド2枚追加！", "ランダムカード獲得！", "カード複製！", "ハズレ（悪玉+1）", "コイン+30G！" };
        int roll = Random.Range(0, names.Length);
        bool isBad = (roll == 3);

        var gm = GameManager.Instance;
        if (gm != null)
        {
            switch (roll)
            {
                case 0:
                    for (int i = 0; i < 2; i++) { var sc = PickInventoryCard(gm); if (sc != null) gm.shields.Add(sc); }
                    break;
                case 1:
                    var all = Resources.FindObjectsOfTypeAll<KanjiCardData>();
                    if (all.Length > 0) gm.AddToInventory(all[Random.Range(0, all.Length)]);
                    break;
                case 2:
                    if (gm.inventory.Count > 0) gm.AddToInventory(gm.inventory[Random.Range(0, gm.inventory.Count)]);
                    break;
                case 3:
                    AkudamaCount++;
                    break;
                case 4:
                    gm.playerGold += 30;
                    break;
            }
        }

        CreateTMP(panel.transform, names[roll], new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.47f), 24,
            isBad ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.5f), FontStyles.Bold);

        var okBtn = CreateBtn(panel.transform, "OK！", new Vector2(0.3f, 0.13f), new Vector2(0.7f, 0.24f), new Color(0.2f, 0.5f, 0.2f));
        okBtn.onClick.AddListener(() => { Destroy(panel); ReturnToMap(); });
    }

    private void StartShieldAddEvent()
    {
        var gm = GameManager.Instance;
        if (gm == null) { ReturnToMap(); return; }

        var card = PickInventoryCard(gm);
        if (card != null) gm.shields.Add(card);

        StartCoroutine(ShowNotificationThenReturn(
            $"シールドが1枚追加された！\n（合計 {gm.shields.Count} 枚）", new Color(0.3f, 0.6f, 1f)));
    }

    private IEnumerator ShowCardDuplicateUI()
    {
        yield return null;

        var gm = GameManager.Instance;
        if (gm == null) { ReturnToMap(); yield break; }
        if (mapCanvas == null) FindMapCanvas();
        if (mapCanvas == null) { ReturnToMap(); yield break; }

        var panel = CreateFullPanel("CardDuplicatePanel", new Color(0.04f, 0.06f, 0.08f, 0.97f));
        CreateTMP(panel.transform, "✦ カード複製 ✦", new Vector2(0f, 0.87f), new Vector2(1f, 0.97f), 26, new Color(0.3f, 1f, 0.8f), FontStyles.Bold);
        CreateTMP(panel.transform, "複製したいカードを選んでください", new Vector2(0.05f, 0.8f), new Vector2(0.95f, 0.88f), 14, new Color(0.8f, 0.8f, 0.8f));

        if (gm.inventory.Count == 0)
        {
            CreateTMP(panel.transform, "手持ちカードがありません", new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.6f), 20, new Color(0.5f, 0.5f, 0.5f));
        }
        else
        {
            var row = new GameObject("Row");
            row.transform.SetParent(panel.transform, false);
            var rRect = row.AddComponent<RectTransform>();
            rRect.anchorMin = new Vector2(0.02f, 0.2f);
            rRect.anchorMax = new Vector2(0.98f, 0.78f);
            rRect.offsetMin = rRect.offsetMax = Vector2.zero;
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            int count = Mathf.Min(5, gm.inventory.Count);
            for (int i = 0; i < count; i++)
            {
                var card = gm.inventory[i];
                var go = new GameObject($"DupCard_{card.kanji}");
                go.transform.SetParent(row.transform, false);
                go.AddComponent<RectTransform>().sizeDelta = new Vector2(85f, 115f);
                var le = go.AddComponent<LayoutElement>(); le.preferredWidth = 85; le.preferredHeight = 115;
                go.AddComponent<Image>().color = new Color(0.1f, 0.2f, 0.2f, 0.95f);
                var border = go.AddComponent<Outline>();
                border.effectColor = new Color(0.3f, 0.9f, 0.7f);
                border.effectDistance = new Vector2(2f, -2f);
                CreateTMP(go.transform, card.kanji, new Vector2(0f, 0.35f), Vector2.one, 36, Color.white);

                var btn = go.AddComponent<Button>();
                var capturedCard = card;
                var capturedPanel = panel;
                btn.onClick.AddListener(() =>
                {
                    var gm2 = GameManager.Instance;
                    if (gm2 != null) gm2.AddToInventory(capturedCard);
                    Destroy(capturedPanel);
                    StartCoroutine(ShowNotificationThenReturn($"『{capturedCard.kanji}』を複製した！", new Color(0.3f, 1f, 0.8f)));
                });
            }
        }

        var closeBtn = CreateBtn(panel.transform, "立ち去る", new Vector2(0.3f, 0.03f), new Vector2(0.7f, 0.13f), new Color(0.25f, 0.25f, 0.25f));
        closeBtn.onClick.AddListener(() => { Destroy(panel); ReturnToMap(); });
    }

    private void StartBossEvent()
    {
        var gm = GameManager.Instance;
        var bm = gm?.battleManager;
        if (bm == null) return;

        var wolfData = ScriptableObject.CreateInstance<EnemyData>();
        wolfData.enemyName = "狼";
        wolfData.displayKanji = "狼";
        wolfData.maxHP = 150;
        wolfData.attackPower = 2;
        wolfData.enemyType = EnemyType.Boss;
        wolfData.componentCount = 3;
        wolfData.isWolfBoss = true;

        gm.ChangeState(GameState.Battle);
        bm.StartBattle(wolfData);
    }

    // ============================================
    // 悪玉消費システム（バトル中）
    // ============================================

    public static bool ConsumeAkudamaForAttackBuff()
    {
        if (AkudamaCount <= 0) return false;
        AkudamaCount--;
        return true;
    }

    // ============================================
    // ユーティリティ
    // ============================================

    private KanjiCardData PickInventoryCard(GameManager gm)
    {
        if (gm.inventory.Count > 0) return gm.inventory[Random.Range(0, gm.inventory.Count)];
        var all = Resources.FindObjectsOfTypeAll<KanjiCardData>();
        return all.Length > 0 ? all[Random.Range(0, all.Length)] : null;
    }

    private EnemyData CreateEnemyData(string name, string kanji, int hp, int atk, EnemyType type)
    {
        var data = ScriptableObject.CreateInstance<EnemyData>();
        data.enemyName = name;
        data.displayKanji = kanji;
        data.maxHP = hp;
        data.attackPower = atk;
        data.enemyType = type;
        data.componentCount = 2;
        return data;
    }

    private IEnumerator ShowNotificationThenReturn(string message, Color color)
    {
        if (mapCanvas == null) FindMapCanvas();
        if (mapCanvas == null) { ReturnToMap(); yield break; }

        var notif = new GameObject("Notif");
        notif.transform.SetParent(mapCanvas.transform, false);
        var rect = notif.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.38f);
        rect.anchorMax = new Vector2(0.9f, 0.62f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        notif.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.92f);
        CreateTMP(notif.transform, message, Vector2.zero, Vector2.one, 22, color, FontStyles.Bold);

        yield return new WaitForSeconds(2f);
        Destroy(notif);
        ReturnToMap();
    }

    // ルートマップを持つGameObjectのCanvasを探す
    private GameObject CreateFullPanel(string name, Color bgColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(mapCanvas.transform, false);
        go.transform.SetAsLastSibling();
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = bgColor;
        return go;
    }

    private TextMeshProUGUI CreateTMP(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax,
        float fontSize, Color color, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("TMP");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        if (appFont != null) tmp.font = appFont;
        return tmp;
    }

    private Button CreateBtn(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
    {
        var go = new GameObject("Btn");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = bgColor;
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = bgColor * 1.4f;
        colors.pressedColor = bgColor * 0.7f;
        btn.colors = colors;
        CreateTMP(go.transform, label, Vector2.zero, Vector2.one, 14, Color.white);
        return btn;
    }

    // ============================================
    // ノード表示ヘルパー
    // ============================================

    private Color GetNodeColor(RouteNodeType type, bool available, bool visited)
    {
        if (visited) return new Color(0.18f, 0.18f, 0.18f, 0.7f);
        if (!available) return new Color(0.12f, 0.12f, 0.18f, 0.75f);
        switch (type)
        {
            case RouteNodeType.Battle:        return new Color(0.55f, 0.18f, 0.18f, 0.9f);
            case RouteNodeType.EliteBattle:   return new Color(0.78f, 0.12f, 0.12f, 0.9f);
            case RouteNodeType.Shop:          return new Color(0.18f, 0.48f, 0.18f, 0.9f);
            case RouteNodeType.OniArea:       return new Color(0.38f, 0f, 0.08f, 0.9f);
            case RouteNodeType.TimedBattle:   return new Color(0.48f, 0.28f, 0f, 0.9f);
            case RouteNodeType.Lottery:       return new Color(0.28f, 0.08f, 0.48f, 0.9f);
            case RouteNodeType.ShieldAdd:     return new Color(0.08f, 0.28f, 0.58f, 0.9f);
            case RouteNodeType.CardDuplicate: return new Color(0.08f, 0.38f, 0.38f, 0.9f);
            case RouteNodeType.Boss:          return new Color(0.68f, 0.08f, 0.08f, 0.9f);
            default:                          return new Color(0.28f, 0.28f, 0.28f, 0.9f);
        }
    }

    private string GetNodeIcon(RouteNodeType type)
    {
        switch (type)
        {
            case RouteNodeType.Battle:        return "⚔";
            case RouteNodeType.EliteBattle:   return "⚔+";
            case RouteNodeType.Shop:          return "店";
            case RouteNodeType.OniArea:       return "鬼";
            case RouteNodeType.TimedBattle:   return "⏱";
            case RouteNodeType.Lottery:       return "★";
            case RouteNodeType.ShieldAdd:     return "盾";
            case RouteNodeType.CardDuplicate: return "✦";
            case RouteNodeType.Boss:          return "狼";
            default:                          return "?";
        }
    }

    private string GetNodeLabel(RouteNodeType type)
    {
        switch (type)
        {
            case RouteNodeType.Battle:        return "戦闘";
            case RouteNodeType.EliteBattle:   return "中堅戦";
            case RouteNodeType.Shop:          return "店";
            case RouteNodeType.OniArea:       return "鬼道";
            case RouteNodeType.TimedBattle:   return "時間戦";
            case RouteNodeType.Lottery:       return "福引";
            case RouteNodeType.ShieldAdd:     return "守護";
            case RouteNodeType.CardDuplicate: return "複製";
            case RouteNodeType.Boss:          return "BOSS";
            default:                          return "?";
        }
    }
}
