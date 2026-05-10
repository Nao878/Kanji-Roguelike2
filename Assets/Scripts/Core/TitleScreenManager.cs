using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ゲーム起動時のタイトル画面 → チュートリアル戦闘を管理する
/// PlayerPrefs("TutorialCompleted")==1 なら初回のみスキップする
/// </summary>
public class TitleScreenManager : MonoBehaviour
{
    public static TitleScreenManager Instance { get; private set; }

    private const string TUTORIAL_KEY = "TutorialCompleted";

    private GameObject titleOverlay;
    private CanvasGroup titleCG;
    private bool isTutorialMode = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        bool tutorialDone = PlayerPrefs.GetInt(TUTORIAL_KEY, 0) == 1;
        if (!tutorialDone)
        {
            isTutorialMode = true;
            StartCoroutine(ShowTitleScreen());
        }
        // チュートリアル済みの場合は通常フロー（FieldManager.ShowField()がGameManager.InitializeGame()経由で呼ばれる）
    }

    private IEnumerator ShowTitleScreen()
    {
        // GameManagerがField状態で初期化されるのを待つ
        yield return new WaitForSeconds(0.1f);

        // フィールドを非表示にしてタイトル画面を前面表示
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.fieldPanel != null)
                GameManager.Instance.fieldPanel.SetActive(false);
        }

        BuildTitleUI();
    }

    private void BuildTitleUI()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        titleOverlay = new GameObject("TitleOverlay");
        titleOverlay.transform.SetParent(canvas.transform, false);
        titleOverlay.transform.SetAsLastSibling();

        var rect = titleOverlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var bg = titleOverlay.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.02f, 0.05f, 1f);

        titleCG = titleOverlay.AddComponent<CanvasGroup>();
        titleCG.alpha = 0f;

        // フォント取得（AppFont SDFを優先）
        TMP_FontAsset font = null;
        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        // 優先順位: AppFont > JP含む > それ以外
        foreach (var f in fonts)
            if (f.name.Contains("AppFont")) { font = f; break; }
        if (font == null)
            foreach (var f in fonts)
                if (f.name.Contains("JP")) { font = f; break; }

        // タイトルテキスト「漢字ローグライク」
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(titleOverlay.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.55f);
        titleRect.anchorMax = new Vector2(1f, 0.85f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "漢字ローグライク";
        titleTmp.fontSize = 58f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(1f, 0.92f, 0.15f);
        if (font != null) titleTmp.font = font;
        var titleOutline = titleGo.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.6f, 0.3f, 0f, 0.9f);
        titleOutline.effectDistance = new Vector2(3f, -3f);

        // サブタイトル
        var subGo = new GameObject("SubTitle");
        subGo.transform.SetParent(titleOverlay.transform, false);
        var subRect = subGo.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.1f, 0.46f);
        subRect.anchorMax = new Vector2(0.9f, 0.57f);
        subRect.offsetMin = Vector2.zero;
        subRect.offsetMax = Vector2.zero;
        var subTmp = subGo.AddComponent<TextMeshProUGUI>();
        subTmp.text = "〜 チュートリアル：まずは戦闘を体験しよう 〜";
        subTmp.fontSize = 20f;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color = new Color(0.7f, 0.85f, 1f, 0.9f);
        if (font != null) subTmp.font = font;

        // CLICK TO START テキスト（点滅）
        var clickGo = new GameObject("ClickToStart");
        clickGo.transform.SetParent(titleOverlay.transform, false);
        var clickRect = clickGo.AddComponent<RectTransform>();
        clickRect.anchorMin = new Vector2(0.1f, 0.25f);
        clickRect.anchorMax = new Vector2(0.9f, 0.38f);
        clickRect.offsetMin = Vector2.zero;
        clickRect.offsetMax = Vector2.zero;
        var clickTmp = clickGo.AddComponent<TextMeshProUGUI>();
        clickTmp.text = "CLICK TO START";
        clickTmp.fontSize = 30f;
        clickTmp.fontStyle = FontStyles.Bold;
        clickTmp.alignment = TextAlignmentOptions.Center;
        clickTmp.color = Color.white;
        if (font != null) clickTmp.font = font;

        // 点滅コルーチン
        StartCoroutine(BlinkText(clickTmp));

        // フェードイン
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            if (titleCG != null) titleCG.alpha = Mathf.Clamp01(t / 0.8f);
            yield return null;
        }
        if (titleCG != null) titleCG.alpha = 1f;
    }

    private IEnumerator BlinkText(TextMeshProUGUI tmp)
    {
        while (true)
        {
            for (float t = 0f; t < 0.9f; t += Time.deltaTime) { yield return null; }
            if (tmp == null) yield break;
            float alpha = tmp.color.a;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                if (tmp == null) yield break;
                tmp.alpha = Mathf.Lerp(alpha, 0.15f, t / 0.4f);
                yield return null;
            }
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                if (tmp == null) yield break;
                tmp.alpha = Mathf.Lerp(0.15f, 1f, t / 0.4f);
                yield return null;
            }
        }
    }

    private void Update()
    {
        if (!isTutorialMode || titleOverlay == null) return;

        bool triggered = false;
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if ((mouse != null && mouse.leftButton.wasPressedThisFrame) ||
            (keyboard != null && keyboard.anyKey.wasPressedThisFrame))
            triggered = true;
#else
        if (Input.anyKeyDown) triggered = true;
#endif
        if (triggered) StartCoroutine(StartTutorialBattle());
    }

    private IEnumerator StartTutorialBattle()
    {
        isTutorialMode = false; // 二重起動防止

        // フェードアウト
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            if (titleCG != null) titleCG.alpha = 1f - Mathf.Clamp01(t / 0.5f);
            yield return null;
        }

        if (titleOverlay != null) Destroy(titleOverlay);

        // チュートリアル専用弱い敵を生成
        var tutorialEnemy = ScriptableObject.CreateInstance<EnemyData>();
        tutorialEnemy.enemyName = "チュートリアル敵";
        tutorialEnemy.displayKanji = "弱";
        tutorialEnemy.maxHP = 12;
        tutorialEnemy.attackPower = 2;
        tutorialEnemy.componentCount = 1;
        tutorialEnemy.enemyType = EnemyType.Normal;

        var gm = GameManager.Instance;
        if (gm == null) yield break;

        // バトルパネルを有効化してフィールドを非表示
        if (gm.fieldPanel != null) gm.fieldPanel.SetActive(false);

        // HelpPanelをチュートリアル中に表示
        if (gm.helpPanel != null)
        {
            gm.helpPanel.SetActive(true);
            Time.timeScale = 0f; // ヘルプ中は一時停止
        }

        // HelpPanelを閉じたら戦闘開始
        StartCoroutine(WaitForHelpAndStartBattle(tutorialEnemy));
    }

    private IEnumerator WaitForHelpAndStartBattle(EnemyData tutorialEnemy)
    {
        var gm = GameManager.Instance;
        if (gm == null) yield break;

        // HelpPanelが表示されていれば、クリックで閉じるまで待つ
        if (gm.helpPanel != null && gm.helpPanel.activeSelf)
        {
            // HelpPanelに「戦闘開始」ボタンを追加
            AddStartButtonToHelpPanel(tutorialEnemy);
        }
        else
        {
            // HelpPanelなしなら即座に戦闘開始
            Time.timeScale = 1f;
            BeginTutorialBattle(tutorialEnemy);
        }

        yield return null;
    }

    private void AddStartButtonToHelpPanel(EnemyData tutorialEnemy)
    {
        var gm = GameManager.Instance;
        if (gm?.helpPanel == null) return;

        // HelpPanelに「理解した！戦闘開始」ボタンを追加
        var btnGo = new GameObject("TutorialStartButton");
        btnGo.transform.SetParent(gm.helpPanel.transform, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.15f, 0.02f);
        btnRect.anchorMax = new Vector2(0.85f, 0.12f);
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.55f, 0.15f, 0.95f);

        var btn = btnGo.AddComponent<Button>();
        var btnColors = btn.colors;
        btnColors.highlightedColor = new Color(0.2f, 0.7f, 0.2f, 1f);
        btn.colors = btnColors;

        TMP_FontAsset font = null;
        var fontsAll = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var f in fontsAll)
            if (f.name.Contains("AppFont")) { font = f; break; }
        if (font == null)
            foreach (var f in fontsAll)
                if (f.name.Contains("JP")) { font = f; break; }

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(btnGo.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "理解した！戦闘開始！";
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        var tmpRect = textGo.GetComponent<RectTransform>();
        tmpRect.anchorMin = Vector2.zero;
        tmpRect.anchorMax = Vector2.one;
        tmpRect.offsetMin = Vector2.zero;
        tmpRect.offsetMax = Vector2.zero;

        var capturedEnemy = tutorialEnemy;
        btn.onClick.AddListener(() =>
        {
            if (gm.helpPanel != null) gm.helpPanel.SetActive(false);
            Time.timeScale = 1f;
            Destroy(btnGo);
            BeginTutorialBattle(capturedEnemy);
        });
    }

    private void BeginTutorialBattle(EnemyData tutorialEnemy)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.battleManager == null) return;

        // フィールドManagerのチュートリアルフラグを設定
        if (gm.fieldManager != null)
            gm.fieldManager.SetTutorialMode(true);

        gm.battleManager.StartBattle(tutorialEnemy);
    }

    /// <summary>
    /// チュートリアル戦闘勝利後に呼ばれる
    /// </summary>
    public static void OnTutorialBattleWon()
    {
        PlayerPrefs.SetInt(TUTORIAL_KEY, 1);
        PlayerPrefs.Save();
        Debug.Log("[TitleScreenManager] チュートリアル完了！フィールドに移行します。");
    }
}
