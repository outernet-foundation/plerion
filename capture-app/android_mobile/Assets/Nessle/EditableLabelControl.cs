using TMPro;

namespace Nessle
{
    public class EditableLabelControl : UnityValueControl<string>
    {
        public InputFieldControl inputField { get; }
        public UnityComponentControl<PointerClickHandler> pointerClickHandler { get; }
        public UnityComponentControl<TextMeshProUGUI> label { get; }

        public EditableLabelControl() : base(nameof(EditableLabelControl))
        {
            this.Children(
                pointerClickHandler = new UnityComponentControl<PointerClickHandler>(gameObject.AddComponent<PointerClickHandler>()),
                label = UIBuilder.Label().FillParent(),
                inputField = UIBuilder.InputField().FillParent().BindInput(this).Active(false)
            );

            pointerClickHandler.component.onClick += x =>
            {
                if (x.clickCount > 1)
                {
                    label.gameObject.SetActive(false);
                    inputField.gameObject.SetActive(true);
                    inputField.component.Select();
                }
            };

            inputField.component.onEndEdit.AddListener(x =>
            {
                inputField.gameObject.SetActive(false);
                label.gameObject.SetActive(true);
                value = x;
            });
        }

        protected override void HandleValueChanged()
        {
            inputField.component.text = value;
            label.component.text = value;
        }
    }
}