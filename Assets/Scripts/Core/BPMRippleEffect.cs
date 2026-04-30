using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BPM 152 に同期した波紋エフェクト（敵の足元から円形波動）
/// AudioSettings.dspTime で正確なリズム同期を実現
/// Sprite + Coroutine でスケール拡大 ＋ Alpha フェードアウト
/// </summary>
public class BPMRippleEffect : MonoBehaviour
{
    [Header("BPM設定（404FreezeCode.mp3 = 152）")]
    public float bpm = 152f;
    [Tooltip("何拍ごとに波紋を生成するか（1=毎拍、2=2拍ごと）")]
    public int spawnEveryNBeats = 2;

    [Header("波紋設定")]
    public float initialSize = 80f;
    public float maxScale = 3.5f;
    public float lifetime = 0.9f;
    public Color rippleColor = new Color(0.5f, 0.3f, 1f, 0.65f);
    public Color rippleColor2 = new Color(1f, 0.3f, 0.5f, 0.4f); // 2拍目の色

    [Header("参照（BattleUI.StartBattleで自動セット）")]
    public Canvas targetCanvas;
    public Transform enemyTransform;

    private double _nextBeatDspTime;
    private bool _isActive = false;
    private int _beatCount = 0;
    private double _beatInterval;

    private void Start()
    {
        _beatInterval = 60.0 / bpm;
        _nextBeatDspTime = AudioSettings.dspTime + _beatInterval;
    }

    private void Update()
    {
        if (!_isActive || enemyTransform == null) return;

        if (AudioSettings.dspTime >= _nextBeatDspTime)
        {
            _beatCount++;
            _nextBeatDspTime += _beatInterval;

            if (_beatCount % spawnEveryNBeats == 0)
            {
                SpawnRipple(_beatCount % (spawnEveryNBeats * 2) == 0 ? rippleColor : rippleColor2);
            }
        }
    }

    /// <summary>
    /// エフェクトのアクティブ状態を切り替え（戦闘開始/終了時に呼ぶ）
    /// </summary>
    public void SetActive(bool active)
    {
        _isActive = active;
        if (active)
        {
            _beatInterval = 60.0 / bpm;
            _nextBeatDspTime = AudioSettings.dspTime + _beatInterval;
            _beatCount = 0;
        }
    }

    private void SpawnRipple(Color color)
    {
        if (targetCanvas == null)
        {
            targetCanvas = FindObjectOfType<Canvas>();
            if (targetCanvas == null) return;
        }

        var go = new GameObject("BPMRipple");
        go.transform.SetParent(targetCanvas.transform, false);
        go.transform.SetAsLastSibling();

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(initialSize, initialSize);

        // 敵の位置をCanvas座標へ変換（UI要素 = RectTransform の場合は直接ローカル座標を取得）
        RectTransform enemyRect = enemyTransform as RectTransform;
        if (enemyRect == null) enemyRect = enemyTransform.GetComponent<RectTransform>();
        if (enemyRect != null)
        {
            // UI要素同士なので、Canvas内のローカル座標を直接計算
            Vector3 worldPos = enemyTransform.position;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetCanvas.transform as RectTransform, screenPoint,
                targetCanvas.worldCamera, out Vector2 localPoint);
            rect.anchoredPosition = localPoint + new Vector2(0f, -30f); // 足元
        }
        else
        {
            // ワールドオブジェクトの場合
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                targetCanvas.worldCamera ?? Camera.main, enemyTransform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetCanvas.transform as RectTransform, screenPoint,
                targetCanvas.worldCamera, out Vector2 localPoint);
            rect.anchoredPosition = localPoint + new Vector2(0f, -30f);
        }

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        go.transform.localScale = Vector3.one * 0.1f;
        StartCoroutine(RippleAnim(go, img, color));
    }

    private IEnumerator RippleAnim(GameObject go, Image img, Color startColor)
    {
        float t = 0f;
        while (t < lifetime)
        {
            if (go == null) yield break;
            t += Time.deltaTime;
            float p = t / lifetime;

            // スケール拡大
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, maxScale, p);

            // Alpha フェードアウト（最初速く、最後ゆっくり消える）
            float alpha = startColor.a * (1f - p * p);
            img.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
