using TMPro;
using UnityEngine;
using UnityEngine.UI;

using ScrollbarDirection = UnityEngine.UI.Scrollbar.Direction;
using ScrollbarComponent = UnityEngine.UI.Scrollbar;

namespace Nessle
{
    public class UnityButton
    {
        public Button buttonComponent;
        public Image background;
        public HorizontalLayoutGroup layout;

        public UnityButton()
        {
            buttonComponent = new GameObject("Button", typeof(Button), typeof(RectTransform)).GetComponent<Button>();

            background = new GameObject("Background", typeof(Image)).GetComponent<Image>();
            background.rectTransform.SetParent(buttonComponent.transform, false);
            background.rectTransform.FillParent();
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            layout = buttonComponent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            buttonComponent.targetGraphic = background;
        }
    }

    public class UnityInputField
    {
        public TMP_InputField inputFieldComponent;
        public Image background;
        public RectTransform viewport;
        public TextMeshProUGUI inputText;
        public TextMeshProUGUI placeholder;

        public UnityInputField()
        {
            inputFieldComponent = new GameObject("InputField", typeof(TMP_InputField)).GetComponent<TMP_InputField>();

            background = new GameObject("Background", typeof(Image)).GetComponent<Image>();
            background.rectTransform.SetParent(inputFieldComponent.transform, false);
            background.rectTransform.FillParent();

            viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(inputFieldComponent.transform, false);
            viewport.FillParent();

            inputText = new GameObject("InputText", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            inputText.rectTransform.SetParent(viewport.transform, false);
            inputText.rectTransform.FillParent();

            placeholder = new GameObject("PlaceholderText", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            placeholder.rectTransform.SetParent(viewport.transform, false);
            placeholder.rectTransform.FillParent();

            inputFieldComponent.targetGraphic = background;
            inputFieldComponent.textComponent = inputText;
            inputFieldComponent.placeholder = placeholder;
            inputFieldComponent.textViewport = viewport;

            inputFieldComponent.enabled = false;
            inputFieldComponent.enabled = true;
        }
    }

    public class UnityScrollbar
    {
        public ScrollbarComponent scrollbarComponent;
        public RectTransform scrollbarTransform;
        public Image background;
        public Image handle;

        public UnityScrollbar()
        {
            scrollbarComponent = new GameObject("Scrollbar", typeof(ScrollbarComponent)).GetComponent<ScrollbarComponent>();
            scrollbarTransform = scrollbarComponent.GetComponent<RectTransform>();
            scrollbarComponent.direction = ScrollbarDirection.LeftToRight;

            background = new GameObject("Background", typeof(Image)).GetComponent<Image>();
            background.rectTransform.SetParent(scrollbarComponent.transform, false);
            background.rectTransform.FillParent();

            handle = new GameObject("Handle", typeof(Image)).GetComponent<Image>();
            handle.rectTransform.SetParent(background.transform, false);
            handle.rectTransform.FillParent();

            scrollbarComponent.handleRect = handle.rectTransform;
            scrollbarComponent.targetGraphic = handle;
        }
    }

    public class UnityScrollRect
    {
        public ScrollRect scrollRectComponent;
        public RectTransform scrollRectTransform;
        public Image background;
        public RectMask2D viewport;
        public ContentSizeFitter content;
        public RectTransform contentRectTransform;
        public UnityScrollbar horizontalScrollbar;
        public UnityScrollbar verticalScrollbar;

        public UnityScrollRect()
        {
            scrollRectComponent = new GameObject("ScrollRect", typeof(Image), typeof(ScrollRect)).GetComponent<ScrollRect>();
            scrollRectTransform = (RectTransform)scrollRectComponent.transform;
            background = scrollRectComponent.GetComponent<Image>();

            viewport = new GameObject("Viewport", typeof(RectMask2D)).GetComponent<RectMask2D>();
            viewport.rectTransform.pivot = new Vector2(0, 1f);
            viewport.rectTransform.SetParent(scrollRectComponent.transform, false);
            viewport.rectTransform.FillParent();

            content = new GameObject("Content", typeof(ContentSizeFitter), typeof(VerticalLayoutGroup)).GetComponent<ContentSizeFitter>();
            contentRectTransform = (RectTransform)content.transform;
            content.transform.SetParent(viewport.transform, false);

            scrollRectComponent.content = (RectTransform)content.transform;
            scrollRectComponent.content.pivot = new Vector2(0, 1);
            scrollRectComponent.content.AnchorToTop();

            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;

            horizontalScrollbar = new UnityScrollbar();
            horizontalScrollbar.scrollbarComponent.direction = ScrollbarDirection.LeftToRight;
            horizontalScrollbar.scrollbarTransform.SetParent(scrollRectComponent.transform, false);
            horizontalScrollbar.scrollbarTransform.pivot = new Vector2(0f, 0f);
            horizontalScrollbar.scrollbarTransform.anchorMin = new Vector2(0f, 0f);
            horizontalScrollbar.scrollbarTransform.anchorMax = new Vector2(1f, 0f);
            horizontalScrollbar.scrollbarTransform.offsetMin = new Vector2(0, 0f);
            horizontalScrollbar.scrollbarTransform.offsetMax = new Vector2(5f, 10f);

            verticalScrollbar = new UnityScrollbar();
            verticalScrollbar.scrollbarComponent.direction = ScrollbarDirection.TopToBottom;
            verticalScrollbar.scrollbarTransform.SetParent(scrollRectComponent.transform, false);
            verticalScrollbar.scrollbarTransform.pivot = new Vector2(1f, 1f);
            verticalScrollbar.scrollbarTransform.anchorMin = new Vector2(1f, 0f);
            verticalScrollbar.scrollbarTransform.anchorMax = new Vector2(1f, 1f);
            verticalScrollbar.scrollbarTransform.offsetMin = new Vector2(-10f, -5f);
            verticalScrollbar.scrollbarTransform.offsetMax = new Vector2(0f, 0f);

            scrollRectComponent.viewport = viewport.rectTransform;
            scrollRectComponent.horizontalScrollbar = horizontalScrollbar.scrollbarComponent;
            scrollRectComponent.verticalScrollbar = verticalScrollbar.scrollbarComponent;
        }
    }

    public class UnityDropdown
    {
        public TMP_Dropdown dropdownComponent;
        public Image background;
        public TextMeshProUGUI captionText;
        public Image arrow;
        public UnityScrollRect template;
        public Toggle itemToggle;
        public Image itemBackground;
        public TextMeshProUGUI itemText;
        public Image itemCheckmark;

        public UnityDropdown()
        {
            dropdownComponent = new GameObject("Dropdown", typeof(TMP_Dropdown)).GetComponent<TMP_Dropdown>();

            background = new GameObject("Background", typeof(Image)).GetComponent<Image>();
            background.rectTransform.SetParent(dropdownComponent.transform, false);
            background.rectTransform.FillParent();

            captionText = new GameObject("Caption Text", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            captionText.transform.SetParent(dropdownComponent.transform, false);
            captionText.rectTransform.FillParent();

            arrow = new GameObject("Arrow", typeof(Image)).GetComponent<Image>();
            arrow.rectTransform.SetParent(dropdownComponent.transform, false);
            arrow.rectTransform.AnchorToRight();
            arrow.rectTransform.sizeDelta = new Vector2(10, 10);
            arrow.rectTransform.anchoredPosition = new Vector3(-15, -1.2f);

            template = new UnityScrollRect();
            template.scrollRectTransform.SetParent(dropdownComponent.transform, false);
            template.scrollRectTransform.sizeDelta = new Vector2(0, 150);
            template.scrollRectTransform.pivot = new Vector2(0, 1);
            template.scrollRectTransform.FillParentWidth();
            template.scrollRectTransform.AnchorToBottom();

            var content = new GameObject("Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<VerticalLayoutGroup>();
            var contentTransform = content.GetComponent<RectTransform>();
            contentTransform.SetParent(template.scrollRectTransform, false);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            content.childControlWidth = true;
            content.childControlHeight = true;
            contentTransform.pivot = new Vector2(0.5f, 1f);
            contentTransform.AnchorToTop();
            contentTransform.FillParentWidth();
            contentTransform.sizeDelta = new Vector2(0, 150f);

            itemToggle = new GameObject("Item Toggle", typeof(Toggle), typeof(LayoutElement)).GetComponent<Toggle>();
            var toggleTransform = itemToggle.GetComponent<RectTransform>();
            toggleTransform.SetParent(contentTransform, false);
            itemToggle.GetComponent<LayoutElement>().preferredHeight = 30;

            itemBackground = new GameObject("Item Background", typeof(Image)).GetComponent<Image>();
            itemBackground.rectTransform.SetParent(itemToggle.transform, false);
            itemBackground.rectTransform.FillParent();

            itemText = new GameObject("Item Text", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            itemText.transform.SetParent(itemToggle.transform, false);
            itemText.rectTransform.FillParent();

            itemCheckmark = new GameObject("Item Checkmark", typeof(Image)).GetComponent<Image>();
            itemCheckmark.transform.SetParent(itemToggle.transform, false);
            itemCheckmark.rectTransform.pivot = new Vector2(1f, 0.5f);
            itemCheckmark.rectTransform.sizeDelta = new Vector2(15, 15);
            itemCheckmark.rectTransform.AnchorToRight();
            itemCheckmark.rectTransform.anchoredPosition = new Vector3(-10, 0, 0);

            itemToggle.graphic = itemCheckmark.gameObject.GetComponent<Image>();
            itemToggle.targetGraphic = itemBackground.gameObject.GetComponent<Image>();

            dropdownComponent.targetGraphic = background;
            dropdownComponent.template = template.scrollRectTransform;
            dropdownComponent.captionText = captionText;
            dropdownComponent.itemText = itemText;

            template.scrollRectComponent.content = contentTransform;
            template.scrollRectComponent.gameObject.SetActive(false);
        }
    }
}