using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nessle
{
    public class InputFieldControl : UnityComponentControl<TMP_InputField>
    {
        public UnityComponentControl<RectTransform> textViewport { get; private set; }
        public UnityComponentControl<TextMeshProUGUI> inputText { get; private set; }
        public UnityComponentControl<TextMeshProUGUI> placeholderText { get; private set; }
        public UnityComponentControl<Image> background { get; private set; }

        public InputFieldControl(
            UnityComponentControl<RectTransform> textViewport = default,
            UnityComponentControl<TextMeshProUGUI> inputText = default,
            UnityComponentControl<TextMeshProUGUI> placeholderText = default
        ) : base(UIBuilder.GameObject<TMP_InputField>())
        {
            background = new UnityComponentControl<Image>(gameObject.AddComponent<Image>());

            this.PreferredHeight(60)
                .Children(
                    background.Color(new Color(0.1333333f, 0.1333333f, 0.1333333f)),
                    this.textViewport = textViewport ?? UIBuilder.RectTransform().FillParent().Children(
                        this.placeholderText = placeholderText ?? UIBuilder.Label().FillParent().Text("<i>Enter text...").Alpha(0.333f).Alignment(TextAlignmentOptions.CaplineLeft).Margin(new Vector4(10, 0, 0, 0)),
                        this.inputText = inputText ?? UIBuilder.Label().FillParent().Alignment(TextAlignmentOptions.CaplineLeft).Margin(new Vector4(10, 0, 0, 0))
                    )
                );

            component.textViewport = textViewport.component;
            component.textComponent = inputText.component;
            component.placeholder = placeholderText.component;
            component.targetGraphic = background.component;

            // Caret and highlight won't function unless we toggle this
            component.enabled = false;
            component.enabled = true;
        }
    }
}
