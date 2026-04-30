using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BGM・SEを管理するオーディオマネージャー
/// BGM切り替え時のフェード、音量設定UI、PlayerPrefs保存に対応
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGMソース")]
    public AudioSource bgmSource;

    [Header("BGMクリップ")]
    [Tooltip("戦闘BGM（404FreezeCode.mp3）")]
    public AudioClip battleBGM;
    [Tooltip("フィールドBGM")]
    public AudioClip fieldBGM;

    [Header("音量")]
    [Range(0f, 1f)] public float bgmVolume = 0.7f;
    [Range(0f, 1f)] public float seVolume = 1.0f;

    [Header("フォント")]
    public TMP_FontAsset appFont;

    private Coroutine _fadeCoroutine;

    private const string PREF_BGM = "BGMVolume";
    private const string PREF_SE  = "SEVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        // PlayerPrefsから音量を復元
        bgmVolume = PlayerPrefs.GetFloat(PREF_BGM, bgmVolume);
        seVolume  = PlayerPrefs.GetFloat(PREF_SE, seVolume);
        bgmSource.volume = bgmVolume;
    }

    private void Start()
    {
        // フォント未設定ならシーン内から取得
        if (appFont == null)
        {
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in fonts)
            {
                if (f.name.Contains("AppFont") || f.name.Contains("JP"))
                { appFont = f; break; }
            }
        }
        CreateSettingsUI();
    }

    // ─────────────────────────────────────────────
    // BGM 制御
    // ─────────────────────────────────────────────

    /// <summary>
    /// 戦闘BGMを即座に再生（トランジション演出と同期させるため即時）
    /// </summary>
    public void PlayBattleBGMImmediate()
    {
        if (battleBGM == null) { Debug.LogWarning("[AudioManager] battleBGMが未設定"); return; }
        if (bgmSource.clip == battleBGM && bgmSource.isPlaying) return;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        bgmSource.Stop();
        bgmSource.clip = battleBGM;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
        Debug.Log("[AudioManager] バトルBGM即時再生: " + battleBGM.name);
    }

    /// <summary>
    /// 戦闘BGMを再生（後方互換：即時再生）
    /// </summary>
    public void PlayBattleBGM()
    {
        PlayBattleBGMImmediate();
    }

    /// <summary>
    /// フィールドBGMへ1秒かけてフェード切り替え
    /// </summary>
    public void PlayFieldBGM()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);

        if (fieldBGM != null)
        {
            if (bgmSource.clip == fieldBGM && bgmSource.isPlaying) return;
            _fadeCoroutine = StartCoroutine(FadeSwitchBGM(fieldBGM, bgmVolume * 0.8f));
        }
        else
        {
            _fadeCoroutine = StartCoroutine(FadeOutAndStop());
        }
    }

    public void StopBGM()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        bgmSource.Stop();
    }

    private IEnumerator FadeSwitchBGM(AudioClip newClip, float targetVol)
    {
        // フェードアウト
        if (bgmSource.isPlaying)
        {
            float startVol = bgmSource.volume;
            float t = 0f;
            while (t < 1.0f)
            {
                t += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, t / 1.0f);
                yield return null;
            }
        }
        bgmSource.Stop();
        bgmSource.clip = newClip;

        // フェードイン
        bgmSource.volume = 0f;
        bgmSource.Play();
        float t2 = 0f;
        while (t2 < 1.0f)
        {
            t2 += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, targetVol, t2 / 1.0f);
            yield return null;
        }
        bgmSource.volume = targetVol;
        Debug.Log("[AudioManager] BGMフェード切り替え完了: " + newClip.name);
    }

    private IEnumerator FadeOutAndStop()
    {
        float startVol = bgmSource.volume;
        float t = 0f;
        while (t < 1.0f)
        {
            t += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0f, t / 1.0f);
            yield return null;
        }
        bgmSource.Stop();
    }

    // ─────────────────────────────────────────────
    // 音量設定UI（画面端の♪アイコン → パネル展開）
    // ─────────────────────────────────────────────

    private void CreateSettingsUI()
    {
        var mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null) return;

        // ── 設定パネル（先に作ってアイコンのToggle先に使う）──
        var panel = new GameObject("AudioSettingsPanel");
        panel.transform.SetParent(mainCanvas.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot     = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-10f, 68f);
        panelRect.sizeDelta = new Vector2(210f, 110f);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        var panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
        panelOutline.effectDistance = new Vector2(1.5f, -1.5f);
        panel.SetActive(false);

        // BGM スライダー
        AddVolumeSlider(panel.transform, "BGM", bgmVolume, new Vector2(0f, 75f), val =>
        {
            bgmVolume = val;
            bgmSource.volume = val;
            PlayerPrefs.SetFloat(PREF_BGM, val);
            PlayerPrefs.Save();
        });

        // SE スライダー
        AddVolumeSlider(panel.transform, "SE ", seVolume, new Vector2(0f, 35f), val =>
        {
            seVolume = val;
            PlayerPrefs.SetFloat(PREF_SE, val);
            PlayerPrefs.Save();
        });

        // ── ♪ アイコンボタン（右下端）──
        var icon = new GameObject("AudioSettingsIcon");
        icon.transform.SetParent(mainCanvas.transform, false);
        var iconRect = icon.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(1f, 0f);
        iconRect.anchorMax = new Vector2(1f, 0f);
        iconRect.pivot     = new Vector2(1f, 0f);
        iconRect.anchoredPosition = new Vector2(-10f, 10f);
        iconRect.sizeDelta = new Vector2(50f, 50f);
        var iconImg = icon.AddComponent<Image>();
        iconImg.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
        var iconBtn = icon.AddComponent<Button>();
        var iconBtnColors = iconBtn.colors;
        iconBtnColors.normalColor    = iconImg.color;
        iconBtnColors.highlightedColor = new Color(0.35f, 0.35f, 0.35f, 0.9f);
        iconBtnColors.pressedColor   = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        iconBtn.colors = iconBtnColors;

        var iconLabel = new GameObject("Label");
        iconLabel.transform.SetParent(icon.transform, false);
        var iconTMP = iconLabel.AddComponent<TextMeshProUGUI>();
        iconTMP.text = "♪";
        iconTMP.fontSize = 22f;
        iconTMP.alignment = TextAlignmentOptions.Center;
        iconTMP.color = new Color(0.85f, 0.85f, 0.85f);
        iconTMP.raycastTarget = false;
        if (appFont != null) iconTMP.font = appFont;
        var iconLabelRect = iconLabel.GetComponent<RectTransform>();
        iconLabelRect.anchorMin = Vector2.zero;
        iconLabelRect.anchorMax = Vector2.one;
        iconLabelRect.offsetMin = Vector2.zero;
        iconLabelRect.offsetMax = Vector2.zero;

        iconBtn.onClick.AddListener(() => panel.SetActive(!panel.activeSelf));
    }

    private void AddVolumeSlider(Transform parent, string labelStr, float initialVal,
                                  Vector2 position, System.Action<float> onChange)
    {
        var row = new GameObject($"SliderRow_{labelStr}");
        row.transform.SetParent(parent, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0.5f);
        rowRect.anchorMax = new Vector2(1f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = position;
        rowRect.sizeDelta = new Vector2(-20f, 28f);

        // ラベル
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(row.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0.25f, 1f);
        labelRect.offsetMin = new Vector2(5f, 0f);
        labelRect.offsetMax = Vector2.zero;
        var labelTMP = labelGo.AddComponent<TextMeshProUGUI>();
        labelTMP.text = labelStr;
        labelTMP.fontSize = 14f;
        labelTMP.color = new Color(0.85f, 0.85f, 0.85f);
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.raycastTarget = false;
        if (appFont != null) labelTMP.font = appFont;

        // スライダー背景
        var sliderBG = new GameObject("SliderBG");
        sliderBG.transform.SetParent(row.transform, false);
        var sliderBGRect = sliderBG.AddComponent<RectTransform>();
        sliderBGRect.anchorMin = new Vector2(0.27f, 0.2f);
        sliderBGRect.anchorMax = new Vector2(1f, 0.8f);
        sliderBGRect.offsetMin = Vector2.zero;
        sliderBGRect.offsetMax = Vector2.zero;
        var sliderBGImg = sliderBG.AddComponent<Image>();
        sliderBGImg.color = new Color(0.25f, 0.25f, 0.25f);

        // スライダー Fill
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(sliderBG.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(initialVal, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.5f, 0.7f, 0.9f);

        // スライダー Handle（ドラッグ可能ボタン領域）
        var sliderComp = sliderBG.AddComponent<Slider>();
        sliderComp.minValue = 0f;
        sliderComp.maxValue = 1f;
        sliderComp.value = initialVal;

        // fillRect を Slider の fillRect として設定
        sliderComp.fillRect = fillRect;
        var sliderColors = sliderComp.colors;
        sliderColors.normalColor = Color.white;
        sliderComp.colors = sliderColors;

        sliderComp.onValueChanged.AddListener(v =>
        {
            fillRect.anchorMax = new Vector2(v, 1f);
            onChange?.Invoke(v);
        });
    }
}
