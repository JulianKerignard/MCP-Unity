using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class PanelAnimator : MonoBehaviour
{
    public enum AnimType { Fade, SlideUp, SlideDown, ScaleBounce }

    public AnimType showAnim = AnimType.Fade;
    public float showDuration = 0.3f;
    public float hideDuration = 0.2f;

    CanvasGroup cg;
    RectTransform rect;
    Vector2 originalPos;
    Vector3 originalScale;
    Coroutine activeRoutine;
    bool initialized;

    void Init()
    {
        if (initialized) return;
        initialized = true;
        cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        rect = GetComponent<RectTransform>();
        originalPos = rect.anchoredPosition;
        originalScale = rect.localScale;
    }

    public void Show(Action onComplete = null)
    {
        Init();
        gameObject.SetActive(true);
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(AnimateShow(onComplete));
    }

    public void Hide(Action onComplete = null)
    {
        Init();
        if (!gameObject.activeSelf) return;
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(AnimateHide(onComplete));
    }

    public void ShowInstant()
    {
        Init();
        gameObject.SetActive(true);
        cg.alpha = 1f;
        rect.anchoredPosition = originalPos;
        rect.localScale = originalScale;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    public void HideInstant()
    {
        Init();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    IEnumerator AnimateShow(Action onComplete)
    {
        cg.interactable = false;
        cg.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < showDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / showDuration);
            float ease = EaseOutBack(t);
            float fadeT = Mathf.Clamp01(t * 2f);

            cg.alpha = fadeT;

            switch (showAnim)
            {
                case AnimType.SlideUp:
                    rect.anchoredPosition = originalPos + Vector2.down * 40f * (1f - ease);
                    break;
                case AnimType.SlideDown:
                    rect.anchoredPosition = originalPos + Vector2.up * 40f * (1f - ease);
                    break;
                case AnimType.ScaleBounce:
                    float s = Mathf.LerpUnclamped(0.8f, 1f, ease);
                    rect.localScale = originalScale * s;
                    break;
            }

            yield return null;
        }

        cg.alpha = 1f;
        rect.anchoredPosition = originalPos;
        rect.localScale = originalScale;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        onComplete?.Invoke();
    }

    IEnumerator AnimateHide(Action onComplete)
    {
        cg.interactable = false;
        cg.blocksRaycasts = false;

        float elapsed = 0f;
        float startAlpha = cg.alpha;
        Vector2 startPos = rect.anchoredPosition;
        Vector3 startScale = rect.localScale;

        while (elapsed < hideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / hideDuration);
            float ease = EaseInQuad(t);

            cg.alpha = Mathf.Lerp(startAlpha, 0f, ease);

            switch (showAnim)
            {
                case AnimType.SlideUp:
                    rect.anchoredPosition = Vector2.Lerp(startPos, originalPos + Vector2.down * 30f, ease);
                    break;
                case AnimType.SlideDown:
                    rect.anchoredPosition = Vector2.Lerp(startPos, originalPos + Vector2.up * 30f, ease);
                    break;
                case AnimType.ScaleBounce:
                    rect.localScale = Vector3.Lerp(startScale, originalScale * 0.85f, ease);
                    break;
            }

            yield return null;
        }

        cg.alpha = 0f;
        rect.anchoredPosition = originalPos;
        rect.localScale = originalScale;
        gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    static float EaseOutBack(float t)
    {
        float c = 1.4f;
        return 1f + (c + 1f) * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f);
    }

    static float EaseInQuad(float t) => t * t;
}
