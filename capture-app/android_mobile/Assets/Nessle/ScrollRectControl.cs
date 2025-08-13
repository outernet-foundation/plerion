using System;
using ObserveThing;
using UnityEngine;
using UnityEngine.UI;

namespace Nessle
{
    public class ScrollRectControl : UnityComponentControl<ScrollRect>
    {
        public IUnityComponentControl<Image> background { get; }
        public IUnityControl viewport { get; }
        public IUnityControl content { get; }
        public ScrollbarControl horizontalScrollbar { get; }
        public ScrollbarControl verticalScrollbar { get; }

        public ScrollRectControl(
            IUnityComponentControl<Image> background = default,
            ScrollbarControl horizontalScrollbar = default,
            ScrollbarControl verticalScrollbar = default,
            IUnityControl viewport = default,
            IUnityControl content = default
        ) : base(UIBuilder.GameObject<ScrollRect>())
        {
            this.Children(
                this.background = (background ?? UIBuilder.Image()).Name("Background").FillParent(),
                this.viewport = (viewport ?? UIBuilder.ComponentControl<RectMask2D>()).Name("Viewport").FillParent().Children(
                    this.content = (content ?? UIBuilder.VerticalLayout().ControlChildSize(true)).Name("Content")
                ),
                this.horizontalScrollbar = horizontalScrollbar ?? UIBuilder.Scrollbar().SizeDelta(new Vector2(0, 10)).FillParentWidth().SetPivot(new Vector2(0.5f, 0)).AnchorToBottom().Direction(Scrollbar.Direction.LeftToRight),
                this.verticalScrollbar = verticalScrollbar ?? UIBuilder.Scrollbar().SizeDelta(new Vector2(10, 0)).FillParentHeight().SetPivot(new Vector2(1, 0.5f)).AnchorToRight().Direction(Scrollbar.Direction.BottomToTop)
            );

            component.horizontalScrollbar = this.horizontalScrollbar.component;
            component.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            component.horizontalScrollbarSpacing = -3;

            component.verticalScrollbar = this.verticalScrollbar.component;
            component.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            component.verticalScrollbarSpacing = -3;

            component.vertical = true;
            component.horizontal = true;

            component.viewport = this.viewport.rectTransform;
            component.content = this.content.rectTransform;
        }
    }
}
