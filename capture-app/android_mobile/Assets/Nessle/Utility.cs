using UnityEngine;

namespace Nessle
{
    public static class Utility
    {
        public static T GetOrAddComponent<T>(this Component component)
            where T : Component => component.gameObject.GetOrAddComponent<T>();

        public static T GetOrAddComponent<T>(this GameObject gameObject)
            where T : Component
        {
            if (!gameObject.TryGetComponent<T>(out var component))
                component = gameObject.AddComponent<T>();

            return component;
        }

        public static Color WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static void FillParent(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.offsetMin = new Vector2(0, 0);
            rectTransform.offsetMax = new Vector2(0, 0);
        }

        public static void FillParentWidth(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0, rectTransform.anchorMin.y);
            rectTransform.anchorMax = new Vector2(1, rectTransform.anchorMax.y);
            rectTransform.offsetMin = new Vector2(0, rectTransform.offsetMin.y);
            rectTransform.offsetMax = new Vector2(0, rectTransform.offsetMax.y);
        }

        public static void FillParentHeight(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 0);
            rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 1);
            rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, 0);
            rectTransform.offsetMax = new Vector2(rectTransform.offsetMax.x, 0);
        }

        public static void AnchorToTop(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 1);
            rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 1);
        }

        public static void AnchorToBottom(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 0);
            rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 0);
        }

        public static void AnchorToLeft(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0, rectTransform.anchorMin.y);
            rectTransform.anchorMax = new Vector2(0, rectTransform.anchorMax.y);
        }

        public static void AnchorToRight(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(1, rectTransform.anchorMin.y);
            rectTransform.anchorMax = new Vector2(1, rectTransform.anchorMax.y);
        }

        public static void AnchorToTopLeft(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
        }

        public static void AnchorToTopRight(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(1, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
        }

        public static void AnchorToBottomLeft(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);
        }

        public static void AnchorToBottomRight(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(1, 0);
            rectTransform.anchorMax = new Vector2(1, 0);
        }

        public static Color Alpha(this Color color, float alpha)
            => new Color(color.r, color.g, color.b, alpha);

        // public static PointerEventData ToNesslePointerEventData(UnityPointerEventData eventData, Canvas canvas)
        // {
        //     if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, eventData.position, eventData.pressEventCamera, out var localPosition) ||
        //         !RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, eventData.position - eventData.delta, eventData.pressEventCamera, out var localPreviousPoint) ||
        //         !RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, eventData.pressPosition, eventData.pressEventCamera, out var localPressPosition))
        //     {
        //         throw new Exception("Event raycast failed to intercept rect plane. Are you using a worldspace canvas?");
        //     }

        //     var result = new PointerEventData();
        //     result.position = localPosition;
        //     result.delta = localPosition - localPreviousPoint;
        //     result.pressPosition = localPressPosition;
        //     result.clickCount = eventData.clickCount;
        //     result.scrollDelta = eventData.scrollDelta;
        //     result.dragging = eventData.dragging;
        //     result.button = (PointerEventData.InputButton)(int)eventData.button;

        //     return result;
        // }
    }
}