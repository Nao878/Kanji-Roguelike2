using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// フィールドから戦闘への開戦トランジション演出（ちょうど3.0秒）
/// ① 白い横線（0~0.3s）→ ② 黒パネルスライドイン（0.3~1.3s）
/// → ③「開」「戦」ポップアップ（1.3~1.8s）→ 保持（1.8~2.5s）→ ④ フェードアウト（2.5~3.0s）
/// </summary>
public class BattleTransitionManager : MonoBehaviour
{
    public static BattleTransitionManager Instance { get; private set; }

    private const float refWidth = 960f; // CanvasScalerの参照解像度に合わせる

    [Header("フォント（日本語対応必須）")]
    public TMP_FontAsset appFont;

    private Canvas _canvas;
    private Image _whiteLine;
    private Image _topPanel;
    private Image _bottomPanel;
    private TextMeshProUGUI _kaiText;
    private TextMeshProUGUI _senText;
    private bool _isPlaying = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // フォントが未設定ならシーン内から探す
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

        BuildUI();
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("BattleTransitionCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f); // MainCanvasと合わせる
        scaler.matchWidthOrHeight = 0.5f;

        var raycaster = canvasGo.AddComponent<GraphicRaycaster>();
        raycaster.blockingMask = 0;

        var canvasRect = canvasGo.GetComponent<RectTransform>();

        // ① 白い横線（左端から幅0でスタート）
        var lineGo = CreateImageGO(canvasRect, "WhiteLine", Color.white);
        var lineRect = lineGo.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 0.5f);
        lineRect.anchorMax = new Vector2(0f, 0.5f);
        lineRect.pivot = new Vector2(0f, 0.5f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta = new Vector2(0f, 8f);
        _whiteLine = lineGo.GetComponent<Image>();

        // ② 上半分の黒パネル（画面外から登場。少し重複させて隙間を防ぐ）
        var topGo = CreateImageGO(canvasRect, "TopPanel", Color.black);
        var topRect = topGo.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 0.48f); // 少し中央に重複
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.offsetMin = Vector2.zero;
        topRect.offsetMax = Vector2.zero;
        topRect.anchoredPosition = new Vector2(-refWidth, 0f); // 画面外（左）
        _topPanel = topGo.GetComponent<Image>();

        // ② 下半分の黒パネル（画面外から登場。少し重複させて隙間を防ぐ）
        var botGo = CreateImageGO(canvasRect, "BottomPanel", Color.black);
        var botRect = botGo.GetComponent<RectTransform>();
        botRect.anchorMin = new Vector2(0f, 0f);
        botRect.anchorMax = new Vector2(1f, 0.52f); // 少し中央に重複
        botRect.offsetMin = Vector2.zero;
        botRect.offsetMax = Vector2.zero;
        botRect.anchoredPosition = new Vector2(refWidth, 0f); // 画面外（右）
        _bottomPanel = botGo.GetComponent<Image>();

        // ③「開」テキスト（画面右上）
        _kaiText = CreateKaisenText(canvasRect, "開", new Vector2(1f, 1f), new Vector2(-80f, -100f));
        // ③「戦」テキスト（画面左下）
        _senText = CreateKaisenText(canvasRect, "戦", new Vector2(0f, 0f), new Vector2(80f, 100f));

        // 全て非表示で初期化
        _whiteLine.gameObject.SetActive(false);
        _topPanel.gameObject.SetActive(false);
        _bottomPanel.gameObject.SetActive(false);
        _kaiText.gameObject.SetActive(false);
        _senText.gameObject.SetActive(false);
    }

    private GameObject CreateImageGO(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    private TextMeshProUGUI CreateKaisenText(RectTransform parent, string str, Vector2 anchor, Vector2 offset)
    {
        var go = new GameObject($"KaisenText_{str}");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = new Vector2(250f, 250f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = str;
        tmp.fontSize = 140f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;
        if (appFont != null) tmp.font = appFont;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(4f, -4f);

        return tmp;
    }

    /// <summary>
    /// 3秒間の開戦トランジションを再生し、完了後にコールバックを呼ぶ
    /// </summary>
    public void PlayBattleTransition(Action onComplete)
    {
        if (_isPlaying)
        {
            onComplete?.Invoke();
            return;
        }
        StartCoroutine(TransitionSequence(onComplete));
    }

    private IEnumerator TransitionSequence(Action onComplete)
    {
        _isPlaying = true;

        // BGMをトランジション頭から再生（3秒後に盛り上がる部分に同期）
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBattleBGMImmediate();

        // refWidth はクラス定数として定義済み

        // ===== フェーズ① 白い横線が伸びる（0 ~ 0.3秒）=====
        _whiteLine.gameObject.SetActive(true);
        var lineRect = _whiteLine.GetComponent<RectTransform>();
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.3f);
            float ease = 1f - (1f - p) * (1f - p); // EaseOutQuad
            lineRect.sizeDelta = new Vector2(refWidth * ease, 8f);
            yield return null;
        }
        lineRect.sizeDelta = new Vector2(refWidth, 8f);

        // ===== フェーズ② 黒パネルがスライドイン（0.3 ~ 1.3秒）=====
        _topPanel.color = Color.black;
        _bottomPanel.color = Color.black;
        _topPanel.gameObject.SetActive(true);
        _bottomPanel.gameObject.SetActive(true);
        t = 0f;
        while (t < 1.0f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 1.0f);
            float ease = 1f - (1f - p) * (1f - p) * (1f - p); // EaseOutCubic（最初速く、後でゆっくり）
            var topRt = _topPanel.GetComponent<RectTransform>();
            var botRt = _bottomPanel.GetComponent<RectTransform>();
            topRt.anchoredPosition = new Vector2(Mathf.Lerp(-refWidth, 0f, ease), 0f);
            botRt.anchoredPosition = new Vector2(Mathf.Lerp(refWidth, 0f, ease), 0f);
            yield return null;
        }
        _topPanel.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        _bottomPanel.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        _whiteLine.gameObject.SetActive(false);

        // ===== フェーズ③「開」「戦」テキストポップアップ（1.3 ~ 1.8秒）=====
        _kaiText.gameObject.SetActive(true);
        _senText.gameObject.SetActive(true);
        _kaiText.alpha = 0f;
        _senText.alpha = 0f;
        _kaiText.transform.localScale = Vector3.one * 0.3f;
        _senText.transform.localScale = Vector3.one * 0.3f;
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.5f);
            float ease = 1f - (1f - p) * (1f - p);
            _kaiText.alpha = ease;
            _senText.alpha = ease;
            float sc = Mathf.Lerp(0.3f, 1f, ease);
            _kaiText.transform.localScale = Vector3.one * sc;
            _senText.transform.localScale = Vector3.one * sc;
            yield return null;
        }
        _kaiText.alpha = 1f;
        _senText.alpha = 1f;
        _kaiText.transform.localScale = Vector3.one;
        _senText.transform.localScale = Vector3.one;

        // ===== 保持（1.8 ~ 2.5秒）=====
        yield return new WaitForSeconds(0.7f);

        // ===== フェーズ④ フェードアウト（2.5 ~ 3.0秒）=====
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(t / 0.5f);
            _topPanel.color = new Color(0f, 0f, 0f, alpha);
            _bottomPanel.color = new Color(0f, 0f, 0f, alpha);
            _kaiText.alpha = alpha;
            _senText.alpha = alpha;
            yield return null;
        }

        // クリーンアップ＆リセット
        _whiteLine.gameObject.SetActive(false);
        _topPanel.gameObject.SetActive(false);
        _bottomPanel.gameObject.SetActive(false);
        _kaiText.gameObject.SetActive(false);
        _senText.gameObject.SetActive(false);
        _topPanel.color = Color.black;
        _bottomPanel.color = Color.black;
        _topPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(-refWidth, 0f);
        _bottomPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(refWidth, 0f);

        _isPlaying = false;
        onComplete?.Invoke();
    }
}
