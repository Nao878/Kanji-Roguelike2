using System.Collections;
using UnityEngine;

/// <summary>
/// 狼ボス専用行動マネージャー
/// BattleManagerと同じGameObjectにアタッチして使用
/// - 二段階フェーズ（第一形態→第二形態HP99）
/// - 特殊カード「血」の生成
/// - 戦闘中回復の廃止
/// </summary>
public class WolfBossManager : MonoBehaviour
{
    [Header("手札破壊設定")]
    [Tooltip("ターンごとの手札破壊発動確率 (%)")]
    public int handDestroyChancePercent = 35;
    [Tooltip("この間隔ターンでは確定発動")]
    public int handDestroyEveryNTurns = 3;

    [Header("第二形態設定")]
    [Tooltip("第二形態のHP")]
    public int phase2HP = 99;

    [Header("血カード設定")]
    [Tooltip("血カードが生成されるまでのターン間隔")]
    public int bloodCardInterval = 3;
    [Tooltip("血カードの自傷・攻撃ダメージ")]
    public int bloodDamage = 3;

    private int _turnCount = 0;
    private bool _hasEnteredPhase2 = false;
    private KanjiCardData _errorCard;
    private KanjiCardData _bloodCard;
    private BattleManager _bm;
    private BattleUI _battleUI;

    public bool HasEnteredPhase2 => _hasEnteredPhase2;
    // 後方互換
    public bool HasEnraged => _hasEnteredPhase2;

    private void Awake()
    {
        _bm = GetComponent<BattleManager>();
    }

    public void InitForWolfBoss()
    {
        _turnCount = 0;
        _hasEnteredPhase2 = false;
        _errorCard = null;
        _bloodCard = null;
        _battleUI = _bm?.battleUI;

        // 第一形態：プレイヤーの現在HPと同程度に設定
        if (_bm != null && _bm.currentEnemyData != null && GameManager.Instance != null)
        {
            int playerHP = GameManager.Instance.playerHP;
            _bm.currentEnemyData.maxHP = playerHP;
            _bm.enemyCurrentHP = playerHP;
            Debug.Log($"[WolfBossManager] 狼ボス戦開始！ 第一形態HP={playerHP}（プレイヤーHP準拠）");
        }
        else
        {
            Debug.Log("[WolfBossManager] 狼ボス戦開始！");
        }
    }

    private KanjiCardData GetOrCreateErrorCard()
    {
        if (_errorCard != null) return _errorCard;
        _errorCard = ScriptableObject.CreateInstance<KanjiCardData>();
        _errorCard.kanji = "※";
        _errorCard.cardName = "404 ERROR";
        _errorCard.description = "使用不可・合体不可（狼に破壊された）";
        _errorCard.cost = 99;
        _errorCard.effectValue = 0;
        _errorCard.effectType = CardEffectType.Debuff;
        return _errorCard;
    }

    /// <summary>
    /// 特殊カード「血」を生成
    /// </summary>
    private KanjiCardData GetOrCreateBloodCard()
    {
        if (_bloodCard != null) return _bloodCard;
        _bloodCard = ScriptableObject.CreateInstance<KanjiCardData>();
        _bloodCard.kanji = "血";
        _bloodCard.cardName = "血";
        _bloodCard.description = $"敵に{bloodDamage}ダメージ、自分も{bloodDamage}ダメージ";
        _bloodCard.cost = 1;
        _bloodCard.effectValue = bloodDamage;
        _bloodCard.effectType = CardEffectType.Attack;
        _bloodCard.element = CardElement.None;
        _bloodCard.componentCount = 1;
        _bloodCard.cardId = -999; // 特殊ID
        return _bloodCard;
    }

    /// <summary>
    /// カードが「血」カードかどうかを判定
    /// </summary>
    public bool IsBloodCard(KanjiCardData card)
    {
        return card != null && card.kanji == "血" && card.cardId == -999;
    }

    /// <summary>
    /// 血カードの自傷ダメージを適用
    /// </summary>
    public void ApplyBloodSelfDamage()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.playerHP = Mathf.Max(0, gm.playerHP - bloodDamage);
        _bm?.AddBattleLog($"<color=#990000><b>血の反動！ 自分に{bloodDamage}ダメージ！</b></color>");
        Debug.Log($"[WolfBossManager] 血カード自傷ダメージ: {bloodDamage}");

        // プレイヤーHP0チェック
        if (gm.playerHP <= 0)
        {
            gm.ChangeState(GameState.GameOver);
        }
    }

    /// <summary>
    /// 第二形態への移行チェック（BattleManager.CheckBattleEndから呼ぶ）
    /// </summary>
    public bool CheckPhase2Transition()
    {
        if (_hasEnteredPhase2) return false;
        if (_bm == null || _bm.enemyCurrentHP > 0) return false;

        // 第二形態へ移行！
        _hasEnteredPhase2 = true;

        _bm.enemyCurrentHP = phase2HP;
        _bm.currentEnemyData.maxHP = phase2HP;

        _bm.AddBattleLog("<color=#FF0000><size=120%><b>「君となら本気で戦えそうだ」</b></size></color>");
        Debug.Log($"[WolfBossManager] 第二形態移行！ HP={phase2HP}");

        // 演出
        if (VFXManager.Instance != null && _battleUI != null)
        {
            VFXManager.Instance.PlayComboEffect(_battleUI.gameObject,
                "PHASE 2\nHP 99!!", Color.red);
            if (_battleUI.enemyKanjiText != null)
                VFXManager.Instance.PlayCriticalHitVFX(_battleUI.enemyKanjiText.transform.position);
        }

        _bm.UpdateUI();
        if (_battleUI != null) _battleUI.UpdateStatusUI();

        return true; // 戦闘続行
    }

    /// <summary>
    /// 狼のターン専用行動（BattleManagerのExecuteEnemyTurnから呼ぶ）
    /// </summary>
    public void OnWolfTurnAction(System.Action onComplete)
    {
        StartCoroutine(CoWolfTurnAction(onComplete));
    }

    private IEnumerator CoWolfTurnAction(System.Action onComplete)
    {
        _turnCount++;
        _battleUI = _bm?.battleUI;

        // 血カード生成チェック（一定ターン経過後）
        if (_turnCount > 0 && _turnCount % bloodCardInterval == 0)
        {
            yield return StartCoroutine(CoBloodCardInjection());
        }

        // 手札破壊チェック（確定ターンまたは確率）
        bool forceDestroy = (_turnCount % handDestroyEveryNTurns == 0);
        bool randomDestroy = (Random.Range(0, 100) < handDestroyChancePercent);
        if (forceDestroy || randomDestroy)
        {
            yield return StartCoroutine(CoHandDestruction());
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// 手札のランダムな1枚を「血」カードに変更
    /// </summary>
    private IEnumerator CoBloodCardInjection()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.hand.Count == 0) yield break;

        int idx = Random.Range(0, gm.hand.Count);
        var target = gm.hand[idx];

        // 既に血カードや404ERRORカードなら別のを探す
        if (target.kanji == "血" || target.kanji == "※")
        {
            bool found = false;
            for (int i = 0; i < gm.hand.Count; i++)
            {
                if (gm.hand[i].kanji != "血" && gm.hand[i].kanji != "※")
                {
                    idx = i;
                    target = gm.hand[i];
                    found = true;
                    break;
                }
            }
            if (!found) yield break;
        }

        _bm?.AddBattleLog($"<color=#990000><b>狼の呪い！『{target.kanji}』が『血』に変わった！</b></color>");

        if (VFXManager.Instance != null && _battleUI != null)
            VFXManager.Instance.PlayComboEffect(_battleUI.gameObject, "BLOOD\nCARD!!", new Color(0.6f, 0f, 0f));

        yield return new WaitForSeconds(0.6f);

        gm.hand[idx] = GetOrCreateBloodCard();

        if (_battleUI != null) _battleUI.UpdateHandUI();
        Debug.Log($"[WolfBossManager] 血カード生成: 『{target.kanji}』→血");
    }

    private IEnumerator CoHandDestruction()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.hand.Count == 0) yield break;

        int idx = Random.Range(0, gm.hand.Count);
        var target = gm.hand[idx];

        _bm?.AddBattleLog($"<color=#FF4444><b>狼の咆哮！『{target.kanji}』が<color=#FF0000>404 ERROR</color>になった！</b></color>");

        if (VFXManager.Instance != null && _battleUI != null)
            VFXManager.Instance.PlayComboEffect(_battleUI.gameObject, "HAND\nBREAK!!", new Color(1f, 0.15f, 0.15f));

        yield return new WaitForSeconds(0.6f);

        gm.hand[idx] = GetOrCreateErrorCard();

        if (_battleUI != null) _battleUI.UpdateHandUI();
        Debug.Log($"[WolfBossManager] 手札破壊: 『{target.kanji}』→404ERROR");
    }
}
