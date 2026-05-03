using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 2D見下ろし型フィールドを管理するマネージャー
/// 階層システム・鬼（追跡者）システムを含む
/// </summary>
public class FieldManager : MonoBehaviour
{
    [Header("フィールド設定")]
    public int gridWidth = 10;
    public int gridHeight = 8;
    public float cellSize = 64f;

    [Header("UI参照")]
    public RectTransform fieldContent;
    public TextMeshProUGUI inventoryCountText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI goldText;
    public Button deckButton;

    [Header("フォント")]
    public TMP_FontAsset appFont;

    [Header("階層システム")]
    public int currentFloor = 1;
    private int defeatedOnCurrentFloor = 0;
    private const int OniSpawnThreshold = 5;
    private TextMeshProUGUI floorText;

    // プレイヤー
    private GameObject playerObject;
    private Vector2Int playerGridPos;
    private TopDownPlayerController playerController;

    // 敵シンボル
    private List<FieldEnemy> fieldEnemies = new List<FieldEnemy>();

    // 背景タイル
    private List<GameObject> backgroundTiles = new List<GameObject>();

    // 階段
    private GameObject stairsObject;
    private Vector2Int stairsGridPos;

    // 鬼
    private GameObject oniObject;
    private Vector2Int oniGridPos;
    private bool oniIsActive = false;
    private Coroutine oniChaseCoroutine;

    [System.Serializable]
    public class FieldEnemy
    {
        public Vector2Int gridPos;
        public EnemyData enemyData;
        public GameObject uiObject;
        public bool isDefeated;
    }

    // 現在エンカウント中の敵インデックス
    private int currentEncounterIndex = -1;

    /// <summary>
    /// フィールド表示
    /// </summary>
    public void ShowField()
    {
        Debug.Log($"[FieldManager] フィールド再生成実行: F{currentFloor}");
        ClearField();
        CreateBackground();
        SpawnPlayer();
        SpawnEnemies();
        CreateStairs();
        UpdateStatusUI();

        // 鬼が既にアクティブなら追跡を再開
        if (oniIsActive && oniObject == null)
        {
            SpawnOniSymbol();
        }
        else if (oniIsActive && oniObject != null && oniChaseCoroutine == null)
        {
            oniChaseCoroutine = StartCoroutine(OniChasePlayer());
        }
    }

    private void Start()
    {
        // deckButton が Inspector で未設定なら名前で自動検索
        if (deckButton == null)
        {
            var allBtns = FindObjectsOfType<UnityEngine.UI.Button>(true);
            foreach (var b in allBtns)
            {
                if (b.gameObject.name == "FieldDeckBtn")
                {
                    deckButton = b;
                    break;
                }
            }
        }

        if (deckButton != null)
        {
            deckButton.onClick.RemoveAllListeners();
            deckButton.onClick.AddListener(() => {
                if (GameManager.Instance != null)
                    GameManager.Instance.ChangeState(GameState.DeckEdit);
            });
            Debug.Log("[FieldManager] deckButtonをDeckEditに紐付けました");
        }
    }

    /// <summary>
    /// フィールドをクリア
    /// </summary>
    private void ClearField()
    {
        foreach (var tile in backgroundTiles)
        {
            if (tile != null) Destroy(tile);
        }
        backgroundTiles.Clear();

        foreach (var enemy in fieldEnemies)
        {
            if (enemy.uiObject != null) Destroy(enemy.uiObject);
        }
        fieldEnemies.Clear();

        if (playerObject != null) Destroy(playerObject);
        if (stairsObject != null) Destroy(stairsObject);
        stairsObject = null;

        if (oniObject != null) Destroy(oniObject);
        oniObject = null;
        if (oniChaseCoroutine != null) StopCoroutine(oniChaseCoroutine);
        oniChaseCoroutine = null;
    }

    /// <summary>
    /// 全データを初期化（リセット時用）
    /// </summary>
    public void ClearData()
    {
        ClearField();
        fieldEnemies.Clear();
        currentEncounterIndex = -1;
        currentFloor = 1;
        defeatedOnCurrentFloor = 0;
        oniIsActive = false;
    }

    /// <summary>
    /// 背景グリッドを描画（和風テイスト）
    /// </summary>
    private void CreateBackground()
    {
        if (fieldContent == null) return;

        string[] groundKanji = { "草", "原", "野", "道", "土", "石" };

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                var go = new GameObject($"Tile_{x}_{y}");
                go.transform.SetParent(fieldContent, false);

                var rect = go.AddComponent<RectTransform>();
                rect.anchoredPosition = GridToUIPosition(x, y);
                rect.sizeDelta = new Vector2(cellSize, cellSize);

                var img = go.AddComponent<Image>();
                bool isLight = (x + y) % 2 == 0;
                img.color = isLight
                    ? new Color(0.15f, 0.18f, 0.12f, 0.6f)
                    : new Color(0.12f, 0.15f, 0.10f, 0.6f);
                img.raycastTarget = false;

                var textGo = new GameObject("TerrainText");
                textGo.transform.SetParent(go.transform, false);
                var text = textGo.AddComponent<TextMeshProUGUI>();
                text.text = groundKanji[Random.Range(0, groundKanji.Length)];
                text.fontSize = 14;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(0.3f, 0.35f, 0.25f, 0.15f);
                text.raycastTarget = false;
                if (appFont != null) text.font = appFont;
                var textRect = textGo.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                backgroundTiles.Add(go);
            }
        }
    }

    /// <summary>
    /// プレイヤーを配置
    /// </summary>
    private void SpawnPlayer()
    {
        if (fieldContent == null) return;

        playerGridPos = new Vector2Int(1, 1);

        playerObject = new GameObject("Player");
        playerObject.transform.SetParent(fieldContent, false);

        var rect = playerObject.AddComponent<RectTransform>();
        rect.anchoredPosition = GridToUIPosition(playerGridPos.x, playerGridPos.y);
        rect.sizeDelta = new Vector2(cellSize * 0.9f, cellSize * 0.9f);

        var bg = playerObject.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.4f, 0.8f, 0.85f);
        bg.raycastTarget = false;

        var textGo = new GameObject("PlayerText");
        textGo.transform.SetParent(playerObject.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "人";
        text.fontSize = 36;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        if (appFont != null) text.font = appFont;
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // Outline で視認性向上
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.1f, 0.5f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        playerController = playerObject.AddComponent<TopDownPlayerController>();
        playerController.fieldManager = this;

        playerObject.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 敵シンボルを配置
    /// </summary>
    private void SpawnEnemies()
    {
        if (fieldContent == null) return;

        var bm = GameManager.Instance?.battleManager;
        if (bm == null || bm.normalEnemies == null || bm.normalEnemies.Length == 0) return;

        if (fieldEnemies.Count == 0)
        {
            int enemyCount = Mathf.Min(6, bm.normalEnemies.Length * 2);

            for (int i = 0; i < enemyCount; i++)
            {
                Vector2Int pos;
                int attempts = 0;
                do
                {
                    pos = new Vector2Int(Random.Range(2, gridWidth - 1), Random.Range(1, gridHeight - 1));
                    attempts++;
                } while (IsOccupied(pos) && attempts < 50);

                if (attempts >= 50) continue;

                var enemyData = bm.normalEnemies[Random.Range(0, bm.normalEnemies.Length)];
                var scaledEnemy = ScaleEnemyForFloor(enemyData);
                var fieldEnemy = new FieldEnemy
                {
                    gridPos = pos,
                    enemyData = scaledEnemy,
                    isDefeated = false
                };

                fieldEnemy.uiObject = CreateEnemySymbol(fieldEnemy);
                fieldEnemies.Add(fieldEnemy);
            }

            if (bm.bossEnemy != null)
            {
                var bossPos = new Vector2Int(gridWidth - 2, gridHeight / 2);
                var scaledBoss = ScaleEnemyForFloor(bm.bossEnemy);
                var bossEnemy = new FieldEnemy
                {
                    gridPos = bossPos,
                    enemyData = scaledBoss,
                    isDefeated = false
                };
                bossEnemy.uiObject = CreateEnemySymbol(bossEnemy);
                fieldEnemies.Add(bossEnemy);
            }
        }
        else
        {
            foreach (var enemy in fieldEnemies)
            {
                if (!enemy.isDefeated && enemy.uiObject == null)
                {
                    enemy.uiObject = CreateEnemySymbol(enemy);
                }
            }
        }
    }

    /// <summary>
    /// フロアに応じて敵データをスケーリング（コピーを返す）
    /// </summary>
    private EnemyData ScaleEnemyForFloor(EnemyData original)
    {
        if (currentFloor <= 1) return original;
        var scaled = Instantiate(original);
        float mult = 1f + (currentFloor - 1) * 0.3f;
        scaled.maxHP = Mathf.RoundToInt(original.maxHP * mult);
        scaled.attackPower = Mathf.RoundToInt(original.attackPower * mult);
        return scaled;
    }

    /// <summary>
    /// 敵シンボルUI生成
    /// </summary>
    private GameObject CreateEnemySymbol(FieldEnemy enemy)
    {
        var go = new GameObject($"Enemy_{enemy.enemyData.displayKanji}");
        go.transform.SetParent(fieldContent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = GridToUIPosition(enemy.gridPos.x, enemy.gridPos.y);
        rect.sizeDelta = new Vector2(cellSize * 0.85f, cellSize * 0.85f);

        var bg = go.AddComponent<Image>();
        bool isBoss = enemy.enemyData.enemyType == EnemyType.Boss;
        bg.color = isBoss
            ? new Color(0.8f, 0.15f, 0.15f, 0.85f)
            : new Color(0.6f, 0.2f, 0.2f, 0.75f);
        bg.raycastTarget = false;

        var textGo = new GameObject("EnemyText");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = enemy.enemyData.displayKanji;
        text.fontSize = isBoss ? 32 : 28;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        if (appFont != null) text.font = appFont;
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // Outline追加（視認性向上）
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = isBoss ? new Color(0.5f, 0f, 0f, 0.8f) : new Color(0.2f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        return go;
    }

    // ============================================
    // 階段（次フロアへ）
    // ============================================

    private void CreateStairs()
    {
        if (fieldContent == null) return;

        stairsGridPos = new Vector2Int(gridWidth - 1, gridHeight - 1);

        stairsObject = new GameObject("Stairs");
        stairsObject.transform.SetParent(fieldContent, false);

        var rect = stairsObject.AddComponent<RectTransform>();
        rect.anchoredPosition = GridToUIPosition(stairsGridPos.x, stairsGridPos.y);
        rect.sizeDelta = new Vector2(cellSize * 0.9f, cellSize * 0.9f);

        var bg = stairsObject.AddComponent<Image>();
        bg.color = new Color(0.6f, 0.5f, 0.1f, 0.9f);
        bg.raycastTarget = false;

        var textGo = new GameObject("StairsText");
        textGo.transform.SetParent(stairsObject.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "▽";
        text.fontSize = 28;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.95f, 0.5f);
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        if (appFont != null) text.font = appFont;
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // 下に「次の階」テキスト
        var labelGo = new GameObject("StairsLabel");
        labelGo.transform.SetParent(stairsObject.transform, false);
        var labelText = labelGo.AddComponent<TextMeshProUGUI>();
        labelText.text = $"F{currentFloor + 1}↓";
        labelText.fontSize = 10;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = new Color(1f, 0.9f, 0.3f, 0.9f);
        labelText.raycastTarget = false;
        if (appFont != null) labelText.font = appFont;
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0.3f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Debug.Log($"[FieldManager] 階段を配置: {stairsGridPos}");
    }

    private void AdvanceFloor()
    {
        currentFloor++;
        defeatedOnCurrentFloor = 0;
        Debug.Log($"[FieldManager] フロア{currentFloor}へ進む！ 敵レベルアップ");

        // 鬼を退場
        if (oniChaseCoroutine != null) StopCoroutine(oniChaseCoroutine);
        oniChaseCoroutine = null;
        if (oniObject != null) { Destroy(oniObject); oniObject = null; }
        oniIsActive = false;

        // フィールドを新たに生成
        ShowField();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.fieldManager.UpdateStatusUI();
        }
    }

    // ============================================
    // 鬼（追跡者）システム
    // ============================================

    private void TrySpawnOni()
    {
        if (oniIsActive) return;

        oniIsActive = true;
        Debug.Log("[FieldManager] 鬼がスポーン！");
        SpawnOniSymbol();
        oniChaseCoroutine = StartCoroutine(OniChasePlayer());
    }

    private void SpawnOniSymbol()
    {
        if (fieldContent == null) return;

        // プレイヤーから離れた位置にスポーン
        oniGridPos = new Vector2Int(
            playerGridPos.x > gridWidth / 2 ? 0 : gridWidth - 1,
            playerGridPos.y > gridHeight / 2 ? 0 : gridHeight - 1
        );

        oniObject = new GameObject("Oni");
        oniObject.transform.SetParent(fieldContent, false);

        var rect = oniObject.AddComponent<RectTransform>();
        rect.anchoredPosition = GridToUIPosition(oniGridPos.x, oniGridPos.y);
        rect.sizeDelta = new Vector2(cellSize * 0.95f, cellSize * 0.95f);

        var bg = oniObject.AddComponent<Image>();
        bg.color = new Color(0.25f, 0f, 0.1f, 0.95f);
        bg.raycastTarget = false;

        var textGo = new GameObject("OniText");
        textGo.transform.SetParent(oniObject.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "鬼";
        text.fontSize = 36;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.1f, 0.1f);
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        if (appFont != null) text.font = appFont;
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // 赤黒オーラ（外側の発光）
        var auraGo = new GameObject("OniAura");
        auraGo.transform.SetParent(oniObject.transform, false);
        auraGo.transform.SetAsFirstSibling();
        var auraRect = auraGo.AddComponent<RectTransform>();
        auraRect.anchorMin = new Vector2(-0.15f, -0.15f);
        auraRect.anchorMax = new Vector2(1.15f, 1.15f);
        auraRect.offsetMin = Vector2.zero;
        auraRect.offsetMax = Vector2.zero;
        var auraImg = auraGo.AddComponent<Image>();
        auraImg.color = new Color(0.8f, 0f, 0f, 0.35f);
        auraImg.raycastTarget = false;

        // Outline
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.8f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        oniObject.transform.SetAsLastSibling();

        Debug.Log($"[FieldManager] 鬼スポーン位置: {oniGridPos}");
    }

    private IEnumerator OniChasePlayer()
    {
        while (oniIsActive)
        {
            yield return new WaitForSeconds(0.8f);

            if (oniObject == null || playerObject == null) yield break;
            if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Field) yield return null;

            // 1マスずつプレイヤーに近づく
            Vector2Int diff = playerGridPos - oniGridPos;
            Vector2Int step = Vector2Int.zero;

            if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y))
                step.x = diff.x > 0 ? 1 : -1;
            else if (diff.y != 0)
                step.y = diff.y > 0 ? 1 : -1;
            else if (diff.x != 0)
                step.x = diff.x > 0 ? 1 : -1;

            Vector2Int newPos = oniGridPos + step;
            newPos.x = Mathf.Clamp(newPos.x, 0, gridWidth - 1);
            newPos.y = Mathf.Clamp(newPos.y, 0, gridHeight - 1);
            oniGridPos = newPos;

            if (oniObject != null)
            {
                var rect = oniObject.GetComponent<RectTransform>();
                if (rect != null) rect.anchoredPosition = GridToUIPosition(oniGridPos.x, oniGridPos.y);
                oniObject.transform.SetAsLastSibling();
            }

            // 接触判定
            if (oniGridPos == playerGridPos)
            {
                Debug.Log("[FieldManager] 鬼と接触！強制バトル開始");
                StartOniBattle();
                yield break;
            }

            // パルス演出（赤く点滅）
            StartCoroutine(OniPulse());
        }
    }

    private IEnumerator OniPulse()
    {
        if (oniObject == null) yield break;
        var bg = oniObject.GetComponent<Image>();
        if (bg == null) yield break;
        Color original = bg.color;
        bg.color = new Color(0.5f, 0f, 0.2f, 0.95f);
        yield return new WaitForSeconds(0.15f);
        if (bg != null) bg.color = original;
    }

    private void StartOniBattle()
    {
        oniIsActive = false;
        if (oniChaseCoroutine != null) StopCoroutine(oniChaseCoroutine);
        oniChaseCoroutine = null;
        if (oniObject != null) { Destroy(oniObject); oniObject = null; }

        var bm = GameManager.Instance?.battleManager;
        if (bm == null) return;

        // 鬼用EnemyDataを生成（現フロアに合わせた強力な敵）
        var oniData = ScriptableObject.CreateInstance<EnemyData>();
        oniData.enemyName = "鬼（追跡者）";
        oniData.displayKanji = "鬼";
        oniData.maxHP = 80 + currentFloor * 20;
        oniData.attackPower = 12 + currentFloor * 3;
        oniData.enemyType = EnemyType.Boss;
        oniData.componentCount = 10;

        AddBattleFloorLabel();
        bm.StartBattle(oniData);
    }

    private void AddBattleFloorLabel()
    {
        // 戦闘ログに「鬼バトル」の注記を後から追加するため空メソッド
    }

    // ============================================
    // プレイヤーの移動処理
    // ============================================

    public bool TryMovePlayer(Vector2Int direction)
    {
        Vector2Int newPos = playerGridPos + direction;

        if (newPos.x < 0 || newPos.x >= gridWidth || newPos.y < 0 || newPos.y >= gridHeight)
            return false;

        playerGridPos = newPos;

        if (playerObject != null)
        {
            var rect = playerObject.GetComponent<RectTransform>();
            rect.anchoredPosition = GridToUIPosition(playerGridPos.x, playerGridPos.y);
            playerObject.transform.SetAsLastSibling();
        }

        // 階段チェック
        if (playerGridPos == stairsGridPos)
        {
            AdvanceFloor();
            return true;
        }

        // 鬼との接触チェック（移動先）
        if (oniIsActive && playerGridPos == oniGridPos)
        {
            Debug.Log("[FieldManager] 鬼に接触（移動）！強制バトル開始");
            StartOniBattle();
            return true;
        }

        CheckEncounter();

        return true;
    }

    /// <summary>
    /// エンカウントチェック
    /// </summary>
    private void CheckEncounter()
    {
        for (int i = fieldEnemies.Count - 1; i >= 0; i--)
        {
            var enemy = fieldEnemies[i];
            if (enemy.isDefeated) continue;

            if (enemy.gridPos == playerGridPos)
            {
                Debug.Log($"[FieldManager] エンカウント！ 敵: {enemy.enemyData.displayKanji}");
                currentEncounterIndex = i;

                var bm = GameManager.Instance?.battleManager;
                if (bm != null)
                {
                    bm.StartBattle(enemy.enemyData);
                }
                return;
            }
        }
    }

    /// <summary>
    /// 戦闘勝利後に呼ばれる：敵を撃破済みにする
    /// </summary>
    public void OnBattleWon()
    {
        if (currentEncounterIndex >= 0 && currentEncounterIndex < fieldEnemies.Count)
        {
            var enemy = fieldEnemies[currentEncounterIndex];
            enemy.isDefeated = true;
            if (enemy.uiObject != null)
            {
                Destroy(enemy.uiObject);
                enemy.uiObject = null;
            }
            Debug.Log($"[FieldManager] 敵撃破: {enemy.enemyData.displayKanji}");

            defeatedOnCurrentFloor++;
            Debug.Log($"[FieldManager] 今フロア撃破数: {defeatedOnCurrentFloor}/{OniSpawnThreshold}");

            // 鬼スポーン判定
            if (defeatedOnCurrentFloor >= OniSpawnThreshold && !oniIsActive)
            {
                Debug.Log("[FieldManager] 鬼スポーン条件達成！");
                TrySpawnOni();
            }
        }
        currentEncounterIndex = -1;

        UpdateStatusUI();

        bool allDefeated = true;
        foreach (var e in fieldEnemies)
        {
            if (!e.isDefeated) { allDefeated = false; break; }
        }
        if (allDefeated)
        {
            Debug.Log("[FieldManager] 全敵撃破！ フィールドクリア！");
        }
    }

    /// <summary>
    /// ステータスUI更新
    /// </summary>
    public void UpdateStatusUI()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (inventoryCountText != null)
            inventoryCountText.text = $"デッキ: {gm.inventory.Count}/{gm.inventoryMaxSize}";
        if (hpText != null)
            hpText.text = $"HP: {gm.playerHP}/{gm.playerMaxHP}";
        if (goldText != null)
            goldText.text = $"金: {gm.playerGold}G";

        // フロア表示
        if (floorText == null && fieldContent != null)
        {
            var go = new GameObject("FloorText");
            go.transform.SetParent(fieldContent.parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -10f);
            rect.sizeDelta = new Vector2(120f, 30f);
            floorText = go.AddComponent<TextMeshProUGUI>();
            floorText.fontSize = 18;
            floorText.color = new Color(1f, 0.9f, 0.4f);
            floorText.fontStyle = FontStyles.Bold;
            if (appFont != null) floorText.font = appFont;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.2f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }
        if (floorText != null)
            floorText.text = $"F{currentFloor}　鬼:{defeatedOnCurrentFloor}/{OniSpawnThreshold}";
    }

    /// <summary>
    /// グリッド座標→UI座標変換
    /// </summary>
    private Vector2 GridToUIPosition(int x, int y)
    {
        float startX = -(gridWidth * cellSize) / 2f + cellSize / 2f;
        float startY = -(gridHeight * cellSize) / 2f + cellSize / 2f;
        return new Vector2(startX + x * cellSize, startY + y * cellSize);
    }

    /// <summary>
    /// 指定座標が占有されているか
    /// </summary>
    private bool IsOccupied(Vector2Int pos)
    {
        if (pos == playerGridPos) return true;
        if (pos == stairsGridPos) return true;
        foreach (var e in fieldEnemies)
        {
            if (!e.isDefeated && e.gridPos == pos) return true;
        }
        return false;
    }
}
