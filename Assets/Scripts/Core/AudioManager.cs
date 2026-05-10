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
    [Tooltip("戦闘BGM1（404FreezeCode）")]
    public AudioClip battleBGM;
    [Tooltip("戦闘BGM2（memento_loop）")]
    public AudioClip battleBGM2;
    [Tooltip("狼ボス専用BGM（Loneryboy）")]
    public AudioClip wolfBossBGM;
    [Tooltip("鬼ボス専用BGM")]
    public AudioClip oniBossBGM;
    [Tooltip("絶ボス用BGM (CultusA)")]
    public AudioClip zetsuBossBGMA;
    [Tooltip("絶ボス用BGM (CultusB)")]
    public AudioClip zetsuBossBGMB;
    [Tooltip("フィールドBGM")]
    public AudioClip fieldBGM;

    [Header("SEクリップ")]
    [Tooltip("敵を倒した時")]
    public AudioClip seBlow3;
    [Tooltip("ダメージを受けた時")]
    public AudioClip seButton38;
    [Tooltip("UIホバー/クリック時")]
    public AudioClip seButton44;
    [Tooltip("メイン決定アクション")]
    public AudioClip seButton50;

    public enum BossType
    {
        Wolf,
        Oni,
        Zetsu
    }

    private Coroutine _zetsuCoroutine;
    private AudioSource _bgmSource2;

    [Header("BPM設定")]
    [Tooltip("battleBGM（404FreezeCode）のBPM")]
    public float battleBGM1_BPM = 152f;
    [Tooltip("battleBGM2（memento_loop）のBPM")]
    public float battleBGM2_BPM = 120f;
    [Tooltip("狼ボスBGM（Loneryboy）のBPM")]
    public float wolfBossBGM_BPM = 140f;

    public float CurrentBattleBPM { get; private set; } = 152f;

    private AudioClip _nextBattleBGMOverride = null;
    private float _nextBattleBPMOverride = 0f;

    [Header("音量")]
    [Range(0f, 1f)] public float bgmVolume = 0.7f;
    [Range(0f, 1f)] public float seVolume = 1.0f;

    [Header("フォント")]
    public TMP_FontAsset appFont;

    private Coroutine _fadeCoroutine;

    [Header("SEソース")]
    public AudioSource seSource;

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

        if (seSource == null)
        {
            seSource = gameObject.AddComponent<AudioSource>();
        }
        seSource.playOnAwake = false;

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

        // BGMクリップがインスペクタ未設定の場合、動的に読み込む
        LoadBGMClipsIfMissing();
        LoadSEClipsIfMissing();

        CreateSettingsUI();
    }

    /// <summary>
    /// SEクリップが未設定の場合、動的に読み込む
    /// </summary>
    private void LoadSEClipsIfMissing()
    {
        var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
        foreach (var clip in allClips)
        {
            if (clip == null) continue;
            string clipName = clip.name.ToLower();

            if (seBlow3 == null && clipName.Contains("blow3"))
            {
                seBlow3 = clip;
                Debug.Log($"[AudioManager] seBlow3を自動設定: {clip.name}");
            }
            else if (seButton38 == null && clipName.Contains("button38"))
            {
                seButton38 = clip;
                Debug.Log($"[AudioManager] seButton38を自動設定: {clip.name}");
            }
            else if (seButton44 == null && clipName.Contains("button44"))
            {
                seButton44 = clip;
                Debug.Log($"[AudioManager] seButton44を自動設定: {clip.name}");
            }
            else if (seButton50 == null && clipName.Contains("button50"))
            {
                seButton50 = clip;
                Debug.Log($"[AudioManager] seButton50を自動設定: {clip.name}");
            }
        }
    }

    /// <summary>
    /// BGMクリップがnullの場合、Assets/Audiosフォルダから動的に探す
    /// </summary>
    private void LoadBGMClipsIfMissing()
    {
        if (battleBGM == null || battleBGM2 == null || wolfBossBGM == null || oniBossBGM == null)
        {
            // UnityEditorのみ使用可能なAssetDatabase以外のアプローチ
            // Resources以外のフォルダの場合、シーン内のAudioSourceやScriptableObjectから参照を取得
            var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            foreach (var clip in allClips)
            {
                if (clip == null) continue;
                string clipName = clip.name.ToLower();

                if (battleBGM == null && clipName.Contains("404freezecode"))
                {
                    battleBGM = clip;
                    Debug.Log($"[AudioManager] battleBGMを自動設定: {clip.name}");
                }
                else if (battleBGM2 == null && clipName.Contains("memento_loop"))
                {
                    battleBGM2 = clip;
                    Debug.Log($"[AudioManager] battleBGM2を自動設定: {clip.name}");
                }
                else if (wolfBossBGM == null && clipName.Contains("loneryboy"))
                {
                    wolfBossBGM = clip;
                    Debug.Log($"[AudioManager] wolfBossBGMを自動設定: {clip.name}");
                }
                else if (oniBossBGM == null && clipName.Contains("karma1loop"))
                {
                    oniBossBGM = clip;
                    Debug.Log($"[AudioManager] oniBossBGMを自動設定: {clip.name}");
                }
            }

            // Editorフォールバック：AssetDatabaseから直接ロード
#if UNITY_EDITOR
            if (battleBGM == null)
                battleBGM = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/404FreezeCode.mp3");
            if (battleBGM2 == null)
                battleBGM2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/memento_loop.ogg");
            if (wolfBossBGM == null)
                wolfBossBGM = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/Loneryboy.mp3");
            if (oniBossBGM == null)
            {
                oniBossBGM = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/KARMA1Loop.mp3");
                if (oniBossBGM != null) Debug.Log("[AudioManager] oniBossBGMをAssetDatabaseから読み込み: KARMA1Loop.mp3");
            }
#endif
            if (battleBGM == null) Debug.LogWarning("[AudioManager] battleBGM（404FreezeCode）が見つかりません");
            if (battleBGM2 == null) Debug.LogWarning("[AudioManager] battleBGM2（memento_loop）が見つかりません");
            if (oniBossBGM == null) Debug.LogWarning("[AudioManager] oniBossBGM（KARMA1Loop）が見つかりません");
        }
    }

    // ─────────────────────────────────────────────
    // BGM 制御
    // ─────────────────────────────────────────────

    public void PlaySE(AudioClip clip)
    {
        if (clip == null) return;
        seSource.PlayOneShot(clip, seVolume);
    }

    /// <summary>
    /// 戦闘BGMを即座に再生。オーバーライドがあればそれを使用、なければBGM1/2からランダム
    /// </summary>
    public void PlayBattleBGMImmediate()
    {
        AudioClip selected;
        float bpm;

        if (_nextBattleBGMOverride != null)
        {
            selected = _nextBattleBGMOverride;
            bpm = _nextBattleBPMOverride > 0f ? _nextBattleBPMOverride : battleBGM1_BPM;
            _nextBattleBGMOverride = null;
            _nextBattleBPMOverride = 0f;
        }
        else if (battleBGM2 != null && Random.value < 0.5f)
        {
            selected = battleBGM2;
            bpm = battleBGM2_BPM;
        }
        else
        {
            selected = battleBGM;
            bpm = battleBGM1_BPM;
        }

        if (selected == null) { Debug.LogWarning("[AudioManager] battleBGMが未設定"); return; }
        if (bgmSource.clip == selected && bgmSource.isPlaying) return;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        if (_zetsuCoroutine != null) StopCoroutine(_zetsuCoroutine);
        bgmSource.Stop();
        if (_bgmSource2 != null) _bgmSource2.Stop();
        bgmSource.loop = true;
        bgmSource.clip = selected;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
        CurrentBattleBPM = bpm;
        Debug.Log($"[AudioManager] バトルBGM即時再生: {selected.name} BPM:{bpm}");
    }

    /// <summary>
    /// 戦闘BGMを再生（後方互換：即時再生）
    /// </summary>
    public void PlayBattleBGM()
    {
        PlayBattleBGMImmediate();
    }

    /// <summary>
    /// 狼ボス第1形態用：404FreezeCodeを次回戦闘BGMに強制設定
    /// </summary>
    public void SetWolfBossBGM()
    {
        // 第1形態は404FreezeCodeを固定再生（ランダム選曲しない）
        if (battleBGM != null)
        {
            _nextBattleBGMOverride = battleBGM;
            _nextBattleBPMOverride = battleBGM1_BPM;
        }
    }

    /// <summary>
    /// 鬼ボス用：KARMA1Loopを次回戦闘BGMに強制設定
    /// </summary>
    public void SetOniBossBGM()
    {
        if (oniBossBGM != null)
        {
            _nextBattleBGMOverride = oniBossBGM;
            _nextBattleBPMOverride = 120f;
        }
    }

    /// <summary>
    /// 狼ボス第2形態移行時：LonerboyBGMにフェード切替
    /// </summary>
    public void SwitchToWolfBossPhase2BGM()
    {
        if (wolfBossBGM == null)
        {
            Debug.LogWarning("[AudioManager] wolfBossBGM(Loneryboy)が未設定");
            return;
        }
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeSwitchBGM(wolfBossBGM, bgmVolume));
        CurrentBattleBPM = wolfBossBGM_BPM;
        Debug.Log($"[AudioManager] 狼ボス第2形態BGM切替: Loneryboy BPM:{wolfBossBGM_BPM}");
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
        if (_zetsuCoroutine != null) StopCoroutine(_zetsuCoroutine);
        bgmSource.Stop();
        if (_bgmSource2 != null) _bgmSource2.Stop();
    }

    private IEnumerator FadeSwitchBGM(AudioClip newClip, float targetVol)
    {
        if (_zetsuCoroutine != null) StopCoroutine(_zetsuCoroutine);

        // フェードアウト
        if (bgmSource.isPlaying || (_bgmSource2 != null && _bgmSource2.isPlaying))
        {
            float startVol1 = bgmSource.volume;
            float startVol2 = _bgmSource2 != null ? _bgmSource2.volume : 0;
            float t = 0f;
            while (t < 1.0f)
            {
                t += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVol1, 0f, t / 1.0f);
                if (_bgmSource2 != null) _bgmSource2.volume = Mathf.Lerp(startVol2, 0f, t / 1.0f);
                yield return null;
            }
        }
        bgmSource.Stop();
        if (_bgmSource2 != null) _bgmSource2.Stop();
        bgmSource.loop = true;
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
        if (_zetsuCoroutine != null) StopCoroutine(_zetsuCoroutine);

        float startVol1 = bgmSource.volume;
        float startVol2 = _bgmSource2 != null ? _bgmSource2.volume : 0;
        float t = 0f;
        while (t < 1.0f)
        {
            t += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVol1, 0f, t / 1.0f);
            if (_bgmSource2 != null) _bgmSource2.volume = Mathf.Lerp(startVol2, 0f, t / 1.0f);
            yield return null;
        }
        bgmSource.Stop();
        if (_bgmSource2 != null) _bgmSource2.Stop();
    }

    public void PlayBossBGM(BossType bossType)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        if (_zetsuCoroutine != null) StopCoroutine(_zetsuCoroutine);
        if (_bgmSource2 != null) _bgmSource2.Stop();
        bgmSource.loop = true;

        switch (bossType)
        {
            case BossType.Wolf:
                if (wolfBossBGM != null)
                {
                    _fadeCoroutine = StartCoroutine(FadeSwitchBGM(wolfBossBGM, bgmVolume));
                    CurrentBattleBPM = wolfBossBGM_BPM;
                }
                break;
            case BossType.Oni:
                if (oniBossBGM != null)
                {
                    _fadeCoroutine = StartCoroutine(FadeSwitchBGM(oniBossBGM, bgmVolume));
                    CurrentBattleBPM = 120f; // Optional default
                }
                break;
            case BossType.Zetsu:
                if (zetsuBossBGMA != null && zetsuBossBGMB != null)
                {
                    _zetsuCoroutine = StartCoroutine(CoPlayZetsuBGM());
                }
                break;
        }
    }

    private IEnumerator CoPlayZetsuBGM()
    {
        if (_bgmSource2 == null)
        {
            _bgmSource2 = gameObject.AddComponent<AudioSource>();
            _bgmSource2.playOnAwake = false;
        }

        bgmSource.Stop();
        _bgmSource2.Stop();

        bgmSource.volume = bgmVolume;
        _bgmSource2.volume = bgmVolume;

        bgmSource.loop = false;
        _bgmSource2.loop = false;

        AudioClip nextClip = zetsuBossBGMA;
        AudioSource currentSource = bgmSource;
        AudioSource nextSource = _bgmSource2;

        double nextStartTime = AudioSettings.dspTime + 0.1;
        currentSource.clip = nextClip;
        currentSource.PlayScheduled(nextStartTime);

        bool isA = true;

        while (true)
        {
            double duration = (double)currentSource.clip.samples / currentSource.clip.frequency;
            nextStartTime += duration;

            isA = !isA;
            nextClip = isA ? zetsuBossBGMA : zetsuBossBGMB;

            nextSource.clip = nextClip;
            nextSource.PlayScheduled(nextStartTime);

            yield return new WaitForSecondsRealtime((float)duration - 0.5f);

            var temp = currentSource;
            currentSource = nextSource;
            nextSource = temp;
        }
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
