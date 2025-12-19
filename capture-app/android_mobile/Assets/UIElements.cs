using UnityEngine;
using Nessle;

using static Nessle.UIBuilder;
using ObserveThing;
using System;
using UnityEngine.EventSystems;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine.Events;

namespace PlerionClient.Client
{
    public static partial class UIElements
    {
        public static UIElementSet elements;

        public struct LabeledButtonProps
        {
            public IValueObservable<string> label;
            public TextStyleProps labelStyle;
            public UnityAction onClick;
            public IValueObservable<bool> interactable;
            public ImageProps background;
        }

        public static IControl LabeledButton(LabeledButtonProps props = default)
        {
            props.labelStyle.horizontalAlignment = props.labelStyle.horizontalAlignment ?? Props.From(HorizontalAlignmentOptions.Center);
            props.labelStyle.verticalAlignment = props.labelStyle.verticalAlignment ?? Props.From(VerticalAlignmentOptions.Capline);

            return Button(new ButtonProps()
            {
                interactable = props.interactable,
                background = props.background,
                onClick = props.onClick
            }).Children(Text(new TextProps() { style = props.labelStyle, value = props.label }));
        }

        public static IControl Row(LayoutProps props = default, PrimitiveControl<LayoutProps> prefab = default)
        {
            props.childControlWidth = props.childControlWidth ?? Props.From(true);
            props.childControlHeight = props.childControlHeight ?? Props.From(true);
            props.childAlignment = props.childAlignment ?? Props.From(TextAnchor.MiddleLeft);
            props.spacing = props.spacing ?? Props.From(10f);
            return HorizontalLayout(props, prefab);
        }

        public static IControl SafeArea()
            => Control(new GameObject("Safe Area", typeof(SafeArea)));

        public class EditableLabelProps
        {
            public InputFieldProps inputField;
            public TextStyleProps label;
            public IValueObservable<bool> inputFieldActive;
        }

        public static IControl EditableLabel(EditableLabelProps props = default)
        {
            UnityAction<string> onEndEdit = props.inputField.onEndEdit;
            ValueObservable<bool> inputFieldActive = new ValueObservable<bool>();

            props.inputField.onEndEdit = x =>
            {
                inputFieldActive.From(false);
                onEndEdit?.Invoke(x);
            };

            return Control(new GameObject("EditableLabel"))
                .OnPointerClick(x =>
                {
                    var eventSystem = EventSystem.current;
                    var target = x.pointerPress;

                    eventSystem.SetSelectedGameObject(target);

                    UniTask.WaitForSeconds(0.5f).ContinueWith(() =>
                    {
                        if (eventSystem.currentSelectedGameObject != target)
                            return;

                        inputFieldActive.From(true);
                    }).Forget();
                })
                .Children(
                    InputField(props.inputField)
                        .FillParent()
                        .Active(props.inputFieldActive)
                        .Selected(props.inputFieldActive)
                        .OnDeselect(_ => inputFieldActive.From(false)),
                    Text(new TextProps()
                    {
                        style = props.label,
                        value = Observables.Combine(
                            props.inputField.value,
                            props.inputField.placeholderValue,
                            (input, placeholder) => string.IsNullOrEmpty(input) ? placeholder : input
                        )
                    })
                    .FillParent()
                );
        }

        public static IControl PropertyLabel(IControl label, IControl control)
        {
            return HorizontalLayout(new LayoutProps()
            {
                childScaleHeight = Props.From(true),
                childControlWidth = Props.From(true),
                childForceExpandHeight = Props.From(true),
                spacing = Props.From(10f)
            }).Children(label, control.FlexibleWidth(true));
        }

        public static IControl Title(TextProps props = default, PrimitiveControl<TextProps> prefab = default)
        {
            props.style.horizontalAlignment = props.style.horizontalAlignment ?? Props.From(HorizontalAlignmentOptions.Center);
            props.style.verticalAlignment = props.style.verticalAlignment ?? Props.From(VerticalAlignmentOptions.Capline);
            props.style.fontSize = props.style.fontSize ?? Props.From(48f);

            return Text(props, prefab);
        }

        public static IControl TightRowsWideColumns(LayoutProps props = default, PrimitiveControl<LayoutProps> prefab = default)
        {
            props.childControlWidth = props.childControlWidth ?? Props.From(true);
            props.childControlHeight = props.childControlHeight ?? Props.From(true);
            props.childForceExpandWidth = props.childForceExpandWidth ?? Props.From(true);
            props.spacing = props.spacing ?? Props.From(30f);

            return VerticalLayout(props, prefab);
        }

        public struct LabeledControlProps
        {
            public LayoutProps layout;
            public TextProps label;
            public IValueObservable<float> labelWidth;
            public IControl control;
        }

        public static IControl LabeledControl(LabeledControlProps props = default)
        {
            props.layout.childAlignment = props.layout.childAlignment ?? Props.From(TextAnchor.MiddleLeft);
            props.layout.childControlWidth = props.layout.childControlWidth ?? Props.From(true);
            props.layout.childControlHeight = props.layout.childControlHeight ?? Props.From(true);

            props.label.style.verticalAlignment = props.label.style.verticalAlignment ?? Props.From(VerticalAlignmentOptions.Capline);
            props.label.style.overflowMode = props.label.style.overflowMode ?? Props.From(TextOverflowModes.Ellipsis);
            props.label.style.textWrappingMode = props.label.style.textWrappingMode ?? Props.From(TextWrappingModes.NoWrap);

            props.labelWidth = props.labelWidth ?? Props.From(200f);

            var control = HorizontalLayout(props.layout)
                .Children(
                    Text(props.label)
                        .MinWidth(props.labelWidth)
                        .PreferredWidth(props.labelWidth)
                );

            if (props.control != null)
            {
                control.children.Add(
                    props.control.FlexibleWidth(true)
                );
            }

            return control;
        }

        public struct RoundIconButtonProps
        {
            public ImageProps icon;
            public UnityAction onClick;
            public IValueObservable<bool> interactable;
            public ImageProps background;
        }

        public static IControl RoundIconButton(RoundIconButtonProps props = default)
        {
            return RoundButton(new ButtonProps()
            {
                background = props.background,
                interactable = props.interactable,
                onClick = props.onClick
            }).Children(Image(props.icon));
        }

        public static IControl RoundButton(ButtonProps props = default)
            => Control(props, elements.roundButton);

        public static IControl Foldout(FoldoutProps props = default)
            => Control(props, elements.foldout);

        // public static IControl AdaptiveLabel(IControl label, IControl content)
        // {
        //     return VerticalLayout().Style(root =>
        //     {
        //         root.props.childControlWidth.From(true);
        //         root.props.childControlHeight.From(true);
        //         IControl narrowContentRegion = HorizontalLayout("narrowContent").Style(narrowContentRegion =>
        //         {
        //             narrowContentRegion.props.childControlWidth.From(true);
        //             narrowContentRegion.props.childControlHeight.From(true);
        //             narrowContentRegion.FlexibleWidth(true);
        //             narrowContentRegion.FlexibleHeight(true);
        //         });

        //         IControl wideContentRegion = VerticalLayout("wideContent").Style(wideContentRegion =>
        //         {
        //             wideContentRegion.FlexibleWidth(true);
        //             wideContentRegion.props.childControlWidth.From(true);
        //             wideContentRegion.props.childControlHeight.From(true);
        //         });

        //         var contentIsWide = Observables.Combine(
        //             content.rect, narrowContentRegion.rect,
        //             (_, region) =>
        //             {
        //                 var tooWide = LayoutUtility.GetPreferredWidth(content.rectTransform) > region.width;
        //                 var tooTall = LayoutUtility.GetPreferredHeight(content.rectTransform) > region.height;
        //                 return tooWide || tooTall;
        //             }
        //         );

        //         wideContentRegion.Active(contentIsWide);
        //         narrowContentRegion.Active(contentIsWide.SelectDynamic(x => !x));
        //         content.parent.From(contentIsWide.SelectDynamic(x => x ? wideContentRegion : narrowContentRegion));

        //         root.Children(
        //             Row().Children(label, narrowContentRegion),
        //             wideContentRegion
        //         );

        //         content.FillParent();
        //     });
        // }

        public static IControl Columns(IValueObservable<float> spacing)
        {
            spacing = spacing ?? Props.From(0f);
            float spacingValue = 0;

            return Control(new GameObject("Columns"))
                .Style(columns =>
                {
                    columns.AddBinding(
                        Observables.Any(columns.children, spacing).Subscribe(x =>
                        {
                            if (x.source == spacing)
                                spacingValue = ((IValueEventArgs<float>)x).currentValue;

                            float step = 1f / columns.children.count;

                            for (int i = 0; i < columns.children.count; i++)
                            {
                                var child = columns.children[i];
                                child.rectTransform.anchorMin = new Vector2(step * i, 0);
                                child.rectTransform.anchorMax = new Vector2(step * (i + 1), 1);

                                child.rectTransform.offsetMin = new Vector2(Mathf.Lerp(0f, spacingValue, i * step), 0);
                                child.rectTransform.offsetMax = new Vector2(Mathf.Lerp(-spacingValue, 0f, (i + 1) * step), 0);
                            }
                        })
                    );
                });
        }

        public static T Columns<T>(this T control, float spacing, params IControl[] controls)
            where T : IControl
        {
            control.children.From(controls);
            float step = 1f / controls.Length;
            for (int i = 0; i < controls.Length; i++)
            {
                var child = controls[i];
                child.rectTransform.anchorMin = new Vector2(step * i, 0);
                child.rectTransform.anchorMax = new Vector2(step * (i + 1), 1);

                child.rectTransform.offsetMin = new Vector2(Mathf.Lerp(0f, spacing, i * step), 0);
                child.rectTransform.offsetMax = new Vector2(Mathf.Lerp(-spacing, 0f, (i + 1) * step), 0);
            }

            return control;
        }
    }
}