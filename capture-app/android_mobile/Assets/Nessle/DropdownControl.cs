using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nessle
{
    public class DropdownControl : UnityComponentControl<TMP_Dropdown>
    {
        public IUnityComponentControl<Image> background { get; }
        public IUnityComponentControl<TextMeshProUGUI> captionText { get; }
        // public UnityComponentControl<Image> captionImage { get; }
        public ScrollRectControl template { get; }
        public ImageMaskControl viewport { get; }
        public IUnityComponentControl<Toggle> itemToggle { get; }
        public IUnityComponentControl<Image> itemBackground { get; }
        public IUnityComponentControl<TextMeshProUGUI> itemLabel { get; }
        // public UnityComponentControl<Image> itemImage { get; }
        public IUnityComponentControl<Image> itemCheckmark { get; }

        public DropdownControl() : base(UIBuilder.GameObject<TMP_Dropdown>())
        {
            this.Children(
                background = new UnityComponentControl<Image>(gameObject.AddComponent<Image>()),
                captionText = UIBuilder.Label().FillParent().Alignment(TextAlignmentOptions.CaplineLeft).Margin(new Vector4(12, 0, 0, 0)),
                template = UIBuilder.ScrollRect().Style(x =>
                {
                    x.EnableHorizontalScroll(false)
                        .FillParentWidth()
                        .SizeDelta(new Vector2(0, 150))
                        .SetPivot(new Vector2(0, 1))
                        .AnchorToBottom();

                    x.content
                        .FitContentVertical(ContentSizeFitter.FitMode.PreferredSize)
                        .SetPivot(new Vector2(0.5f, 1))
                        .AnchorToTop()
                        .FillParentWidth()
                        .SizeDelta(new Vector2(0, 150));

                }).Content(
                    UIBuilder.VerticalLayout().ControlChildSize(true).ChildForceExpandWidth(true).Children( //Content
                        itemToggle = UIBuilder.ComponentControl<Toggle>().FillParentWidth().PreferredHeight(30).Children(
                            itemBackground = UIBuilder.Image().FillParent(),
                            itemCheckmark = UIBuilder.Image().AnchorToRight().SetPivot(new Vector2(1f, 0.5f)).SizeDelta(new Vector2(20, 20)).LocalPosition(new Vector3(-10, 0, 0)),
                            itemLabel = UIBuilder.Label().Alignment(TextAlignmentOptions.CaplineLeft).Margin(new Vector4(10, 0, 0, 0)).FillParent()
                        )
                    )
                )
            );

            itemToggle.component.graphic = itemCheckmark.component;
            itemToggle.component.targetGraphic = itemBackground.component;

            component.targetGraphic = background.component;
            component.template = (RectTransform)template.component.transform;
            component.captionText = captionText.component;
            component.itemText = itemLabel.component;

            template.gameObject.SetActive(false);
        }
    }
}
