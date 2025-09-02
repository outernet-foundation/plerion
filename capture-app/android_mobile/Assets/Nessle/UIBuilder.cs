using System;
using System.Linq;
using ObserveThing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using ImageType = UnityEngine.UI.Image.Type;
using TMP_ContentType = TMPro.TMP_InputField.ContentType;
using TMP_LineType = TMPro.TMP_InputField.LineType;
using ScrollRectMovementType = UnityEngine.UI.ScrollRect.MovementType;
using ScrollbarDirection = UnityEngine.UI.Scrollbar.Direction;
using ScrollRectScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility;

namespace Nessle
{
    public static class UIBuilder
    {
        public static Action<TextProps> DefaultTextStyle;
        public static Action<ImageProps> DefaultImageStyle;
        public static Action<ButtonProps> DefaultButtonStyle;
        public static Action<InputFieldProps> DefaultInputFieldStyle;
        public static Action<ScrollbarProps> DefaultScrollbarStyle;
        public static Action<ScrollRectProps> DefaultScrollRectStyle;
        public static Action<DropdownProps> DefaultDropdownStyle;

        public static TextProps DefaultTextProps()
        {
            var props = new TextProps();
            DefaultTextStyle?.Invoke(props);
            return props;
        }

        public static ImageProps DefaultImageProps()
        {
            var props = new ImageProps();
            DefaultImageStyle?.Invoke(props);
            return props;
        }

        public static ButtonProps DefaultButtonProps()
        {
            var props = new ButtonProps();
            DefaultButtonStyle?.Invoke(props);
            return props;
        }

        public static InputFieldProps DefaultInputFieldProps()
        {
            var props = new InputFieldProps();
            DefaultInputFieldStyle?.Invoke(props);
            return props;
        }

        public static ScrollbarProps DefaultScrollbarProps()
        {
            var props = new ScrollbarProps();
            DefaultScrollbarStyle?.Invoke(props);
            return props;
        }

        public static ScrollRectProps DefaultScrollRectProps()
        {
            var props = new ScrollRectProps();
            DefaultScrollRectStyle?.Invoke(props);
            return props;
        }

        public static DropdownProps DefaultDropdownProps()
        {
            var props = new DropdownProps();

            props.captionText.margin.value = new Vector4(12, 0, 0, 0);
            props.captionText.alignment.value = TextAlignmentOptions.CaplineLeft;

            props.arrow.preserveAspect.value = true;
            props.arrow.sprite.value = UIResources.ArrowDown;

            props.itemText.margin.value = new Vector4(12, 0, 0, 0);
            props.itemText.alignment.value = TextAlignmentOptions.CaplineLeft;

            props.itemCheckmark.preserveAspect.value = true;
            props.itemCheckmark.sprite.value = UIResources.Checkmark;

            DefaultDropdownStyle?.Invoke(props);
            return props;
        }

        public static Control Control(string name, params Type[] components)
            => new Control(name, components);

        public static Control<T> Control<T>(string name)
            where T : new() => new Control<T>(new T(), name);

        public static Control<T> Control<T>(string name, T props)
            => new Control<T>(props, name);

        public static Control<T> Control<T>(RectTransform control, T props)
            => new Control<T>(props, control.gameObject);

        public static Control<T> Style<T>(this Control<T> control, Action<T> style)
        {
            style(control.props);
            return control;
        }

        public class TextProps : IDisposable
        {
            public ValueObservable<string> text { get; } = new ValueObservable<string>();
            public ValueObservable<float> fontSize { get; } = new ValueObservable<float>();
            public ValueObservable<Color> color { get; } = new ValueObservable<Color>();
            public ValueObservable<TextAlignmentOptions> alignment { get; } = new ValueObservable<TextAlignmentOptions>();
            public ValueObservable<TMP_FontAsset> font { get; } = new ValueObservable<TMP_FontAsset>();
            public ValueObservable<FontStyles> fontStyle { get; } = new ValueObservable<FontStyles>();
            public ValueObservable<Vector4> margin { get; } = new ValueObservable<Vector4>();
            public ValueObservable<TextWrappingModes> wrappingMode { get; } = new ValueObservable<TextWrappingModes>();
            public ValueObservable<TextOverflowModes> overflowMode { get; } = new ValueObservable<TextOverflowModes>();
            public ValueObservable<FontWeight> fontWeight { get; } = new ValueObservable<FontWeight>();

            public void Dispose()
            {
                text.Dispose();
                fontSize.Dispose();
                color.Dispose();
                alignment.Dispose();
                font.Dispose();
                fontStyle.Dispose();
                margin.Dispose();
                wrappingMode.Dispose();
                overflowMode.Dispose();
                fontWeight.Dispose();
            }
        }

        public static Control<TextProps> Text(TextProps props = default)
            => Text(props ?? DefaultTextProps(), out var _);

        private static Control<TextProps> Text(TextProps props, out TextMeshProUGUI textComponent)
        {
            props = props ?? DefaultTextProps();
            var control = new Control<TextProps>(props, "Text", typeof(TextMeshProUGUI));
            var text = control.gameObject.GetComponent<TextMeshProUGUI>();

            control.AddBinding(
                props.text.Subscribe(x => text.text = x.currentValue),
                props.fontSize.Subscribe(x => text.fontSize = x.currentValue),
                props.color.Subscribe(x => text.color = x.currentValue),
                props.alignment.Subscribe(x => text.alignment = x.currentValue),
                props.font.Subscribe(x => text.font = x.currentValue),
                props.fontStyle.Subscribe(x => text.fontStyle = x.currentValue),
                props.margin.Subscribe(x => text.margin = x.currentValue),
                props.wrappingMode.Subscribe(x => text.textWrappingMode = x.currentValue),
                props.overflowMode.Subscribe(x => text.overflowMode = x.currentValue),
                props.fontWeight.Subscribe(x => text.fontWeight = x.currentValue)
            );

            textComponent = text;

            return control;
        }

        public class ImageProps : IDisposable
        {
            public ValueObservable<Sprite> sprite { get; } = new ValueObservable<Sprite>();
            public ValueObservable<Color> color { get; } = new ValueObservable<Color>(Color.white);
            public ValueObservable<float> pixelsPerUnitMultiplier { get; } = new ValueObservable<float>();
            public ValueObservable<bool> preserveAspect { get; } = new ValueObservable<bool>();
            public ValueObservable<ImageType> imageType { get; } = new ValueObservable<ImageType>();

            public void Dispose()
            {
                sprite.Dispose();
                color.Dispose();
                pixelsPerUnitMultiplier.Dispose();
                preserveAspect.Dispose();
                imageType.Dispose();
            }
        }

        public static Control<ImageProps> Image(ImageProps props = default)
            => Image(props ?? DefaultImageProps(), out var _);

        private static Control<ImageProps> Image(ImageProps props, out Image imageComponent)
        {
            props = props ?? DefaultImageProps();
            var control = new Control<ImageProps>(props, "Image", typeof(Image));
            var image = control.gameObject.GetComponent<Image>();

            control.AddBinding(
                props.sprite.Subscribe(x => image.sprite = x.currentValue),
                props.color.Subscribe(x => image.color = x.currentValue),
                props.pixelsPerUnitMultiplier.Subscribe(x => image.pixelsPerUnitMultiplier = x.currentValue),
                props.preserveAspect.Subscribe(x => image.preserveAspect = x.currentValue),
                props.imageType.Subscribe(x => image.type = x.currentValue)
            );

            imageComponent = image;

            return control;
        }

        public static IDisposable BindImage(ImageProps props, Image image)
        {
            return new ComposedDisposable(
                props.sprite.Subscribe(x => image.sprite = x.currentValue),
                props.color.Subscribe(x => image.color = x.currentValue),
                props.pixelsPerUnitMultiplier.Subscribe(x => image.pixelsPerUnitMultiplier = x.currentValue),
                props.preserveAspect.Subscribe(x => image.preserveAspect = x.currentValue),
                props.imageType.Subscribe(x => image.type = x.currentValue)
            );
        }

        public class ButtonProps : IDisposable
        {
            public event Action onClick;
            public ValueObservable<bool> selected { get; } = new ValueObservable<bool>();
            public ValueObservable<bool> interactable { get; } = new ValueObservable<bool>(true);
            public ValueObservable<RectOffset> padding { get; } = new ValueObservable<RectOffset>(new RectOffset());
            public ValueObservable<float> spacing { get; } = new ValueObservable<float>(10);
            public ImageProps background { get; } = DefaultImageProps();

            public void Dispose()
            {
                interactable.Dispose();
                background.Dispose();
            }

            public void InvokeOnClick()
            {
                onClick?.Invoke();
            }
        }

        public static Control<ButtonProps> Button(ButtonProps props = default)
        {
            props = props ?? DefaultButtonProps();
            var button = new UnityButton();
            var control = new Control<ButtonProps>(props, button.buttonComponent.gameObject);

            button.buttonComponent.onClick.AddListener(props.InvokeOnClick);

            control.AddBinding(
                props.interactable.Subscribe(x => button.buttonComponent.interactable = x.currentValue),
                props.padding.Subscribe(x => button.layout.padding = x.currentValue),
                props.spacing.Subscribe(x => button.layout.spacing = x.currentValue),
                BindImage(props.background, button.background)
            );

            return control;
        }

        public class LayoutProps : IDisposable
        {
            public ValueObservable<RectOffset> padding { get; } = new ValueObservable<RectOffset>(new RectOffset());
            public ValueObservable<float> spacing { get; } = new ValueObservable<float>();
            public ValueObservable<TextAnchor> childAlignment { get; } = new ValueObservable<TextAnchor>();
            public ValueObservable<bool> reverseArrangement { get; } = new ValueObservable<bool>();
            public ValueObservable<bool> childForceExpandHeight { get; } = new ValueObservable<bool>();
            public ValueObservable<bool> childForceExpandWidth { get; } = new ValueObservable<bool>();
            public ValueObservable<bool> childControlWidth { get; } = new ValueObservable<bool>();
            public ValueObservable<bool> childControlHeight { get; } = new ValueObservable<bool>();
            public ValueObservable<bool> childScaleWidth { get; } = new ValueObservable<bool>();
            public ValueObservable<bool> childScaleHeight { get; } = new ValueObservable<bool>();

            public void Dispose()
            {
                padding.Dispose();
                spacing.Dispose();
                childAlignment.Dispose();
                reverseArrangement.Dispose();
                childForceExpandHeight.Dispose();
                childForceExpandWidth.Dispose();
                childControlWidth.Dispose();
                childControlHeight.Dispose();
                childScaleWidth.Dispose();
                childScaleHeight.Dispose();
            }
        }

        public static Control<LayoutProps> HorizontalLayout(LayoutProps props = default)
        {
            props = props ?? new LayoutProps();
            var control = new Control<LayoutProps>(props, "HorizontalLayout", typeof(HorizontalLayoutGroup));
            var layout = control.gameObject.GetComponent<HorizontalLayoutGroup>();
            control.AddBinding(BindLayout(props, layout));
            return control;
        }

        public static Control<LayoutProps> VerticalLayout(LayoutProps props = default)
        {
            props = props ?? new LayoutProps();
            var control = new Control<LayoutProps>(props, "VerticalLayout", typeof(VerticalLayoutGroup));
            var layout = control.gameObject.GetComponent<VerticalLayoutGroup>();
            control.AddBinding(BindLayout(props, layout));
            return control;
        }

        public static IDisposable BindLayout(LayoutProps props, HorizontalOrVerticalLayoutGroup layout)
        {
            return new ComposedDisposable(
                props.padding.Subscribe(x => layout.padding = x.currentValue),
                props.spacing.Subscribe(x => layout.spacing = x.currentValue),
                props.childAlignment.Subscribe(x => layout.childAlignment = x.currentValue),
                props.reverseArrangement.Subscribe(x => layout.reverseArrangement = x.currentValue),
                props.childForceExpandHeight.Subscribe(x => layout.childForceExpandHeight = x.currentValue),
                props.childForceExpandWidth.Subscribe(x => layout.childForceExpandWidth = x.currentValue),
                props.childControlWidth.Subscribe(x => layout.childControlWidth = x.currentValue),
                props.childControlHeight.Subscribe(x => layout.childControlHeight = x.currentValue),
                props.childScaleWidth.Subscribe(x => layout.childScaleWidth = x.currentValue),
                props.childScaleHeight.Subscribe(x => layout.childScaleHeight = x.currentValue)
            );
        }

        public class InputFieldProps : IDisposable
        {
            public event Action<string> onEndEdit;

            public ValueObservable<bool> interactable { get; } = new ValueObservable<bool>(true);
            public ValueObservable<TMP_ContentType> contentType { get; } = new ValueObservable<TMP_ContentType>();
            public ValueObservable<bool> readOnly { get; } = new ValueObservable<bool>();
            public ValueObservable<TMP_LineType> lineType { get; } = new ValueObservable<TMP_LineType>();
            public ValueObservable<int> characterLimit { get; } = new ValueObservable<int>();
            public TextProps inputText { get; } = DefaultTextProps();
            public TextProps placeholder { get; } = DefaultTextProps();
            public ImageProps background { get; } = DefaultImageProps();

            public void Dispose()
            {
                contentType.Dispose();
                readOnly.Dispose();
                lineType.Dispose();
                characterLimit.Dispose();
                interactable.Dispose();
                inputText.Dispose();
                placeholder.Dispose();
                background.Dispose();
            }

            public void RaiseEndEdit()
            {
                onEndEdit?.Invoke(inputText.text.value);
            }
        }

        public static Control<InputFieldProps> InputField(InputFieldProps props = default)
            => InputField(props ?? DefaultInputFieldProps(), out var _);

        private static Control<InputFieldProps> InputField(InputFieldProps props, out TMP_InputField inputFieldComponent)
        {
            props = props ?? DefaultInputFieldProps();
            var control = new Control<InputFieldProps>(props, "InputField", typeof(TMP_InputField));
            var inputField = control.gameObject.GetComponent<TMP_InputField>();

            IControl viewport;

            control.Children(
                Image(props.background, out var background).Name("Background").FillParent(),
                viewport = Control("Viewport", typeof(RectMask2D)).FillParent().Children(
                    Text(props.inputText, out var inputText).Name("InputText").FillParent(),
                    Text(props.placeholder, out var placeholder).Name("Placeholder").FillParent()
                )
            );

            inputField.targetGraphic = background;
            inputField.textViewport = viewport.transform;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;

            inputField.enabled = false;
            inputField.enabled = true;

            inputField.onEndEdit.AddListener(x =>
            {
                props.inputText.text.value = x;
                props.RaiseEndEdit();
            });

            control.AddBinding(
                props.inputText.text.Subscribe(x => inputField.text = x.currentValue),
                props.contentType.Subscribe(x => inputField.contentType = x.currentValue),
                props.readOnly.Subscribe(x => inputField.readOnly = x.currentValue),
                props.lineType.Subscribe(x => inputField.lineType = x.currentValue),
                props.characterLimit.Subscribe(x => inputField.characterLimit = x.currentValue),
                props.interactable.Subscribe(x => inputField.interactable = x.currentValue)
            );

            inputFieldComponent = inputField;

            return control;
        }

        public static Control Space()
            => new Control("Space");

        public class ScrollbarProps : IDisposable
        {
            public ValueObservable<bool> selected { get; } = new ValueObservable<bool>();
            public ValueObservable<bool> interactable { get; } = new ValueObservable<bool>();
            public ValueObservable<float> value { get; } = new ValueObservable<float>();
            public ValueObservable<ScrollbarDirection> direction { get; } = new ValueObservable<ScrollbarDirection>();
            public ValueObservable<float> size { get; } = new ValueObservable<float>();
            public ValueObservable<int> numberOfSteps { get; } = new ValueObservable<int>();
            public ImageProps handle { get; } = DefaultImageProps();
            public ImageProps background { get; } = DefaultImageProps();

            public void Dispose()
            {
                value.Dispose();
                direction.Dispose();
                size.Dispose();
                numberOfSteps.Dispose();
                handle.Dispose();
                background.Dispose();
            }
        }

        public static Control<ScrollbarProps> Scrollbar(ScrollbarProps props = default)
            => Scrollbar(props ?? DefaultScrollbarProps(), out var _);

        public static Control<ScrollbarProps> Scrollbar(ScrollbarProps props, out Scrollbar scrollbarComponent)
        {
            props = props ?? DefaultScrollbarProps();
            var control = new Control<ScrollbarProps>(props, "Scrollbar", typeof(Scrollbar));
            var scrollbar = control.gameObject.GetComponent<Scrollbar>();

            control.Children(
                Image(props.background, out var background)
                    .FillParent()
                    .Children(
                        Image(props.handle, out var handle)
                            .FillParent()
                    )
            );

            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.onValueChanged.AddListener(x => props.value.value = x);

            control.AddBinding(
                props.value.Subscribe(x => scrollbar.value = x.currentValue),
                props.direction.Subscribe(x => scrollbar.direction = x.currentValue),
                props.size.Subscribe(x => scrollbar.size = x.currentValue),
                props.numberOfSteps.Subscribe(x => scrollbar.numberOfSteps = x.currentValue),
                BindImage(props.background, background),
                BindImage(props.handle, handle)
            );

            scrollbarComponent = scrollbar;

            return control;
        }

        private static IDisposable BindScrollbar(ScrollbarProps props, UnityScrollbar scrollbar)
        {
            return new ComposedDisposable(
                props.value.Subscribe(x => scrollbar.scrollbarComponent.value = x.currentValue),
                props.direction.Subscribe(x => scrollbar.scrollbarComponent.direction = x.currentValue),
                props.size.Subscribe(x => scrollbar.scrollbarComponent.size = x.currentValue),
                props.numberOfSteps.Subscribe(x => scrollbar.scrollbarComponent.numberOfSteps = x.currentValue),
                BindImage(props.background, scrollbar.background),
                BindImage(props.handle, scrollbar.handle)
            );
        }

        public class ScrollRectProps : IDisposable
        {
            public ValueObservable<Vector2> value { get; } = new ValueObservable<Vector2>(new Vector2(0, 1));
            public ValueObservable<bool> horizontal { get; } = new ValueObservable<bool>(true);
            public ValueObservable<bool> vertical { get; } = new ValueObservable<bool>(true);
            public ValueObservable<ScrollRectMovementType> movementType { get; } = new ValueObservable<ScrollRectMovementType>(ScrollRectMovementType.Elastic);
            public ValueObservable<bool> inertia { get; } = new ValueObservable<bool>(true);
            public ValueObservable<float> decelerationRate { get; } = new ValueObservable<float>(1);
            public ValueObservable<float> scrollSensitivity { get; } = new ValueObservable<float>(1);
            public ValueObservable<ScrollRectScrollbarVisibility> horizontalScrollbarVisibility { get; } = new ValueObservable<ScrollRectScrollbarVisibility>(ScrollRectScrollbarVisibility.AutoHideAndExpandViewport);
            public ValueObservable<ScrollRectScrollbarVisibility> verticalScrollbarVisibility { get; } = new ValueObservable<ScrollRectScrollbarVisibility>(ScrollRectScrollbarVisibility.AutoHideAndExpandViewport);
            public ValueObservable<float> horizontalScrollbarSpacing { get; } = new ValueObservable<float>(3);
            public ValueObservable<float> verticalScrollbarSpacing { get; } = new ValueObservable<float>(3);
            public ImageProps background { get; } = DefaultImageProps();
            public ScrollbarProps verticalScrollbar { get; } = DefaultScrollbarProps();
            public ScrollbarProps horizontalScrollbar { get; } = DefaultScrollbarProps();

            public ScrollRectProps()
            {
                verticalScrollbar.direction.value = ScrollbarDirection.BottomToTop;
                horizontalScrollbar.direction.value = ScrollbarDirection.LeftToRight;
            }

            public void Dispose()
            {
                value.Dispose();
                horizontal.Dispose();
                vertical.Dispose();
                movementType.Dispose();
                inertia.Dispose();
                decelerationRate.Dispose();
                scrollSensitivity.Dispose();
                horizontalScrollbarVisibility.Dispose();
                verticalScrollbarVisibility.Dispose();
                horizontalScrollbarSpacing.Dispose();
                verticalScrollbarSpacing.Dispose();
                background.Dispose();
                horizontalScrollbar.Dispose();
                verticalScrollbar.Dispose();
            }
        }

        public static Control<ScrollRectProps> ScrollRect(ScrollRectProps props = default)
            => ScrollRect(props ?? DefaultScrollRectProps(), out var _);

        private static Control<ScrollRectProps> ScrollRect(ScrollRectProps props, out ScrollRect scrollRectComponent)
        {
            var control = new Control<ScrollRectProps>(props, "ScrollRect", typeof(Image), typeof(ScrollRect));
            var scrollRect = control.gameObject.GetComponent<ScrollRect>();

            IControl viewport = default;
            IControl content = default;

            control.Children(
                viewport = new Control("Viewport", typeof(RectMask2D))
                    .SetPivot(new Vector2(0, 1f))
                    .FillParent()
                    .Children(
                        content = new Control("Content", typeof(ContentSizeFitter), typeof(VerticalLayoutGroup))
                            .SetPivot(new Vector2(0, 1))
                            .AnchorToTop()
                    ),
                Scrollbar(props.horizontalScrollbar, out var horizontalScrollbar)
                    .SetPivot(Vector2.zero)
                    .AnchorMin(Vector2.zero)
                    .AnchorMax(Vector2.right)
                    .OffsetMin(Vector2.zero)
                    .OffsetMax(new Vector2(5f, 10f))
                    .Style(x => x.direction.value = ScrollbarDirection.LeftToRight),
                Scrollbar(props.verticalScrollbar, out var verticalScrollbar)
                    .SetPivot(Vector2.one)
                    .AnchorMin(Vector2.right)
                    .AnchorMax(Vector2.one)
                    .OffsetMin(new Vector2(-10f, -5f))
                    .OffsetMax(Vector2.zero)
                    .Style(x => x.direction.value = ScrollbarDirection.TopToBottom)
            );

            var contentSizeFitter = content.gameObject.GetComponent<ContentSizeFitter>();
            var contentLayout = content.gameObject.GetComponent<VerticalLayoutGroup>();

            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;

            scrollRect.viewport = viewport.transform;
            scrollRect.horizontalScrollbar = horizontalScrollbar;
            scrollRect.verticalScrollbar = verticalScrollbar;
            scrollRect.onValueChanged.AddListener(x => props.value.value = x);

            control.childParentOverride = (RectTransform)scrollRect.content.transform;
            control.AddBinding(
                props.value.Subscribe(x => scrollRect.normalizedPosition = x.currentValue),
                props.movementType.Subscribe(x => scrollRect.movementType = x.currentValue),
                props.inertia.Subscribe(x => scrollRect.inertia = x.currentValue),
                props.decelerationRate.Subscribe(x => scrollRect.decelerationRate = x.currentValue),
                props.scrollSensitivity.Subscribe(x => scrollRect.scrollSensitivity = x.currentValue),
                props.horizontalScrollbarVisibility.Subscribe(x => scrollRect.horizontalScrollbarVisibility = x.currentValue),
                props.verticalScrollbarVisibility.Subscribe(x => scrollRect.verticalScrollbarVisibility = x.currentValue),
                props.horizontalScrollbarSpacing.Subscribe(x => scrollRect.horizontalScrollbarSpacing = x.currentValue),
                props.verticalScrollbarSpacing.Subscribe(x => scrollRect.verticalScrollbarSpacing = x.currentValue),
                props.horizontal.Subscribe(x =>
                {
                    if (x.currentValue)
                    {
                        scrollRect.horizontal = true;
                        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    }
                    else
                    {
                        scrollRect.horizontal = false;
                        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                        content.transform.FillParentWidth();
                    }
                }),
                props.vertical.Subscribe(x =>
                {
                    if (x.currentValue)
                    {
                        scrollRect.vertical = true;
                        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    }
                    else
                    {
                        scrollRect.vertical = false;
                        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                        content.transform.FillParentHeight();
                    }
                })
            );

            scrollRectComponent = scrollRect;

            return control;
        }

        public static IDisposable BindScrollRect(ScrollRectProps props, UnityScrollRect scrollRect)
        {
            return new ComposedDisposable(
                props.value.Subscribe(x => scrollRect.scrollRectComponent.normalizedPosition = x.currentValue),
                props.horizontal.Subscribe(x =>
                {
                    if (x.currentValue)
                    {
                        scrollRect.scrollRectComponent.horizontal = true;
                        scrollRect.content.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    }
                    else
                    {
                        scrollRect.scrollRectComponent.horizontal = false;
                        scrollRect.content.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                        scrollRect.contentRectTransform.FillParentWidth();
                    }
                }),
                props.vertical.Subscribe(x =>
                {
                    if (x.currentValue)
                    {
                        scrollRect.scrollRectComponent.vertical = true;
                        scrollRect.content.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    }
                    else
                    {
                        scrollRect.scrollRectComponent.vertical = false;
                        scrollRect.content.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                        scrollRect.contentRectTransform.FillParentHeight();
                    }
                }),
                props.movementType.Subscribe(x => scrollRect.scrollRectComponent.movementType = x.currentValue),
                props.inertia.Subscribe(x => scrollRect.scrollRectComponent.inertia = x.currentValue),
                props.decelerationRate.Subscribe(x => scrollRect.scrollRectComponent.decelerationRate = x.currentValue),
                props.scrollSensitivity.Subscribe(x => scrollRect.scrollRectComponent.scrollSensitivity = x.currentValue),
                props.horizontalScrollbarVisibility.Subscribe(x => scrollRect.scrollRectComponent.horizontalScrollbarVisibility = x.currentValue),
                props.verticalScrollbarVisibility.Subscribe(x => scrollRect.scrollRectComponent.verticalScrollbarVisibility = x.currentValue),
                props.horizontalScrollbarSpacing.Subscribe(x => scrollRect.scrollRectComponent.horizontalScrollbarSpacing = x.currentValue),
                props.verticalScrollbarSpacing.Subscribe(x => scrollRect.scrollRectComponent.verticalScrollbarSpacing = x.currentValue),
                BindImage(props.background, scrollRect.background),
                BindScrollbar(props.horizontalScrollbar, scrollRect.horizontalScrollbar),
                BindScrollbar(props.verticalScrollbar, scrollRect.verticalScrollbar)
            );
        }

        public class DropdownProps : IDisposable
        {
            public ValueObservable<bool> selected { get; } = new ValueObservable<bool>();
            public ValueObservable<bool> interactable { get; } = new ValueObservable<bool>(true);
            public ValueObservable<int> value { get; } = new ValueObservable<int>();
            public ValueObservable<bool> allowMultiselect { get; } = new ValueObservable<bool>();
            public ListObservable<string> options { get; } = new ListObservable<string>();

            public ImageProps background { get; } = DefaultImageProps();
            public TextProps captionText { get; } = DefaultTextProps();
            public ImageProps arrow { get; } = DefaultImageProps();
            public ScrollRectProps template { get; } = DefaultScrollRectProps();
            public ImageProps itemBackground { get; } = DefaultImageProps();
            public TextProps itemText { get; } = DefaultTextProps();
            public ImageProps itemCheckmark { get; } = DefaultImageProps();

            public void Dispose()
            {
                selected.Dispose();
                interactable.Dispose();
                value.Dispose();
                allowMultiselect.Dispose();
                options.Dispose();
                background.Dispose();
                captionText.Dispose();
                arrow.Dispose();
                template.Dispose();
                itemBackground.Dispose();
                itemText.Dispose();
                itemCheckmark.Dispose();
            }
        }

        public static Control<DropdownProps> Dropdown(DropdownProps props = default)
        {
            props = props ?? DefaultDropdownProps();
            var control = new Control<DropdownProps>(props, "Dropdown", typeof(TMP_Dropdown));
            var dropdown = control.gameObject.GetComponent<TMP_Dropdown>();

            control.Children(
                Image(props.background, out var background).Name("Background").FillParent(),
                Text(props.captionText, out var captionText).Name("CaptionText").FillParent(),
                Image(props.arrow, out var arrow).Name("Arrow").AnchorToRight().SizeDelta(new Vector2(10, 10)).AnchoredPosition(new Vector2(-15f, -1.2f)),
                ScrollRect(props.template, out var template).SizeDelta(new Vector2(0, 150)).SetPivot(new Vector2(0, 1)).FillParentWidth().AnchorToBottom()
            // Control("Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).FitContentVertical(ContentSizeFitter.FitMode.PreferredSize).controlchildwi,

            );

            background = new GameObject("Background", typeof(Image)).GetComponent<Image>();
            background.rectTransform.SetParent(dropdown.transform, false);
            background.rectTransform.FillParent();

            captionText = new GameObject("Caption Text", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            captionText.transform.SetParent(dropdown.transform, false);
            captionText.rectTransform.FillParent();

            arrow = new GameObject("Arrow", typeof(Image)).GetComponent<Image>();
            arrow.rectTransform.SetParent(dropdown.transform, false);
            arrow.rectTransform.AnchorToRight();
            arrow.rectTransform.sizeDelta = new Vector2(10, 10);
            arrow.rectTransform.anchoredPosition = new Vector3(-15, -1.2f);

            template = new UnityScrollRect();
            template.scrollRectTransform.SetParent(dropdown.transform, false);
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

            dropdown.targetGraphic = background;
            dropdown.template = template.scrollRectTransform;
            dropdown.captionText = captionText;
            dropdown.itemText = itemText;

            template.scrollRectComponent.content = contentTransform;
            template.scrollRectComponent.gameObject.SetActive(false);

            dropdown.dropdownComponent.onValueChanged.AddListener(x => props.value.value = x);

            control.AddBinding(BindDropdown(props, dropdown));
            return control;
        }

        private static IDisposable BindDropdown(DropdownProps props, UnityDropdown dropdown)
        {
            return new ComposedDisposable(
                props.value.Subscribe(x => dropdown.dropdownComponent.value = x.currentValue),
                props.options.Subscribe(_ => dropdown.dropdownComponent.options = props.options.Select(x => new TMP_Dropdown.OptionData() { text = x }).ToList()),
                props.interactable.Subscribe(x => dropdown.dropdownComponent.interactable = x.currentValue),
                BindImage(props.background, dropdown.background),
                BindText(props.captionText, dropdown.captionText),
                BindImage(props.arrow, dropdown.arrow),
                BindScrollRect(props.template, dropdown.template),
                BindImage(props.itemBackground, dropdown.itemBackground),
                BindText(props.itemText, dropdown.itemText),
                BindImage(props.itemCheckmark, dropdown.itemCheckmark)
            );
        }
    }
}
