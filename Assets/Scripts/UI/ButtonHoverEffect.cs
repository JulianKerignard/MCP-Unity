using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public float hoverScale = 1.08f;
    public float pressScale = 0.95f;
    public float animSpeed = 12f;

    Vector3 originalScale;
    float targetScale = 1f;
    bool isHovered;

    void Start()
    {
        originalScale = transform.localScale;
    }

    void Update()
    {
        float current = transform.localScale.x / originalScale.x;
        float newScale = Mathf.Lerp(current, targetScale, Time.unscaledDeltaTime * animSpeed);
        transform.localScale = originalScale * newScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        targetScale = hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        targetScale = 1f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = pressScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = isHovered ? hoverScale : 1f;
    }
}
