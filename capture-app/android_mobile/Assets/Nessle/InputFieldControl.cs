using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nessle
{
    public class InputFieldControl : UnityValueControl<string>, IUnityComponentControl<TMP_InputField>
    {
        public TMP_InputField component { get; }

        public UnityComponentControl<RectTransform> textViewport { get; private set; }
        public UnityComponentControl<TextMeshProUGUI> inputText { get; private set; }
        public UnityComponentControl<TextMeshProUGUI> placeholderText { get; private set; }
        public UnityComponentControl<Image> background { get; private set; }

        public InputFieldControl(
            UnityComponentControl<RectTransform> textViewport = default,
            UnityComponentControl<TextMeshProUGUI> inputText = default,
            UnityComponentControl<TextMeshProUGUI> placeholderText = default
        ) : base(new GameObject(nameof(InputFieldControl), typeof(TMP_InputField)))
        {
            component = gameObject.GetComponent<TMP_InputField>();

            this.PreferredHeight(60)
                .Children(
                    background = gameObject.ComponentControl<Image>().Color(Color.black),
                    this.textViewport = textViewport ?? UIBuilder.RectTransform().FillParent().Children(
                        this.placeholderText = placeholderText ?? UIBuilder.Label().FillParent().Text("<i>Enter text...").Alpha(0.333f).Alignment(TextAlignmentOptions.CaplineLeft).Margin(new Vector4(10, 0, 0, 0)),
                        this.inputText = inputText ?? UIBuilder.Label().FillParent().Alignment(TextAlignmentOptions.CaplineLeft).Margin(new Vector4(10, 0, 0, 0))
                    )
                );

            component.textViewport = this.textViewport.component;
            component.textComponent = this.inputText.component;
            component.placeholder = this.placeholderText.component;
            component.targetGraphic = this.background.component;

            // Caret and highlight won't function unless we toggle this
            component.enabled = false;
            component.enabled = true;

            component.text = value;
            component.onEndEdit.AddListener(x => value = x);
        }

        protected override void HandleValueChanged()
        {
            component.text = value;
        }
    }
}
