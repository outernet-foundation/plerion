using System;
using System.Collections.Generic;
using ObserveThing;
using UnityEngine;
using UnityEngine.UI;

namespace Nessle
{
    public class ButtonControl : UnityControl, IUnityComponentControl<Button>
    {
        public Button component { get; private set; }
        public UnityComponentControl<HorizontalLayoutGroup> contentLayout { get; }
        public UnityComponentControl<Image> background { get; }

        public ButtonControl() : base(new GameObject(nameof(ButtonControl), typeof(Button), typeof(HorizontalLayoutGroup)))
        {
            component = gameObject.GetComponent<Button>();

            this.Children(
                background = UIBuilder.Image().FillParent().IgnoreLayout(true),
                contentLayout = new UnityComponentControl<HorizontalLayoutGroup>(gameObject.GetComponent<HorizontalLayoutGroup>())
                    .ChildForceExpand(false)
                    .ControlChildSize(true)
                    .Alignment(TextAnchor.MiddleCenter)
                    .Padding(new RectOffset(3, 3, 3, 3)).Spacing(5)
            );

            component.targetGraphic = background.component;
        }
    }

    public static class ButtonControlExtensions
    {
        public static T Content<T>(this T control, params IControl[] children)
            where T : ButtonControl
        {
            control.contentLayout.Children((IEnumerable<IControl>)children);
            return control;
        }

        public static T Content<T>(this T control, IEnumerable<IControl> children)
            where T : ButtonControl
        {
            control.contentLayout.Children(children);
            return control;
        }

        public static TParent Content<TParent, TChildren>(this TParent control, IListObservable<TChildren> children)
            where TParent : ButtonControl where TChildren : IControl
        {
            control.contentLayout.Children(children);
            return control;
        }

        public static T StyleContentLayout<T>(this T control, Action<UnityComponentControl<HorizontalLayoutGroup>> style)
            where T : ButtonControl
        {
            style(control.contentLayout);
            return control;
        }

        public static T StyleBackground<T>(this T control, Action<UnityComponentControl<Image>> style)
            where T : ButtonControl
        {
            style(control.background);
            return control;
        }
    }
}