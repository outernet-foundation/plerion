using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nessle
{
    public class ScrollbarControl : UnityValueControl<float>, IUnityComponentControl<Scrollbar>
    {
        public Scrollbar component { get; }
        public UnityComponentControl<Image> background { get; private set; }
        public UnityComponentControl<RectTransform> slidingArea { get; private set; }
        public UnityComponentControl<Image> handle { get; private set; }

        public ScrollbarControl(
            UnityComponentControl<Image> background = default,
            UnityComponentControl<RectTransform> slidingArea = default,
            UnityComponentControl<Image> handle = default
        ) : base(new GameObject(nameof(ScrollbarControl), typeof(Scrollbar)))
        {
            component = gameObject.GetComponent<Scrollbar>();

            this.Children(
                this.background = background ?? UIBuilder.Image().FillParent().Color(new Color(0.1686275f, 0.1686275f, 0.1686275f, 1)),
                this.slidingArea = slidingArea ?? UIBuilder.RectTransform().FillParent().Children(
                    this.handle = handle ?? UIBuilder.Image().FillParent().Color(new Color(0.2980392f, 0.2980392f, 0.2980392f, 1))
                )
            );

            component.handleRect = (RectTransform)this.handle.component.transform;
            component.targetGraphic = this.handle.component;
            component.onValueChanged.AddListener(x => value = x);
        }

        protected override void HandleValueChanged()
        {
            component.value = value;
        }
    }

    public static class ScrollbarControlExtensions
    {
        public static T Background<T>(this T view, Action<UnityComponentControl<Image>> accessBackground)
            where T : ScrollbarControl
        {
            accessBackground(view.background);
            return view;
        }

        public static T SlidingArea<T>(this T view, Action<UnityComponentControl<RectTransform>> accessSlidingArea)
            where T : ScrollbarControl
        {
            accessSlidingArea(view.slidingArea);
            return view;
        }

        public static T Handle<T>(this T view, Action<UnityComponentControl<Image>> accessHandle)
            where T : ScrollbarControl
        {
            accessHandle(view.handle);
            return view;
        }
    }
}