using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// ドラッグ＆ドロップ操作を基本としたカードコントローラー
/// タップ（クリック）で合体ボタン表示、ドラッグで敵攻撃・カード合体に対応
/// </summary>
public class CardController : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("カードデータ")]
    public KanjiCardData cardData;

    [Header("UI要素")]
    public Image cardBackground;
    public TextMeshProUGUI kanjiText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI descriptionText;
    public CanvasGroup canvasGroup;

    [Header("合体プレビュー")]
    public GameObject fusionPreviewObj;
    public TextMeshProUGUI fusionPreviewText;

    [Header("フォント")]
    public TMP_FontAsset appFont;

    // ドラッグ情報
    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector2 originalPosition;
    private Canvas rootCanvas;
    private RectTransform rectTransform;

    // 合成プレビュー状態
    private bool isHighlighted = false;
    private Color originalColor;

    // ホバースケール
    private Coroutine _hoverScaleCoroutine;

    // 選択ハイライト状態
    private bool isSelected = false;
    private Outline selectionOutline;
    private static readonly float SELECTION_LIFT = 20f; // 選択時の浮き上がり量

    // 合体ボタン管理（静的：全カードで共有）
    private static List<GameObject> activeFusionButtons = new List<GameObject>();
    private static CardController selectedCard = null;

    // 攻撃ボタン管理（静的）
    private static GameObject activeAttackButton = null;

    // コールバック
    public System.Action onCardUsed;
    public System.Action onHandChanged;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// カードデータをセットアップ
    /// </summary>
    public void Setup(KanjiCardData data)
    {
        cardData = data;
        if (data == null) return;

        if (kanjiText != null) kanjiText.text = data.kanji;
        if (costText != null) costText.text = data.cost.ToString();
        if (descriptionText != null) descriptionText.text = data.description;

        // 効果タイプに応じた背景色
        if (cardBackground != null)
        {
            cardBackground.color = GetEffectColor(data.effectType);
            originalColor = cardBackground.color;
        }

        // 合成プレビューは非表示
        if (fusionPreviewObj != null) fusionPreviewObj.SetActive(false);

        // 生成時ボヨヨン演出チェック
        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.CheckAndPlaySpawnEffect(this);
        }
    }

    // ============================================
    // ドラッグ＆ドロップ
    // ============================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.Battle) return;
        if (gm.battleManager == null || gm.battleManager.battleState != BattleManager.BattleState.PlayerTurn) return;

        // ドラッグ開始時に合体ボタンをクリア
        ClearAllFusionButtons();

        // 元の親と位置を記憶
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalPosition = rectTransform.anchoredPosition;

        // Canvasを取得
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) return;

        // Canvas最前面に移動
        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();

        // レイキャストをブロックしない（ドロップ先を検出するため）
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        Debug.Log($"[CardController] ドラッグ開始: {cardData?.kanji}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rootCanvas == null) return;

        // マウス座標に追従
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.transform as RectTransform,
            eventData.position,
            rootCanvas.worldCamera,
            out localPoint
        );
        rectTransform.localPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        // ドロップ先を判定
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        bool handled = false;

        foreach (var result in results)
        {
            if (result.gameObject == gameObject) continue;

            // Tag "Enemy" → 攻撃発動
            if (result.gameObject.CompareTag("Enemy"))
            {
                handled = HandleDropOnEnemy();
                break;
            }

            // Tag "Card" → 合成チェック
            if (result.gameObject.CompareTag("Card"))
            {
                var targetCard = result.gameObject.GetComponent<CardController>();
                if (targetCard == null) targetCard = result.gameObject.GetComponentInParent<CardController>();

                // 自分自身（同一インスタンス）へのドロップは弾くが、同一IDの別カードは合体可能とする
                if (targetCard != null && targetCard.gameObject != this.gameObject)
                {
                    handled = HandleDropOnCard(targetCard);
                    break;
                }
            }
        }

        if (!handled)
        {
            // 元の位置に戻す
            ReturnToHand();
        }

        Debug.Log($"[CardController] ドラッグ終了: {cardData?.kanji} handled={handled}");
    }

    // ============================================
    // ドロップ処理
    // ============================================

    /// <summary>
    /// 敵にドロップ → 合成チェック または カード効果発動
    /// </summary>
    private bool HandleDropOnEnemy()
    {
        var gm = GameManager.Instance;
        if (gm == null || cardData == null) return false;

        // 敵との強制合体チェック
        if (gm.battleManager.TryEnemyFusion(cardData))
        {
            // 合体成功（カードはTryEnemyFusion内で消費済み）
            onHandChanged?.Invoke();
            Destroy(gameObject);
            return true;
        }

        // 通常のカード使用（合体カードはコスト一律1）
        int actualCost = cardData.isFusionResult ? 1 : cardData.cost;
        if (gm.playerMana < actualCost)
        {
            Debug.Log($"[CardController] マナ不足！ 必要:{actualCost} 現在:{gm.playerMana}");
            // AP不足フィードバック
            var battleUI = gm.battleManager?.battleUI;
            if (VFXManager.Instance != null && battleUI != null && battleUI.playerManaText != null)
                VFXManager.Instance.PlayAPShortageEffect(battleUI.playerManaText.GetComponent<UnityEngine.RectTransform>());
            ReturnToHand();
            return false;
        }

        // カード効果発動
        gm.battleManager.PlayCard(cardData);

        // UIを更新
        onCardUsed?.Invoke();
        onHandChanged?.Invoke();

        // カードオブジェクトを削除
        Destroy(gameObject);
        return true;
    }


    /// <summary>
    /// 別のカードにドロップ → 合成チェック
    /// </summary>
    private bool HandleDropOnCard(CardController targetCard)
    {
        var gm = GameManager.Instance;
        if (gm == null || cardData == null || targetCard.cardData == null) return false;

        // Dictionaryベースの高速レシピ検索
        var resultIds = gm.FindFusionResults(cardData.cardId, targetCard.cardData.cardId);

        if (resultIds.Count > 0)
        {
            if (resultIds.Count == 1)
            {
                // 1種類のみ
                ProceedFusion(targetCard, resultIds[0]);
            }
            else
            {
                // 複数種類あり
                gm.ShowFusionSelectionUI(resultIds, (selectedId) =>
                {
                    ProceedFusion(targetCard, selectedId);
                });
            }
            return true;
        }

        // 合成不可 → 元に戻す
        Debug.Log($"[CardController] 合成不可: 『{cardData.kanji}』+『{targetCard.cardData.kanji}』");
        ReturnToHand();
        return false;
    }

    private void ProceedFusion(CardController targetCard, int resultId)
    {
        var gm = GameManager.Instance;
        var resultCard = gm.GetCardById(resultId);
        if (resultCard == null) return;

        Debug.Log($"[CardController] 合成開始: 『{cardData.kanji}』+『{targetCard.cardData.kanji}』=『{resultCard.kanji}』");

        if (VFXManager.Instance != null)
        {
            // 操作ブロック
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
            if (targetCard.canvasGroup != null) targetCard.canvasGroup.blocksRaycasts = false;

            // 次のカードの出現演出を予約
            VFXManager.Instance.RegisterSpawnEffect(resultCard);

            // 合体位置を事前にキャプチャ（ラムダ実行時にGameObjectが破棄済みのため）
            Vector3 fusionCenterPos = (transform.position + targetCard.transform.position) * 0.5f;

            // 合体演出再生
            VFXManager.Instance.PlayFusionSequence(this, targetCard, () =>
            {
                // データ更新：手札から素材を除去
                gm.hand.Remove(cardData);
                gm.hand.Remove(targetCard.cardData);
                // インベントリから素材を除去（消費型）
                gm.inventory.Remove(cardData);
                gm.inventory.Remove(targetCard.cardData);
                // 結果を手札とインベントリに追加
                gm.hand.Add(resultCard);
                gm.AddToInventory(resultCard);
                if (EncyclopediaManager.Instance != null) EncyclopediaManager.Instance.UnlockCard(resultCard.cardId);

                // 合体成功時 AP+1
                gm.playerMana += 1;
                Debug.Log($"[CardController] 合体成功！AP+1（現在AP:{gm.playerMana}）");

                // 「1 MORE」巨大VFX表示
                if (VFXManager.Instance != null)
                {
                    VFXManager.Instance.PlayOneMoreEffect();
                    // CFXR合体成功パーティクルエフェクト
                    VFXManager.Instance.PlayFusionCFXR(fusionCenterPos);
                }

                // 古いオブジェクト削除
                Destroy(targetCard.gameObject);
                Destroy(gameObject);

                // UI更新（ここで新カードが生成され、Setupが呼ばれるはず）
                onHandChanged?.Invoke();
            });
        }
        else
        {
            // Fallback (VFXManagerなし)
            gm.hand.Remove(cardData);
            gm.hand.Remove(targetCard.cardData);
            gm.inventory.Remove(cardData);
            gm.inventory.Remove(targetCard.cardData);
            gm.hand.Add(resultCard);
            gm.AddToInventory(resultCard);
            if (EncyclopediaManager.Instance != null) EncyclopediaManager.Instance.UnlockCard(resultCard.cardId);

            // 合体成功時 AP+1（Fallback）
            gm.playerMana += 1;
            Debug.Log($"[CardController] 合体成功！AP+1（現在AP:{gm.playerMana}）");

            Destroy(targetCard.gameObject);
            Destroy(gameObject);
            onHandChanged?.Invoke();
        }
    }



    /// <summary>
    /// 元の手札位置に戻す
    /// </summary>
    private void ReturnToHand()
    {
        if (originalParent != null)
        {
            transform.SetParent(originalParent, false);
            transform.SetSiblingIndex(originalSiblingIndex);
            rectTransform.anchoredPosition = originalPosition;
        }
    }

    // ============================================
    // ポインターイベント（合成プレビュー）
    // ============================================

    public void OnPointerEnter(PointerEventData eventData)
    {
        // ドラッグ中のカードが重なった場合 → 合成プレビュー表示
        if (eventData.dragging && eventData.pointerDrag != null)
        {
            var draggedCard = eventData.pointerDrag.GetComponent<CardController>();
            // 自分自身（同一インスタンス）はスキップするが、同一IDの別カードは許可
            if (draggedCard != null && draggedCard.gameObject != this.gameObject && draggedCard.cardData != null && cardData != null)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    int resultId = gm.FindFusionResult(draggedCard.cardData.cardId, cardData.cardId);
                    if (resultId >= 0)
                    {
                        var resultCard = gm.GetCardById(resultId);
                        if (resultCard != null)
                        {
                            ShowFusionPreview(resultCard);
                            return;
                        }
                    }
                }
            }
        }

        // 通常のホバー → 少し持ち上げ + 1.05倍スケールTween
        if (!isHighlighted)
        {
            var pos = rectTransform.anchoredPosition;
            pos.y += 10f;
            rectTransform.anchoredPosition = pos;
        }
        if (_hoverScaleCoroutine != null) StopCoroutine(_hoverScaleCoroutine);
        _hoverScaleCoroutine = StartCoroutine(ScaleTween(Vector3.one * 1.05f, 0.08f));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideFusionPreview();

        // ホバー解除 → スケールを1.0に戻す
        if (!isHighlighted)
        {
            var pos = rectTransform.anchoredPosition;
            pos.y -= 10f;
            rectTransform.anchoredPosition = pos;
        }
        if (_hoverScaleCoroutine != null) StopCoroutine(_hoverScaleCoroutine);
        _hoverScaleCoroutine = StartCoroutine(ScaleTween(Vector3.one, 0.08f));
    }

    private System.Collections.IEnumerator ScaleTween(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }
        transform.localScale = target;
    }

    /// <summary>
    /// 合成プレビューを表示（黄色発光 ＋ 進化後の漢字表示）
    /// </summary>
    private void ShowFusionPreview(KanjiCardData resultCard)
    {
        isHighlighted = true;

        // カードを黄色く発光
        if (cardBackground != null)
        {
            cardBackground.color = new Color(1f, 0.9f, 0.3f, 1f);
        }

        // 合成プレビューテキストを表示
        if (fusionPreviewObj != null)
        {
            fusionPreviewObj.SetActive(true);
            if (fusionPreviewText != null)
            {
                fusionPreviewText.text = resultCard.kanji;
            }
        }
    }

    /// <summary>
    /// 合成プレビューを非表示
    /// </summary>
    public void HideFusionPreview()
    {
        isHighlighted = false;

        // 背景色を戻す
        if (cardBackground != null)
        {
            cardBackground.color = originalColor;
        }

        if (kanjiText != null && cardData != null)
        {
            kanjiText.text = cardData.kanji;
            kanjiText.color = Color.white;
            kanjiText.fontSize = 28;
        }

        if (fusionPreviewObj != null)
        {
            fusionPreviewObj.SetActive(false);
        }
    }

    // ============================================
    // タップ（クリック）による合体ボタン表示
    // ============================================

    /// <summary>
    /// 左クリック: 攻撃系カードなら攻撃予測ボタン表示、そうでなければ合体ボタン表示
    /// 右クリック: 分解
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // ドラッグ操作後のクリックは無視
        if (eventData.dragging) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            var gm = GameManager.Instance;
            bool inBattle = gm != null && gm.currentState == GameState.Battle &&
                            gm.battleManager != null &&
                            gm.battleManager.battleState == BattleManager.BattleState.PlayerTurn;

            if (inBattle)
            {
                HandleTapForActionAndFusion();
            }
            else
            {
                HandleTapForFusion();
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.currentState == GameState.Battle && cardData != null)
            {
                if (cardData.isFusionResult)
                {
                    bool decomposed = gm.DecomposeCard(cardData);
                    if (decomposed)
                    {
                        // 自身を破棄
                        Destroy(this.gameObject);
                        // HandUIの更新予約などのためBattleManager経由でUI更新
                        if (gm.battleManager != null && gm.battleManager.battleUI != null)
                        {
                            gm.battleManager.battleUI.UpdateHandUI();
                        }
                    }
                }
                else
                {
                    Debug.Log($"[CardController] 『{cardData.kanji}』は基礎カードのため分解できません");
                }
            }
        }
    }

    private void HandleTapForActionAndFusion()
    {
        var gm = GameManager.Instance;
        if (gm == null || cardData == null) return;

        // 既に自分が選択されていたら解除
        if (selectedCard == this)
        {
            ClearAllFusionButtons();
            ClearAttackButton();
            selectedCard = null;
            return;
        }

        ClearAllFusionButtons();
        ClearAttackButton();
        selectedCard = this;

        // 選択ハイライト
        isSelected = true;
        if (cardBackground != null)
        {
            if (cardData.effectType == CardEffectType.Attack || cardData.effectType == CardEffectType.AttackAll)
                cardBackground.color = new Color(1f, 0.3f, 0.3f, 1f); // 赤ハイライト
            else if (cardData.effectType == CardEffectType.Heal)
                cardBackground.color = new Color(0.3f, 1f, 0.3f, 1f); // 緑ハイライト
            else if (cardData.effectType == CardEffectType.Defense)
                cardBackground.color = new Color(0.3f, 0.3f, 1f, 1f); // 青ハイライト
            else
                cardBackground.color = new Color(1f, 0.85f, 0.2f, 1f); // 黄ハイライト
        }

        selectionOutline = gameObject.GetComponent<Outline>();
        if (selectionOutline == null) selectionOutline = gameObject.AddComponent<Outline>();
        selectionOutline.effectColor = new Color(1f, 0.9f, 0.3f, 0.9f);
        selectionOutline.effectDistance = new Vector2(3f, 3f);
        selectionOutline.enabled = true;

        var pos = rectTransform.anchoredPosition;
        pos.y += SELECTION_LIFT;
        rectTransform.anchoredPosition = pos;

        // 1. 合体ボタンの表示
        var handArea = transform.parent;
        bool foundAny = false;
        if (handArea != null)
        {
            foreach (Transform child in handArea)
            {
                var otherCard = child.GetComponent<CardController>();
                if (otherCard == null || otherCard.gameObject == this.gameObject || otherCard.cardData == null) continue;

                var resultIds = gm.FindFusionResults(cardData.cardId, otherCard.cardData.cardId);
                if (resultIds.Count > 0)
                {
                    int resultId = resultIds[0];
                    var resultCard = gm.GetCardById(resultId);
                    string resultKanji = resultCard != null ? resultCard.kanji : "?";
                    CreateFusionButtonAboveCard(otherCard, resultId, resultKanji, resultIds);
                    foundAny = true;
                }
            }
        }

        // 合体もアクションもない場合はポップアップ
        if (!foundAny && cardData.cost > 99) // costが非常に高いなどの場合は発動不可とみなす例（今回は常に発動可能とするためスキップ）
        {
            Debug.Log($"[CardController] 『{cardData.kanji}』と合体可能なカードが手札にありません");
            if (VFXManager.Instance != null) VFXManager.Instance.PlayNoFusionPopup(transform.position);
        }

        // 2. 発動（予測）ボタンの表示
        var battleUI = gm.battleManager?.battleUI;
        if (battleUI != null)
        {
            Transform targetTransform = null;
            bool isAttack = (cardData.effectType == CardEffectType.Attack || cardData.effectType == CardEffectType.AttackAll || cardData.effectType == CardEffectType.Stun);

            if (isAttack)
            {
                targetTransform = battleUI.enemyArea?.transform ?? transform;
            }
            else
            {
                // 回復・防御はプレイヤーHPテキスト等の付近
                targetTransform = battleUI.playerHPText?.transform ?? transform;
            }

            string actionText = GetActionPredictText(gm, cardData);
            CreatePredictButton(targetTransform, actionText, isAttack);
        }
    }

    private string GetActionPredictText(GameManager gm, KanjiCardData card)
    {
        string text = "";
        if (card.effectType == CardEffectType.Attack || card.effectType == CardEffectType.AttackAll)
        {
            int dmg = card.effectValue + card.attackModifier + gm.playerAttackBuff;
            var bm = gm.battleManager;
            if (bm != null && bm.currentEnemyData != null)
            {
                if (card.kanji == bm.currentEnemyData.displayKanji) dmg *= 3;
                else if (card.componentCount > bm.currentEnemyData.componentCount) dmg = UnityEngine.Mathf.CeilToInt(dmg * 1.5f);
            }
            text = $"{Mathf.Max(0, dmg)} DMG";
        }
        else if (card.effectType == CardEffectType.Heal)
        {
            text = $"回復 +{card.effectValue}";
        }
        else if (card.effectType == CardEffectType.Defense)
        {
            text = $"防御 +{card.effectValue}";
        }
        else if (card.effectType == CardEffectType.Stun)
        {
            text = "STUN";
        }
        else if (card.effectType == CardEffectType.Special)
        {
            text = $"特殊 (+{UnityEngine.Mathf.CeilToInt(card.effectValue * 0.6f)}HP)";
        }
        return text;
    }

    private void CreatePredictButton(Transform targetTransform, string text, bool isAttack)
    {
        ClearAttackButton();
        if (targetTransform == null) return;

        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var btnGo = new GameObject("ActionPredictButton");
        btnGo.transform.SetParent(canvas.transform, false);

        var rect = btnGo.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160f, 50f);
        
        // CanvasのRenderModeを考慮して座標計算
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Overlayの場合はそのままPixel座標。+150px 上に配置
            rect.position = targetTransform.position + new Vector3(0, 150f, 0);
        }
        else
        {
            // ScreenSpaceCameraやWorldSpaceの場合はWorldToScreenPointなどを使用
            rect.position = Camera.main.WorldToScreenPoint(targetTransform.position + Vector3.up * 1.5f);
        }

        var bg = btnGo.AddComponent<UnityEngine.UI.Image>();
        bg.color = isAttack ? new Color(0.8f, 0.2f, 0.2f, 0.9f) : new Color(0.2f, 0.6f, 0.8f, 0.9f); // 攻撃は赤、その他は青

        var btn = btnGo.AddComponent<UnityEngine.UI.Button>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
        
        tmp.text = text;
        tmp.color = Color.white;
        tmp.fontSize = 24;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        if (appFont != null) tmp.font = appFont;
        tmp.raycastTarget = false;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var selfRef = this;
        btn.onClick.AddListener(() =>
        {
            ClearAttackButton();
            ClearAllFusionButtons();

            var gmInner = GameManager.Instance;
            if (gmInner == null || selfRef == null || selfRef.cardData == null) return;

            int cost = selfRef.cardData.isFusionResult ? 1 : selfRef.cardData.cost;
            if (gmInner.playerMana < cost)
            {
                var buiInner = gmInner.battleManager?.battleUI;
                if (VFXManager.Instance != null && buiInner?.playerManaText != null)
                    VFXManager.Instance.PlayAPShortageEffect(buiInner.playerManaText.GetComponent<RectTransform>());
                return;
            }

            gmInner.battleManager.PlayCard(selfRef.cardData);
            selfRef.onCardUsed?.Invoke();
            selfRef.onHandChanged?.Invoke();
            UnityEngine.Object.Destroy(selfRef.gameObject);
        });

        activeAttackButton = btnGo;
    }


    // ============================================
    // 攻撃予測ボタン
    // ============================================

    /// <summary>
    /// 攻撃系カードのタップ: 敵エリアの上に「攻撃ボタン + 予測ダメージ」を表示
    /// </summary>








    public static void ClearAttackButton()
    {
        if (activeAttackButton != null)
        {
            UnityEngine.Object.Destroy(activeAttackButton);
            activeAttackButton = null;
        }
    }

    /// <summary>
    /// タップ時: 手札の中で合体可能なカードを検索し、そのカードの上に「合体」ボタンを表示
    /// </summary>
    private void HandleTapForFusion()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.Battle) return;
        if (gm.battleManager == null || gm.battleManager.battleState != BattleManager.BattleState.PlayerTurn) return;
        if (cardData == null) return;

        // 既に自分が選択されている場合は解除
        if (selectedCard == this)
        {
            ClearAllFusionButtons();
            return;
        }

        // 合体ボタンをクリア
        ClearAllFusionButtons();
        selectedCard = this;

        // 自カードを選択状態にハイライト（色変更 + 浮き上がり + Outline発光）
        isSelected = true;
        if (cardBackground != null)
        {
            cardBackground.color = new Color(1f, 0.85f, 0.2f, 1f); // 黄色ハイライト
        }

        // Outlineコンポーネントで発光エフェクト
        selectionOutline = gameObject.GetComponent<Outline>();
        if (selectionOutline == null)
        {
            selectionOutline = gameObject.AddComponent<Outline>();
        }
        selectionOutline.effectColor = new Color(1f, 0.9f, 0.3f, 0.9f); // 黄金色の発光
        selectionOutline.effectDistance = new Vector2(3f, 3f);
        selectionOutline.enabled = true;

        // カードを少し上に浮き上がらせる
        var pos = rectTransform.anchoredPosition;
        pos.y += SELECTION_LIFT;
        rectTransform.anchoredPosition = pos;

        // 手札の中で合体可能なカードを検索
        var handArea = transform.parent;
        if (handArea == null) return;

        bool foundAny = false;

        foreach (Transform child in handArea)
        {
            var otherCard = child.GetComponent<CardController>();
            // 自分自身（同一インスタンス）はスキップするが、同一IDの別カードは許可する
            if (otherCard == null || otherCard.gameObject == this.gameObject || otherCard.cardData == null) continue;

            // 合体レシピを検索
            var resultIds = gm.FindFusionResults(cardData.cardId, otherCard.cardData.cardId);
            if (resultIds.Count > 0)
            {
                // 合体ボタンを表示
                int resultId = resultIds[0]; // 最初の結果を使用
                var resultCard = gm.GetCardById(resultId);
                string resultKanji = resultCard != null ? resultCard.kanji : "?";

                CreateFusionButtonAboveCard(otherCard, resultId, resultKanji, resultIds);
                foundAny = true;
            }
        }

        if (!foundAny)
        {
            Debug.Log($"[CardController] 『{cardData.kanji}』と合体可能なカードが手札にありません");

            // 「合体不可」ポップアップ表示
            if (VFXManager.Instance != null)
            {
                VFXManager.Instance.PlayNoFusionPopup(transform.position);
            }

            ClearAllFusionButtons();
        }
    }

    /// <summary>
    /// カードの上に「合体」ボタンを生成
    /// </summary>
    private void CreateFusionButtonAboveCard(CardController targetCard, int resultId, string resultKanji, List<int> allResultIds)
    {
        // ボタンの親はCanvasルート（UIの最前面に表示するため）
        Canvas canvas = targetCard.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        GameObject btnObj = new GameObject("FusionButton");
        btnObj.transform.SetParent(canvas.transform, false);
        btnObj.transform.SetAsLastSibling();

        // ボタンの位置をカードの上に設定
        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(100f, 40f);

        // カードのワールド位置をCanvas座標に変換
        Vector3 cardWorldPos = targetCard.transform.position;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, cardWorldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, screenPoint, canvas.worldCamera, out Vector2 localPoint);
        btnRect.anchoredPosition = localPoint + new Vector2(0, 80f); // カードの上に配置

        // ボタン背景
        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(1f, 0.75f, 0f, 0.95f); // ゴールド

        // ボタンコンポーネント
        var button = btnObj.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = btnImage.color;
        colors.highlightedColor = new Color(1f, 0.85f, 0.2f, 1f);
        colors.pressedColor = new Color(0.9f, 0.6f, 0f, 1f);
        button.colors = colors;

        // ボタンテキスト
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(btnObj.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = $"合体→{resultKanji}";
        tmp.fontSize = 18;
        tmp.color = new Color(0.1f, 0.05f, 0f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        if (appFont != null) tmp.font = appFont;

        // ボタンクリック時の処理
        CardController sourceRef = this;
        CardController targetRef = targetCard;

        if (allResultIds.Count == 1)
        {
            button.onClick.AddListener(() =>
            {
                ClearAllFusionButtons();
                sourceRef.ProceedFusion(targetRef, resultId);
            });
        }
        else
        {
            // 複数結果がある場合は選択UIを表示
            button.onClick.AddListener(() =>
            {
                ClearAllFusionButtons();
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.ShowFusionSelectionUI(allResultIds, (selectedId) =>
                    {
                        sourceRef.ProceedFusion(targetRef, selectedId);
                    });
                }
            });
        }

        // ボヨヨン出現アニメーション
        btnObj.transform.localScale = Vector3.zero;
        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.PlaySpawnEffect(btnObj);
        }
        else
        {
            btnObj.transform.localScale = Vector3.one;
        }

        activeFusionButtons.Add(btnObj);
    }

    /// <summary>
    /// 全ての合体ボタン・攻撃ボタンをクリアし、選択状態を解除
    /// </summary>
    public static void ClearAllFusionButtons()
    {
        ClearAttackButton();

        foreach (var btn in activeFusionButtons)
        {
            if (btn != null) Object.Destroy(btn);
        }
        activeFusionButtons.Clear();

        // 選択カードのハイライトを完全に解除
        if (selectedCard != null)
        {
            // 背景色を戻す
            if (selectedCard.cardBackground != null)
            {
                selectedCard.cardBackground.color = selectedCard.originalColor;
            }

            // Outlineを除去
            if (selectedCard.selectionOutline != null)
            {
                selectedCard.selectionOutline.enabled = false;
                Object.Destroy(selectedCard.selectionOutline);
                selectedCard.selectionOutline = null;
            }

            // 浮き上がりを戻す
            if (selectedCard.isSelected && selectedCard.rectTransform != null)
            {
                var pos = selectedCard.rectTransform.anchoredPosition;
                pos.y -= SELECTION_LIFT;
                selectedCard.rectTransform.anchoredPosition = pos;
                selectedCard.isSelected = false;
            }
        }
        selectedCard = null;
    }

    // ============================================
    // ユーティリティ
    // ============================================

    private Color GetEffectColor(CardEffectType type)
    {
        // タスク3: カード背景はダークグレー（#222222）に統一
        // 属性による色差はBattleUI側でボーダー（枠）として適用
        return new Color(0.133f, 0.133f, 0.133f, 0.95f);
    }
}
