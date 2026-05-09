using System.Collections;
using UnityEngine;

/// <summary>
/// 狼ボス専用行動マネージャー
/// BattleManagerと同じGameObjectにアタッチして使用
/// - 手札破壊（404 ERRORカード化）
/// - HP激増イベント（HP50%以下で一度だけ2倍に激増）
/// </summary>
public class WolfBossManager : MonoBehaviour
{
    [Header("手札破壊設定")]
    [Tooltip("ターンごとの手札破壊発動確率 (%)")]
    public int handDestroyChancePercent = 35;
    [Tooltip("この間隔ターンでは確定発動")]
    public int handDestroyEveryNTurns = 3;

    [Header("HP激増設定")]
    [Tooltip("この割合以下のHPになったら発動 (0~1)")]
    [Range(0f, 1f)] public float enrageHpThreshold = 0.5f;
    [Tooltip("激増時のHP倍率")]
    public int enrageMultiplier = 2;

    private int _turnCount = 0;
    private bool _hasEnraged = false;
    private KanjiCardData _errorCard;
    private BattleManager _bm;
    private BattleUI _battleUI;

    public bool HasEnraged => _hasEnraged;

    private void Awake()
    {
        _bm = GetComponent<BattleManager>();
    }

    public void InitForWolfBoss()
    {
        _turnCount = 0;
        _hasEnraged = false;
        _errorCard = null;
        _battleUI = _bm?.battleUI;
        Debug.Log("[WolfBossManager] 狼ボス戦開始！");
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

        // HP激増チェック（未発動かつ閾値以下）
        if (!_hasEnraged && _bm != null && _bm.currentEnemyData != null)
        {
            float ratio = (float)_bm.enemyCurrentHP / _bm.currentEnemyData.maxHP;
            if (ratio <= enrageHpThreshold)
            {
                yield return StartCoroutine(CoEnrageEvent());
                _hasEnraged = true;
            }
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

    private IEnumerator CoEnrageEvent()
    {
        _bm?.AddBattleLog("<color=#FF0000><size=120%><b>【ENRAGE】狼が激昂した…！</b></size></color>");

        // 予兆演出
        if (VFXManager.Instance != null && _battleUI != null)
        {
            VFXManager.Instance.PlayComboEffect(_battleUI.gameObject, "…？！", Color.red);
            if (_battleUI.enemyKanjiText != null)
                VFXManager.Instance.PlayCriticalHitVFX(_battleUI.enemyKanjiText.transform.position);
        }
        yield return new WaitForSeconds(0.8f);

        // HP激増
        int oldMax = _bm.currentEnemyData.maxHP;
        int newMax = oldMax * enrageMultiplier;
        _bm.currentEnemyData.maxHP = newMax;
        _bm.enemyCurrentHP = newMax;

        string logMsg = $"<color=#FF0000><b>【絶望】狼の最大HPが {oldMax} → {newMax} に激増した！全回復！！</b></color>";
        _bm?.AddBattleLog(logMsg);
        Debug.Log($"[WolfBossManager] HP激増: {oldMax} → {newMax}");

        if (VFXManager.Instance != null && _battleUI != null)
        {
            VFXManager.Instance.PlayComboEffect(_battleUI.gameObject,
                $"MAX HP\n{oldMax}→{newMax}!!", Color.red);
            if (_battleUI.enemyKanjiText != null)
                VFXManager.Instance.PlayCriticalHitVFX(_battleUI.enemyKanjiText.transform.position);
        }

        _bm?.UpdateUI();
        if (_battleUI != null) _battleUI.UpdateStatusUI();

        yield return new WaitForSeconds(1.0f);
    }
}
