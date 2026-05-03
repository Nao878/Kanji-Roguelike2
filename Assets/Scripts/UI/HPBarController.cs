using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HPバー表示管理（Tweenアニメーション・格ゲー風遅延バー・オーバーヒール金色対応）
/// </summary>
public class HPBarController : MonoBehaviour
{
    [Header("Bar Parts")]
    public Image normalBar;
    public Image delayBar;      // 格ゲー風：ダメージ後に遅れて減る赤いバー
    public Image overhealBar;
    public TextMeshProUGUI statusIcon;

    [Header("Colors")]
    public Color normalColor    = new Color(0.2f, 0.8f, 0.2f);
    public Color lowHPColor     = new Color(0.9f, 0.2f, 0.2f);
    public Color overhealColor  = new Color(1f, 0.85f, 0f);

    [Header("Animation")]
    public float mainTweenDuration    = 0.35f;
    public float delayBarWait         = 0.25f;
    public float delayBarTweenDuration = 0.45f;

    private float currentFill = 1f;
    private Coroutine mainTween;
    private Coroutine delayTween;

    public bool IsOverhealed { get; private set; }

    // ──────────────────────────────────────────────
    // 通常の HP 更新（Tween あり）
    // ──────────────────────────────────────────────
    public void SetHP(int current, int max)
    {
        if (normalBar == null) return;
        IsOverhealed = current > max;

        float newFill = IsOverhealed ? 1f : (max > 0 ? (float)current / max : 0f);
        bool isDamage = newFill < currentFill;

        RefreshColor(newFill);
        RefreshOverhealBar(current, max);

        // Update bar width based on max HP (e.g., 2 pixels per HP)
        UpdateBarWidth(max);

        if (mainTween != null) StopCoroutine(mainTween);
        mainTween = StartCoroutine(CoMain(newFill));

        if (delayBar != null)
        {
            if (isDamage)
            {
                if (delayTween != null) StopCoroutine(delayTween);
                delayTween = StartCoroutine(CoDelay(newFill));
            }
            else
            {
                // 回復は遅延バーも即時追従
                delayBar.fillAmount = newFill;
            }
        }

        currentFill = newFill;
    }

    // ──────────────────────────────────────────────
    // アニメなし即時セット（戦闘開始・リセット時）
    // ──────────────────────────────────────────────
    public void SetHPImmediate(int current, int max)
    {
        if (normalBar == null) return;
        IsOverhealed = current > max;
        float fill = IsOverhealed ? 1f : (max > 0 ? (float)current / max : 0f);
        normalBar.fillAmount = fill;
        if (delayBar != null) delayBar.fillAmount = fill;
        currentFill = fill;
        RefreshColor(fill);
        RefreshOverhealBar(current, max);
        UpdateBarWidth(max);
    }

    private void UpdateBarWidth(int maxHP)
    {
        var rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            // If anchored to stretch (anchorMin.x == 0, anchorMax.x == 1), we should change it to left-aligned fixed width
            if (rect.anchorMin.x == 0f && rect.anchorMax.x == 1f)
            {
                rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
                rect.anchorMax = new Vector2(0f, rect.anchorMax.y);
                rect.pivot = new Vector2(0f, rect.pivot.y);
                rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
            }
            
            // Base width + scaled width. For example, 100 width for 50 HP.
            // Adjust the multiplier as appropriate for the UI design.
            float targetWidth = Mathf.Clamp(50f + maxHP * 1.5f, 80f, 300f);
            rect.sizeDelta = new Vector2(targetWidth, rect.sizeDelta.y);
        }
    }


    // ──────────────────────────────────────────────
    // ステータスアイコン（スタン/バフ表示）
    // ──────────────────────────────────────────────
    public void SetStatusIcon(string text)
    {
        if (statusIcon == null) return;
        if (string.IsNullOrEmpty(text))
            statusIcon.gameObject.SetActive(false);
        else
        {
            statusIcon.text = text;
            statusIcon.gameObject.SetActive(true);
        }
    }

    // ──────────────────────────────────────────────
    // 内部ヘルパー
    // ──────────────────────────────────────────────
    private void RefreshColor(float fill)
    {
        if (normalBar == null) return;
        // オーバーヒール時も通常バーは緑のまま。黄色はoverhealBarで別表示
        normalBar.color = fill < 0.3f ? lowHPColor : normalColor;
    }

    private void RefreshOverhealBar(int current, int max)
    {
        if (overhealBar == null) return;
        if (IsOverhealed)
        {
            float extra = max > 0 ? (float)(current - max) / max : 0f;
            overhealBar.fillAmount = Mathf.Clamp01(extra);
            overhealBar.gameObject.SetActive(true);
        }
        else
        {
            overhealBar.gameObject.SetActive(false);
        }
    }

    // メインバー：ease-out でスッと減る（0.35 秒）
    private IEnumerator CoMain(float target)
    {
        float start = normalBar.fillAmount;
        float elapsed = 0f;
        while (elapsed < mainTweenDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / mainTweenDuration);
            float eased = 1f - (1f - t) * (1f - t);   // ease-out quad
            normalBar.fillAmount = Mathf.Lerp(start, target, eased);
            yield return null;
        }
        normalBar.fillAmount = target;
        mainTween = null;
    }

    // 遅延バー：0.25 秒待機後にゆっくり追いかける（0.45 秒）
    private IEnumerator CoDelay(float target)
    {
        yield return new WaitForSeconds(delayBarWait);
        float start = delayBar.fillAmount;
        float elapsed = 0f;
        while (elapsed < delayBarTweenDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / delayBarTweenDuration);
            delayBar.fillAmount = Mathf.Lerp(start, target, t);
            yield return null;
        }
        delayBar.fillAmount = target;
        delayTween = null;
    }
}
