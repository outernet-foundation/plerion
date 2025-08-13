using System;
using ObserveThing;
using UnityEngine;
using UnityEngine.UI;

namespace Nessle
{
    public class ScrollRectControl : UnityComponentControl<ScrollRect>
    {
        public ScrollbarControl horizontalScrollbar { get; }
        public ScrollbarControl verticalScrollbar { get; }
        public ImageMaskControl viewport { get; }
        public UnityComponentControl<VerticalLayoutGroup> content { get; }

        public ScrollRectControl(
            ScrollbarControl horizontalScrollbar = default,
            ScrollbarControl verticalScrollbar = default,
            ImageMaskControl viewport = default,
            UnityComponentControl<VerticalLayoutGroup> content = default
        ) : base()
        {
            this.Children(
                this.horizontalScrollbar = horizontalScrollbar ?? UIBuilder.Scrollbar().SizeDelta(new Vector2(0, 10)).FillParentWidth().SetPivot(new Vector2(0.5f, 0)).AnchorToBottom().Direction(Scrollbar.Direction.LeftToRight),
                this.verticalScrollbar = verticalScrollbar ?? UIBuilder.Scrollbar().SizeDelta(new Vector2(10, 0)).FillParentHeight().SetPivot(new Vector2(1, 0.5f)).AnchorToRight().Direction(Scrollbar.Direction.BottomToTop),
                this.viewport = viewport ?? UIBuilder.ImageMask().FillParent().Children(
                    this.content = content ?? UIBuilder.VerticalLayout()
                )
            );

            component.horizontalScrollbar = this.horizontalScrollbar.component;
            component.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            component.horizontalScrollbarSpacing = -3;

            component.verticalScrollbar = this.verticalScrollbar.component;
            component.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            component.verticalScrollbarSpacing = -3;

            component.vertical = true;
            component.horizontal = true;

            component.viewport = (RectTransform)this.viewport.component.transform;
            component.content = (RectTransform)this.content.component.transform;
        }
    }
}
