using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HPバー表示管理（オーバーヒール金色表示対応）
/// </summary>
public class HPBarController : MonoBehaviour
{
    [Header("Bar Parts")]
    public Image normalBar;
    public Image overhealBar;
    public TextMeshProUGUI statusIcon;

    [Header("Colors")]
    public Color normalColor = new Color(0.2f, 0.8f, 0.2f);
    public Color lowHPColor = new Color(0.9f, 0.2f, 0.2f);
    public Color overhealColor = new Color(1f, 0.85f, 0f);

    public bool IsOverhealed { get; private set; }

    public void SetHP(int current, int max)
    {
        if (normalBar == null) return;
        IsOverhealed = current > max;

        float normalFill = IsOverhealed ? 1f : (max > 0 ? (float)current / max : 0f);
        normalBar.fillAmount = normalFill;

        if (IsOverhealed)
        {
            normalBar.color = overhealColor;
            if (overhealBar != null)
            {
                float extra = max > 0 ? (float)(current - max) / max : 0f;
                overhealBar.fillAmount = Mathf.Clamp01(extra);
                overhealBar.gameObject.SetActive(true);
            }
        }
        else
        {
            normalBar.color = normalFill < 0.3f ? lowHPColor : normalColor;
            if (overhealBar != null) overhealBar.gameObject.SetActive(false);
        }
    }

    public void SetStatusIcon(string text)
    {
        if (statusIcon == null) return;
        if (string.IsNullOrEmpty(text))
        {
            statusIcon.gameObject.SetActive(false);
        }
        else
        {
            statusIcon.text = text;
            statusIcon.gameObject.SetActive(true);
        }
    }
}
