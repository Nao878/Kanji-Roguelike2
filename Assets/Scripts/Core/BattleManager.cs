using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// ターン制戦闘管理
/// </summary>
public class BattleManager : MonoBehaviour
{
    [Header("戦闘状態")]
    public EnemyData currentEnemyData;
    public int enemyCurrentHP;
    public bool isPlayerTurn = true;
    public bool enemyIsStunned = false; // スタンフラグ
    public BattleState battleState = BattleState.Idle;

    public List<StatusEffect> playerStatusEffects = new List<StatusEffect>();
    public List<StatusEffect> enemyStatusEffects = new List<StatusEffect>();

    [Header("UI参照")]
    public TextMeshProUGUI playerHPText;
    public TextMeshProUGUI playerManaText;
    public TextMeshProUGUI enemyNameText;
    public TextMeshProUGUI enemyHPText;
    public TextMeshProUGUI battleLogText;
    public Button endTurnButton;
    public Transform handArea;

    // コンボ関連
    private string lastPlayedKanji = "";
    private CardElement lastPlayedElement = CardElement.None;
    private int elementChainCount = 0;

    // 同字連撃コンボ
    public int currentComboCount = 0;
    private string lastComboKanji = "";
    public string LastComboKanji => lastComboKanji;
    
    // 熟語によるボーナスダメージ等
    private System.Collections.Generic.Dictionary<string, int> jukugoCombos = new System.Collections.Generic.Dictionary<string, int>()
    {
        { "火炎", 15 },
        { "森林", 15 },
        { "明暗", 20 },
        { "白黒", 10 },
        { "休眠", 10 },
        { "目口", 5 },
        { "人民", 5 }
    };
    
    [Header("バトルUI")]
    public EnemyData[] normalEnemies;
    public EnemyData bossEnemy;

    [Header("BattleUI参照")]
    public BattleUI battleUI;

    [Header("ゲームオーバー")]
    public GameObject gameOverPanel;

    public enum BattleState
    {
        Idle,
        PlayerTurn,
        EnemyTurn,
        Won,
        Lost
    }

    void Start()
    {
        battleState = BattleState.Idle;
        isPlayerTurn = true;
        enemyIsStunned = false;
        currentEnemyData = null;
        enemyCurrentHP = 0;
        lastPlayedKanji = "";
        lastPlayedElement = CardElement.None;
        elementChainCount = 0;
        currentComboCount = 0;
        lastComboKanji = "";
        wolfBossManager = GetComponent<WolfBossManager>();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    /// <summary>
    /// 全データを初期化（リセット時用）
    /// </summary>
    public void ClearData()
    {
        currentEnemyData = null;
        enemyCurrentHP = 0;
        isPlayerTurn = true;
        enemyIsStunned = false;
        battleState = BattleState.Idle;
        lastPlayedKanji = "";
        lastPlayedElement = CardElement.None;
        elementChainCount = 0;
        currentComboCount = 0;
        lastComboKanji = "";
        playerStatusEffects.Clear();
        enemyStatusEffects.Clear();
    }

    [Header("BPM波紋エフェクト")]
    public BPMRippleEffect bpmRipple;

    private WolfBossManager wolfBossManager;
    private OniBossManager oniBossManager;

    /// <summary>
    /// 戦闘開始（3秒間の開戦トランジション演出を経て本戦闘を開始）
    /// </summary>
    public void StartBattle(EnemyData enemy)
    {
        if (enemy == null)
        {
            Debug.LogError("[BattleManager] 敵データがnullです！");
            return;
        }

        // 事前データセットアップ（トランジション中は表示されない）
        currentEnemyData = Instantiate(enemy);
        enemyCurrentHP = enemy.maxHP;
        isPlayerTurn = true;
        enemyIsStunned = false;
        lastPlayedKanji = "";
        lastPlayedElement = CardElement.None;
        elementChainCount = 0;
        currentComboCount = 0;
        lastComboKanji = "";
        playerStatusEffects.Clear();
        enemyStatusEffects.Clear();

        Debug.Log($"[BattleManager] 開戦演出開始！ 敵:{enemy.enemyName}（HP:{enemy.maxHP}）");

        // 狼ボス戦闘の場合は専用BGMを予約しWolfBossManagerを初期化
        if (enemy.isWolfBoss)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetWolfBossBGM();
            if (wolfBossManager == null) wolfBossManager = gameObject.AddComponent<WolfBossManager>();
            wolfBossManager.InitForWolfBoss();
        }

        // 鬼ボス戦闘の場合はBGMを予約し、OniBossManagerをアタッチ（UIは遷移後に初期化）
        if (enemy.isOniBoss)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetOniBossBGM();
            if (oniBossManager != null) { Destroy(oniBossManager); oniBossManager = null; }
            oniBossManager = gameObject.AddComponent<OniBossManager>();
        }

        // 3秒間の開戦トランジション演出（BGMはTransitionManager内で再生開始）
        if (BattleTransitionManager.Instance != null)
        {
            BattleTransitionManager.Instance.PlayBattleTransition(BeginBattleAfterTransition);
        }
        else
        {
            // TransitionManagerが存在しない場合は即座に開始
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBattleBGM();
            BeginBattleAfterTransition();
        }
    }

    /// <summary>
    /// トランジション完了後に実際の戦闘を開始
    /// </summary>
    private void BeginBattleAfterTransition()
    {
        battleState = BattleState.PlayerTurn;
        AddBattleLog($"『{currentEnemyData.displayKanji}』{currentEnemyData.enemyName}が現れた！");

        // 敵UIをリセット（前回のデスVFXで非表示になっていた場合の復元）
        if (battleUI != null)
        {
            battleUI.ResetEnemyDisplay();
        }

        // 鬼ボス: バトルUIが有効になった後にカウントダウンUIを初期化
        if (currentEnemyData != null && currentEnemyData.isOniBoss && oniBossManager != null)
        {
            oniBossManager.InitForOniBoss();
        }

        // GameManagerにステート変更を通知
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameState.Battle);
            GameManager.Instance.InitializeBattleDeck();
            GameManager.Instance.StartPlayerTurn();
        }

        UpdateUI();

        if (battleUI != null)
        {
            battleUI.UpdateHandUI();
            battleUI.UpdateStatusUI();
        }

        // BPM波紋エフェクトを起動
        SetupBPMRipple();
    }

    /// <summary>
    /// BPM波紋エフェクトをセットアップして起動
    /// </summary>
    private void SetupBPMRipple()
    {
        if (bpmRipple == null)
        {
            // まだ未生成なら自動生成
            bpmRipple = gameObject.AddComponent<BPMRippleEffect>();
        }

        // 敵の漢字テキストを追跡ターゲットに設定
        if (battleUI != null && battleUI.enemyKanjiText != null)
        {
            bpmRipple.enemyTransform = battleUI.enemyKanjiText.transform;
        }

        // Canvas参照（ScreenSpaceCamera の MainCanvas を優先）
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in allCanvases)
        {
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceCamera)
            {
                bpmRipple.targetCanvas = c;
                break;
            }
        }
        if (bpmRipple.targetCanvas == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas != null) bpmRipple.targetCanvas = canvas;
        }

        // 再生中の曲に合わせてBPMを同期
        if (AudioManager.Instance != null)
            bpmRipple.SetBPM(AudioManager.Instance.CurrentBattleBPM);
        bpmRipple.SetActive(true);
        Debug.Log("[BattleManager] BPM波紋エフェクト起動");
    }

    /// <summary>
    /// カードを使用して攻撃/効果を適用
    /// </summary>
    public void PlayCard(KanjiCardData card)
    {
        if (battleState != BattleState.PlayerTurn)
        {
            Debug.Log("[BattleManager] プレイヤーのターンではありません");
            return;
        }

        var gm = GameManager.Instance;
        if (gm == null || !gm.UseCard(card)) return;

        // カード効果を適用
        ApplyCardEffect(card);
        CheckBattleEnd();

        UpdateUI();
        if (battleUI != null)
        {
            battleUI.UpdateHandUI();
            battleUI.UpdateStatusUI();
        }
    }

    /// <summary>
    /// カード効果を適用（漢字ブレイク対応）
    /// </summary>
    private void ApplyCardEffect(KanjiCardData card)
    {
        var gm = GameManager.Instance;
        // modifier対応：攻撃系はattackModifier、防御系はdefenseModifierを加算
        int attackValue = card.effectValue + card.attackModifier + (card.effectType == CardEffectType.Attack ? gm.playerAttackBuff : 0);
        int defenseValue = card.effectValue + card.defenseModifier;

        // SE再生：決定アクション
        bool isAttackType = card.effectType == CardEffectType.Attack || card.effectType == CardEffectType.AttackAll || card.effectType == CardEffectType.Special;
        if (AudioManager.Instance != null)
        {
            if (isAttackType) AudioManager.Instance.PlaySE(AudioManager.Instance.seButton50);
            else AudioManager.Instance.PlaySE(AudioManager.Instance.seButton44);
        }

        // 状態異常付与カード（例：「毒」ならPoison、「癒」ならRegen）
        if (card.kanji == "毒" || card.kanji == "蛇" || card.kanji == "酸")
        {
            AddStatusEffect(false, StatusType.Poison, 2, 3);
            AddBattleLog($"<color=#9932CC>『{card.kanji}』で敵に毒(2)を3ターン付与！</color>");
        }
        else if (card.kanji == "癒" || card.kanji == "薬" || card.kanji == "命")
        {
            AddStatusEffect(true, StatusType.Regen, 3, 3);
            AddBattleLog($"<color=#32CD32>『{card.kanji}』で自身にリジェネ(3)を3ターン付与！</color>");
        }

        // 解毒カード
        if (card.kanji == "清" || card.kanji == "浄" || card.kanji == "祓" || card.kanji == "解" || card.kanji == "薬")
        {
            CleanseDebuffs(true);
        }

        // 【タスク2】漢字ブレイク：相殺 (Mirror Clash)
        bool isMirrorClash = false;
        if (card.kanji == currentEnemyData.displayKanji)
        {
            isMirrorClash = true;
            attackValue *= 3; // 確定クリティカル（特大ダメージ）
            // Mirror Clash特典：ボーナスドロー1枚（合体AP回復とは別の特殊ボーナス）
            if (gm.hand.Count < gm.initialHandSize) gm.DrawFromDeck(1);
            AddBattleLog("<color=#FF0000><b>相殺（Mirror Clash）発動！特大ダメージ ＆ ボーナスドロー！</b></color>");
            if (VFXManager.Instance != null && battleUI != null)
            {
                VFXManager.Instance.PlayComboEffect(battleUI.gameObject, "MIRROR CLASH!!", Color.red);
                // CFXR特大ダメージエフェクト + 強カメラシェイク
                if (battleUI.enemyKanjiText != null)
                    VFXManager.Instance.PlayCriticalHitVFX(battleUI.enemyKanjiText.transform.position);
            }
        }

        // 【タスク2】漢字ブレイク：構成数マウント (Overpower)
        if (!isMirrorClash && card.componentCount > currentEnemyData.componentCount)
        {
            attackValue = Mathf.CeilToInt(attackValue * 1.5f);
            AddBattleLog($"<color=#FFA500>構成数マウント（Overpower）！ ダメージ1.5倍！({card.componentCount} vs {currentEnemyData.componentCount})</color>");
            if (VFXManager.Instance != null && battleUI != null)
            {
                VFXManager.Instance.PlayComboEffect(battleUI.gameObject, "OVERPOWER!!", new Color(1f, 0.5f, 0f));
                // CFXR特大ダメージエフェクト + 強カメラシェイク
                if (battleUI.enemyKanjiText != null)
                    VFXManager.Instance.PlayCriticalHitVFX(battleUI.enemyKanjiText.transform.position);
            }
        }

        // コンボ判定：熟語
        string comboStr = lastPlayedKanji + card.kanji;
        int jukugoBonusDmg = 0;
        if (jukugoCombos.ContainsKey(comboStr))
        {
            jukugoBonusDmg = jukugoCombos[comboStr];
            attackValue += jukugoBonusDmg;
            AddBattleLog($"<color=#FFD700>熟語コンボ発動！『{comboStr}』ボーナス+{jukugoBonusDmg}</color>");
            if (VFXManager.Instance != null && battleUI != null)
            {
                VFXManager.Instance.PlayComboEffect(battleUI.gameObject, $"熟語コンボ\n{comboStr}!!", new Color(1f, 0.8f, 0f));
            }
        }

        // コンボ判定：属性チェイン

        if (card.element != CardElement.None && card.element == lastPlayedElement)
        {
            elementChainCount++;

            int elementBonus = elementChainCount * 2;
            attackValue += elementBonus;
            AddBattleLog($"<color=#00FFFF>属性チェイン！({card.element}) ボーナス+{elementBonus}</color>");
            if (VFXManager.Instance != null && battleUI != null)
            {
                VFXManager.Instance.PlayComboEffect(battleUI.gameObject, $"{elementChainCount} CHAIN!!\n{card.element}", new Color(0f, 1f, 1f));
            }
        }
        else
        {
            elementChainCount = 1;
        }

        // 同字連撃コンボ判定（Attack 系カードのみ）
        if (isAttackType)
        {
            if (!string.IsNullOrEmpty(lastComboKanji) && card.kanji == lastComboKanji)
            {
                currentComboCount++;
                if (currentComboCount >= 3)
                {
                    attackValue = Mathf.CeilToInt(attackValue * 2.0f);
                    AddBattleLog($"<color=#FF44FF><b>{currentComboCount} COMBO!! ×2.0!!</b></color>");
                    if (VFXManager.Instance != null && battleUI != null)
                    {
                        VFXManager.Instance.PlayComboEffect(
                            battleUI.gameObject,
                            $"{currentComboCount} COMBO!!\n×2.0 DAMAGE!",
                            new Color(1f, 0.2f, 1f));
                        if (battleUI.enemyKanjiText != null)
                            VFXManager.Instance.PlayCriticalHitVFX(battleUI.enemyKanjiText.transform.position);
                    }
                }
                else if (currentComboCount == 2)
                {
                    attackValue = Mathf.CeilToInt(attackValue * 1.5f);
                    AddBattleLog($"<color=#FF88FF><b>2 COMBO! ×1.5 DAMAGE!</b></color>");
                    if (VFXManager.Instance != null && battleUI != null)
                    {
                        VFXManager.Instance.PlayComboEffect(
                            battleUI.gameObject,
                            $"2 COMBO!\n×1.5 DAMAGE!",
                            new Color(1f, 0.5f, 1f));
                    }
                }
            }
            else
            {
                currentComboCount = 1;
            }
            lastComboKanji = card.kanji;
        }

        // 次回コンボの布石を記憶
        lastPlayedKanji = card.kanji;
        lastPlayedElement = card.element;

        switch (card.effectType)
        {
            case CardEffectType.Attack:
                enemyCurrentHP = Mathf.Max(0, enemyCurrentHP - attackValue);
                AddBattleLog($"『{card.DisplayName}』で{attackValue}ダメージ！");
                if (battleUI != null && battleUI.enemyKanjiText != null && VFXManager.Instance != null)
                {
                    VFXManager.Instance.PlayDamageEffect(battleUI.enemyKanjiText.gameObject, attackValue);
                    // CFXR通常攻撃ヒットエフェクト（ブレイク発動時以外）
                    if (!isMirrorClash && card.componentCount <= (currentEnemyData != null ? currentEnemyData.componentCount : 999))
                        VFXManager.Instance.PlayAttackHitVFX(battleUI.enemyKanjiText.transform.position);
                }
                // 血カードの自傷ダメージ
                if (wolfBossManager != null && wolfBossManager.IsBloodCard(card))
                {
                    wolfBossManager.ApplyBloodSelfDamage();
                    if (battleUI != null && VFXManager.Instance != null && battleUI.playerManaText != null)
                        VFXManager.Instance.PlayDamageEffect(battleUI.playerManaText.gameObject, wolfBossManager.bloodDamage, true);
                }
                break;

            case CardEffectType.Defense:
                gm.playerDefenseBuff += defenseValue;
                AddBattleLog($"『{card.DisplayName}』で防御力+{defenseValue}！");
                if (VFXManager.Instance != null && battleUI != null && battleUI.playerManaText != null)
                    VFXManager.Instance.PlayDefenseVFX(battleUI.playerManaText.transform.position);
                break;

            case CardEffectType.Heal:
                // HP回復 → シールド1枚追加（シールドライフ化）
                KanjiCardData newShield = null;
                if (gm.drawPile.Count > 0) { newShield = gm.drawPile[0]; gm.drawPile.RemoveAt(0); }
                else if (gm.discardPile.Count > 0) { newShield = gm.discardPile[gm.discardPile.Count - 1]; gm.discardPile.RemoveAt(gm.discardPile.Count - 1); }
                else if (gm.inventory.Count > 0) { newShield = gm.inventory[UnityEngine.Random.Range(0, gm.inventory.Count)]; }
                if (newShield != null)
                {
                    gm.shields.Add(newShield);
                    AddBattleLog($"<color=#00AAFF>『{card.DisplayName}』でシールドを1枚追加！（合計{gm.shields.Count}枚）</color>");
                    if (battleUI != null) battleUI.UpdateShieldUI();
                }
                else
                {
                    AddBattleLog($"<color=#888888>『{card.DisplayName}』を使用したが、追加するカードがない。</color>");
                }
                if (VFXManager.Instance != null && battleUI != null && battleUI.playerManaText != null)
                    VFXManager.Instance.PlayDefenseVFX(battleUI.playerManaText.transform.position);
                break;

            case CardEffectType.Buff:
                int buffVal = card.effectValue + card.attackModifier;
                gm.playerAttackBuff += buffVal;
                AddBattleLog($"『{card.DisplayName}』で攻撃力+{buffVal}！");
                break;

            case CardEffectType.Special:
                int spAtkVal = attackValue;
                enemyCurrentHP = Mathf.Max(0, enemyCurrentHP - spAtkVal);
                int healAmount = Mathf.CeilToInt(spAtkVal * 0.6f);
                gm.Heal(healAmount); // HPシステム廃止済み → シールド1枚追加
                AddBattleLog($"『{card.DisplayName}』で{spAtkVal}ダメージ＋シールド吸収！");
                if (battleUI != null && battleUI.enemyKanjiText != null && VFXManager.Instance != null)
                {
                    VFXManager.Instance.PlayDamageEffect(battleUI.enemyKanjiText.gameObject, spAtkVal);
                    if (!isMirrorClash)
                        VFXManager.Instance.PlayAttackHitVFX(battleUI.enemyKanjiText.transform.position);
                    if (battleUI.playerManaText != null)
                        VFXManager.Instance.SpawnHealNumber(battleUI.playerManaText.transform.position, healAmount);
                }
                break;

            case CardEffectType.Draw:
                gm.DrawFromDeck(card.effectValue);
                AddBattleLog($"『{card.DisplayName}』でカードを{card.effectValue}枚ドロー！");
                break;

            case CardEffectType.AttackAll:
                enemyCurrentHP = Mathf.Max(0, enemyCurrentHP - attackValue);
                AddBattleLog($"『{card.DisplayName}』で敵全体に{attackValue}ダメージ！");
                if (battleUI != null && battleUI.enemyKanjiText != null && VFXManager.Instance != null)
                {
                    VFXManager.Instance.PlayDamageEffect(battleUI.enemyKanjiText.gameObject, attackValue);
                    // CFXR通常攻撃ヒットエフェクト
                    VFXManager.Instance.PlayAttackHitVFX(battleUI.enemyKanjiText.transform.position);
                }
                break;

            case CardEffectType.Stun:
                enemyIsStunned = true;
                AddBattleLog($"『{card.DisplayName}』で敵をスタンさせた！");
                if (VFXManager.Instance != null && battleUI != null && battleUI.enemyKanjiText != null)
                    VFXManager.Instance.PlayStunVFX(battleUI.enemyKanjiText.transform.position);
                break;

            case CardEffectType.Shop:
                AddBattleLog($"『{card.DisplayName}』で店が開いた！好きなカードを1枚選べ！");
                if (battleUI != null) battleUI.ShowShopSelection();
                break;
        }
    }


    /// <summary>
    /// ターン終了
    /// </summary>
    public void EndPlayerTurn()
    {
        if (battleState != BattleState.PlayerTurn) return;

        // ターン終了でコンボリセット
        currentComboCount = 0;
        lastComboKanji = "";

        battleState = BattleState.EnemyTurn;
        isPlayerTurn = false;
        Debug.Log("[BattleManager] プレイヤーターン終了");

        // 敵ターン実行
        ExecuteEnemyTurn();
    }

    /// <summary>
    /// 敵ターン実行
    /// </summary>
    private void ExecuteEnemyTurn()
    {
        if (currentEnemyData == null) return;

        // 状態異常処理（敵）
        ProcessStatusEffects(false);
        if (enemyCurrentHP <= 0)
        {
            CheckBattleEnd();
            return;
        }

        // スタンチェック
        if (enemyIsStunned)
        {
            AddBattleLog($"敵はスタンしていて動けない！");
            enemyIsStunned = false; // スタン解除

            // プレイヤーのターンへ戻る
            Invoke(nameof(FinishEnemyTurn), 1.0f);
            return;
        }

        if (!currentEnemyData.isOniBoss)
        {
            int damage = currentEnemyData.attackPower;
            AddBattleLog($"敵の攻撃！ {damage}ダメージ！");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.TakeDamage(damage);
            }

            Debug.Log($"[BattleManager] 敵が{damage}ダメージの攻撃");

            if (battleUI != null && VFXManager.Instance != null)
            {
                GameObject target = battleUI.playerManaText != null ? battleUI.playerManaText.gameObject : battleUI.gameObject;
                VFXManager.Instance.PlayDamageEffect(target, damage, true);
            }
        }

        CheckBattleEnd();

        if (battleState == BattleState.EnemyTurn)
        {
            // 狼ボス専用行動（手札破壊・血カード生成）
            if (currentEnemyData.isWolfBoss && wolfBossManager != null)
            {
                wolfBossManager.OnWolfTurnAction(FinishEnemyTurn);
            }
            else if (currentEnemyData.isOniBoss && oniBossManager != null)
            {
                oniBossManager.OnOniTurnAction(FinishEnemyTurn);
            }
            else
            {
                FinishEnemyTurn();
            }
        }
        else
        {
            UpdateUI();
            if (battleUI != null) { battleUI.UpdateHandUI(); battleUI.UpdateStatusUI(); }
        }
    }

    private void FinishEnemyTurn()
    {
        // 新ターン開始でコンボリセット
        currentComboCount = 0;
        lastComboKanji = "";

        battleState = BattleState.PlayerTurn;
        isPlayerTurn = true;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartPlayerTurn();
        }

        // 状態異常処理（プレイヤー）
        ProcessStatusEffects(true);
        if (GameManager.Instance != null && GameManager.Instance.playerHP <= 0)
        {
            CheckBattleEnd();
            return;
        }

        UpdateUI();
        
        // BattleUI側も確実に更新する
        if (battleUI != null)
        {
            battleUI.UpdateHandUI();
            battleUI.UpdateStatusUI();
        }
    }

    /// <summary>
    /// 戦闘終了チェック
    /// </summary>
    private void CheckBattleEnd()
    {
        if (enemyCurrentHP <= 0)
        {
            // 狼ボス第二形態チェック
            if (currentEnemyData != null && currentEnemyData.isWolfBoss && wolfBossManager != null)
            {
                if (wolfBossManager.CheckPhase2Transition())
                {
                    // 第二形態に移行したので戦闘続行
                    return;
                }
            }

            battleState = BattleState.Won;
            int goldReward = 15;
            KanjiCardData droppedCard = null;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.playerGold += goldReward;

                if (currentEnemyData.dropCard != null)
                {
                    bool added = GameManager.Instance.AddToInventory(currentEnemyData.dropCard);
                    if (added)
                    {
                        droppedCard = currentEnemyData.dropCard;
                        AddBattleLog($"<color=#FFD700>『{currentEnemyData.dropCard.kanji}』を手に入れた！</color>");
                    }
                    else
                    {
                        AddBattleLog($"<color=#FF6666>インベントリが満杯…『{currentEnemyData.dropCard.kanji}』を諦めた</color>");
                    }
                }
                else if (currentEnemyData.enemyType == EnemyType.Normal)
                {
                    // 雑魚敵はdropCardが未設定でも敵の漢字カードを自動ドロップ
                    var autoCard = ScriptableObject.CreateInstance<KanjiCardData>();
                    autoCard.kanji = currentEnemyData.displayKanji;
                    autoCard.cardName = currentEnemyData.enemyName;
                    autoCard.description = "戦利品";
                    autoCard.cost = 1;
                    autoCard.effectValue = 3;
                    autoCard.effectType = CardEffectType.Attack;
                    autoCard.componentCount = currentEnemyData.componentCount;
                    autoCard.cardId = 9000 + Mathf.Abs(currentEnemyData.displayKanji.GetHashCode()) % 999;
                    bool added = GameManager.Instance.AddToInventory(autoCard);
                    if (added)
                    {
                        droppedCard = autoCard;
                        AddBattleLog($"<color=#FFD700>『{autoCard.kanji}』を手に入れた！</color>");
                    }
                    else
                    {
                        AddBattleLog($"<color=#FF6666>インベントリが満杯…</color>");
                    }
                }
            }
            AddBattleLog($"勝利！ {goldReward}G獲得！");
            Debug.Log($"[BattleManager] 戦闘勝利！ {goldReward}G獲得");

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE(AudioManager.Instance.seBlow3);

            // CFXR敵討伐エフェクト → ResultUI → フィールドへ戻る
            if (VFXManager.Instance != null && battleUI != null && battleUI.enemyKanjiText != null)
            {
                KanjiCardData capturedDrop = droppedCard;
                VFXManager.Instance.PlayEnemyDeathVFX(battleUI.enemyKanjiText.transform.position, () =>
                {
                    if (battleUI.enemyArea != null) battleUI.enemyArea.SetActive(false);
                    ShowResultUI(capturedDrop, ReturnToField);
                });
            }
            else
            {
                ShowResultUI(droppedCard, () => Invoke(nameof(ReturnToField), 1.5f));
            }
        }
        else if (GameManager.Instance != null && GameManager.Instance.playerHP <= 0)
        {
            battleState = BattleState.Lost;
            AddBattleLog("敗北...");
            Debug.Log("[BattleManager] 戦闘敗北...");

            // BPM波紋エフェクト停止
            if (bpmRipple != null) bpmRipple.SetActive(false);

            // ゲームオーバーパネルを表示
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }
            else if (GameManager.Instance != null && GameManager.Instance.gameOverPanel != null)
            {
                GameManager.Instance.gameOverPanel.SetActive(true);
            }
        }
    }

    /// <summary>
    /// ペルソナ風リザルト画面を表示してからコールバックを呼ぶ
    /// </summary>
    private void ShowResultUI(KanjiCardData droppedCard, System.Action onComplete)
    {
        if (battleUI == null) { onComplete?.Invoke(); return; }
        StartCoroutine(CoShowResultUI(droppedCard, onComplete));
    }

    private System.Collections.IEnumerator CoShowResultUI(KanjiCardData droppedCard, System.Action onComplete)
    {
        var canvas = battleUI.GetComponentInParent<Canvas>();
        if (canvas == null) { onComplete?.Invoke(); yield break; }

        var panelGo = new GameObject("ResultPanel");
        panelGo.transform.SetParent(canvas.transform, false);
        panelGo.transform.SetAsLastSibling();

        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.25f);
        panelRect.anchorMax = new Vector2(0.85f, 0.75f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelBg = panelGo.AddComponent<UnityEngine.UI.Image>();
        panelBg.color = new Color(0.04f, 0.04f, 0.1f, 0.97f);

        // 装飾ライン（上）
        var lineTopGo = new GameObject("LineTop");
        lineTopGo.transform.SetParent(panelGo.transform, false);
        var lineTopRect = lineTopGo.AddComponent<RectTransform>();
        lineTopRect.anchorMin = new Vector2(0.05f, 0.88f);
        lineTopRect.anchorMax = new Vector2(0.95f, 0.9f);
        lineTopRect.offsetMin = Vector2.zero;
        lineTopRect.offsetMax = Vector2.zero;
        lineTopGo.AddComponent<UnityEngine.UI.Image>().color = new Color(1f, 0.85f, 0.1f, 0.8f);

        // 装飾ライン（下）
        var lineBotGo = new GameObject("LineBot");
        lineBotGo.transform.SetParent(panelGo.transform, false);
        var lineBotRect = lineBotGo.AddComponent<RectTransform>();
        lineBotRect.anchorMin = new Vector2(0.05f, 0.1f);
        lineBotRect.anchorMax = new Vector2(0.95f, 0.12f);
        lineBotRect.offsetMin = Vector2.zero;
        lineBotRect.offsetMax = Vector2.zero;
        lineBotGo.AddComponent<UnityEngine.UI.Image>().color = new Color(1f, 0.85f, 0.1f, 0.8f);

        // 勝利テキスト
        var winGo = new GameObject("WinText");
        winGo.transform.SetParent(panelGo.transform, false);
        var winRect = winGo.AddComponent<RectTransform>();
        winRect.anchorMin = new Vector2(0f, 0.55f);
        winRect.anchorMax = new Vector2(1f, 0.92f);
        winRect.offsetMin = Vector2.zero;
        winRect.offsetMax = Vector2.zero;
        var winTmp = winGo.AddComponent<TMPro.TextMeshProUGUI>();
        winTmp.text = "勝　利！";
        winTmp.fontSize = 52;
        winTmp.fontStyle = TMPro.FontStyles.Bold;
        winTmp.alignment = TMPro.TextAlignmentOptions.Center;
        winTmp.color = new Color(1f, 0.92f, 0.15f);
        if (battleUI.appFont != null) winTmp.font = battleUI.appFont;

        // 獲得カードテキスト
        if (droppedCard != null)
        {
            var rewardGo = new GameObject("RewardText");
            rewardGo.transform.SetParent(panelGo.transform, false);
            var rewardRect = rewardGo.AddComponent<RectTransform>();
            rewardRect.anchorMin = new Vector2(0f, 0.15f);
            rewardRect.anchorMax = new Vector2(1f, 0.55f);
            rewardRect.offsetMin = Vector2.zero;
            rewardRect.offsetMax = Vector2.zero;
            var rewardTmp = rewardGo.AddComponent<TMPro.TextMeshProUGUI>();
            rewardTmp.text = $"獲得: 『{droppedCard.kanji}』";
            rewardTmp.fontSize = 30;
            rewardTmp.alignment = TMPro.TextAlignmentOptions.Center;
            rewardTmp.color = new Color(1f, 0.75f, 0.4f);
            if (battleUI.appFont != null) rewardTmp.font = battleUI.appFont;
        }

        var cg = panelGo.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // フェードイン
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(t / 0.25f);
            yield return null;
        }
        cg.alpha = 1f;

        yield return new WaitForSeconds(1.3f);

        // フェードアウト
        t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            cg.alpha = 1f - Mathf.Clamp01(t / 0.25f);
            yield return null;
        }

        Destroy(panelGo);
        onComplete?.Invoke();
    }

    private void ReturnToField()
    {
        if (bpmRipple != null) bpmRipple.SetActive(false);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayFieldBGM();

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.routeMapManager != null)
            {
                GameManager.Instance.routeMapManager.OnBattleWon();
            }
            else if (GameManager.Instance.fieldManager != null)
            {
                GameManager.Instance.fieldManager.OnBattleWon();
            }
            GameManager.Instance.ChangeState(GameState.Field);
        }
    }

    /// <summary>
    /// バトルログにテキストを追加
    /// </summary>
    public void AddBattleLog(string message)
    {
        if (battleLogText != null)
        {
            battleLogText.text = message + "\n" + battleLogText.text;
            // ログが長すぎたら切り詰め
            if (battleLogText.text.Length > 500)
            {
                battleLogText.text = battleLogText.text.Substring(0, 500);
            }
        }
    }

    /// <summary>
    /// 敵との強制合体（Enemy Fusion Break）
    /// </summary>
    public bool TryEnemyFusion(KanjiCardData playerCard)
    {
        var gm = GameManager.Instance;
        if (gm == null || currentEnemyData == null || playerCard == null) return false;

        // 敵の漢字データを取得
        var enemyCardData = gm.GetCardByKanji(currentEnemyData.displayKanji);
        if (enemyCardData == null) return false;

        // 合体レシピを検索
        int resultId = gm.FindFusionResult(enemyCardData.cardId, playerCard.cardId);
        if (resultId != -1)
        {
            var resultCard = gm.GetCardById(resultId);
            if (resultCard != null)
            {
                // 【タスク2-3】敵の姿と名前を変化させる
                AddBattleLog($"<color=#FF00FF><b>敵との強制合体！『{currentEnemyData.displayKanji}』＋『{playerCard.kanji}』＝『{resultCard.kanji}』</b></color>");
                
                // 敵の状態を更新
                currentEnemyData.displayKanji = resultCard.kanji;
                currentEnemyData.enemyName = resultCard.cardName + "化した敵";
                currentEnemyData.componentCount = resultCard.componentCount;
                
                // 変化した敵はスタン ＆ ボーナスドロー2枚
                enemyIsStunned = true;
                int fusionDraw = Mathf.Min(2, gm.initialHandSize - gm.hand.Count);
                if (fusionDraw > 0) gm.DrawFromDeck(fusionDraw);

                AddBattleLog($"敵はスタン状態になり、{fusionDraw}枚ドロー！");

                if (VFXManager.Instance != null && battleUI != null)
                {
                    VFXManager.Instance.PlayComboEffect(battleUI.gameObject, "ENEMY FUSION BREAK!!", Color.magenta);
                }
                
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE(AudioManager.Instance.seButton50);

                // カードを消費（捨て札へ）
                gm.UseCard(playerCard);

                UpdateUI();
                if (battleUI != null)
                {
                    battleUI.UpdateHandUI();
                    battleUI.UpdateStatusUI();
                }
                
                CheckBattleEnd();
                return true;
            }
        }

        return false;
    }

    public void UpdateUI()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // プレイヤーHPシステム廃止 - HP表示を非表示
        if (playerHPText != null) playerHPText.gameObject.SetActive(false);

        if (playerManaText != null)
        {
            playerManaText.gameObject.SetActive(true);
            playerManaText.text = $"AP: {gm.playerMana}";
        }

        if (currentEnemyData != null)
        {
            if (enemyNameText != null) enemyNameText.text = $"{currentEnemyData.displayKanji} {currentEnemyData.enemyName}";
            if (enemyHPText != null) enemyHPText.text = $"HP: {enemyCurrentHP}/{currentEnemyData.maxHP}";
        }
    }

    /// <summary>
    /// ランダムな通常敵で戦闘開始
    /// </summary>
    public void StartRandomBattle()
    {
        if (normalEnemies != null && normalEnemies.Length > 0)
        {
            var enemy = normalEnemies[Random.Range(0, normalEnemies.Length)];
            StartBattle(enemy);
        }
        else
        {
            Debug.LogWarning("[BattleManager] 敵データが設定されていません");
        }
    }

    /// <summary>
    /// 逃走処理：カード1枚永久削除して戦闘離脱
    /// </summary>
    public void FleeFromBattle()
    {
        if (battleState != BattleState.PlayerTurn) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        // カード1枚をロスト（手札＋山札＋捨て札から選ぶ）
        var allCards = new System.Collections.Generic.List<KanjiCardData>();
        allCards.AddRange(gm.hand);
        allCards.AddRange(gm.drawPile);
        allCards.AddRange(gm.discardPile);

        string lostCardName = "なし";
        if (allCards.Count > 0)
        {
            int idx = Random.Range(0, allCards.Count);
            var lostCard = allCards[idx];
            lostCardName = lostCard.kanji;

            // 全リストから削除
            gm.hand.Remove(lostCard);
            gm.drawPile.Remove(lostCard);
            gm.discardPile.Remove(lostCard);
            gm.inventory.Remove(lostCard);
            if (gm.deckManager != null)
                gm.deckManager.currentDeck.Remove(lostCard);

            AddBattleLog($"<color=#FF6666><b>逃走のコスト！『{lostCard.kanji}』が永久に失われた…</b></color>");
            Debug.Log($"[BattleManager] 逃走カードロスト: {lostCard.kanji}");
        }

        battleState = BattleState.Won; // 戦闘終了扱い

        // BPM波紋エフェクト停止
        if (bpmRipple != null) bpmRipple.SetActive(false);

        // BGMフェードしてフィールドBGMへ
        if (AudioManager.Instance != null) AudioManager.Instance.PlayFieldBGM();

        // ボス戦からの逃走：シンボルは残す（OnBattleLostで処理）
        bool isBoss = currentEnemyData != null &&
            (currentEnemyData.enemyType == EnemyType.Boss || currentEnemyData.isWolfBoss);

        if (gm.fieldManager != null)
        {
            if (isBoss)
            {
                gm.fieldManager.OnBattleLost();
            }
            else
            {
                gm.fieldManager.OnBattleWon();
            }
        }

        gm.ChangeState(GameState.Field);
        Debug.Log($"[BattleManager] 逃走成功！ カードロスト:『{lostCardName}』");
    }

    // --- Status Effect System ---
    public void AddStatusEffect(bool isPlayer, StatusType type, int value, int duration)
    {
        var targetList = isPlayer ? playerStatusEffects : enemyStatusEffects;
        // 既存のものがあれば上書き
        var existing = targetList.Find(s => s.Type == type);
        if (existing != null)
        {
            existing.Value = value;
            existing.Duration = duration;
        }
        else
        {
            targetList.Add(new StatusEffect { Type = type, Value = value, Duration = duration });
        }
    }

    public void ProcessStatusEffects(bool isPlayer)
    {
        var targetList = isPlayer ? playerStatusEffects : enemyStatusEffects;
        for (int i = targetList.Count - 1; i >= 0; i--)
        {
            var effect = targetList[i];
            
            if (effect.Type == StatusType.Poison)
            {
                if (isPlayer)
                {
                    if (GameManager.Instance != null) GameManager.Instance.TakeDamage(effect.Value);
                    AddBattleLog($"<color=#9932CC>毒ダメージ！ プレイヤーは{effect.Value}のダメージを受けた。</color>");
                    if (battleUI != null && VFXManager.Instance != null && battleUI.playerManaText != null)
                        VFXManager.Instance.PlayDamageEffect(battleUI.playerManaText.gameObject, effect.Value, true);
                }
                else
                {
                    enemyCurrentHP = Mathf.Max(0, enemyCurrentHP - effect.Value);
                    AddBattleLog($"<color=#9932CC>毒ダメージ！ 敵に{effect.Value}のダメージ！</color>");
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySE(AudioManager.Instance.seButton38);
                    if (battleUI != null && VFXManager.Instance != null && battleUI.enemyKanjiText != null)
                        VFXManager.Instance.PlayDamageEffect(battleUI.enemyKanjiText.gameObject, effect.Value);
                }
            }
            else if (effect.Type == StatusType.Regen)
            {
                if (isPlayer)
                {
                    if (GameManager.Instance != null) GameManager.Instance.Heal(effect.Value);
                    AddBattleLog($"<color=#32CD32>リジェネ回復！ シールドが1枚追加された。</color>");
                    if (battleUI != null && VFXManager.Instance != null && battleUI.playerManaText != null)
                    {
                        VFXManager.Instance.PlayHealVFX(battleUI.playerManaText.transform.position);
                        VFXManager.Instance.SpawnHealNumber(battleUI.playerManaText.transform.position, 1);
                    }
                }
                else
                {
                    if (currentEnemyData != null)
                    {
                        enemyCurrentHP = Mathf.Min(currentEnemyData.maxHP, enemyCurrentHP + effect.Value);
                        AddBattleLog($"<color=#32CD32>リジェネ回復！ 敵は{effect.Value}回復した。</color>");
                    }
                }
            }

            effect.Duration--;
            if (effect.Duration <= 0)
            {
                targetList.RemoveAt(i);
            }
        }
    }

    public void CleanseDebuffs(bool isPlayer)
    {
        var targetList = isPlayer ? playerStatusEffects : enemyStatusEffects;
        int removed = targetList.RemoveAll(s => s.Type == StatusType.Poison); // Poisonはデバフ
        if (removed > 0)
        {
            AddBattleLog($"<color=#00FFFF>解毒効果発動！ 毒状態が解除された！</color>");
        }
    }
}

public enum StatusType
{
    Poison,
    Regen
}

[System.Serializable]
public class StatusEffect
{
    public StatusType Type;
    public int Value;
    public int Duration;
}
