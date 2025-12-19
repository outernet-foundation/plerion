using Nessle;
using ObserveThing;
using UnityEngine.Events;

namespace PlerionClient.Client
{
    public struct FoldoutProps
    {
        public TextProps label;
        public IValueObservable<bool> interactable;
        public IValueObservable<bool> isOpen;
        public LayoutProps layout;
        public UnityAction<bool> isOpenChanged;
    }

    public class FoldoutControl : PrimitiveControl<FoldoutProps>
    {
        public PrimitiveControl<ToggleProps> toggle;
        public PrimitiveControl<TextProps> label;
        public PrimitiveControl<LayoutProps> layout;

        protected override void SetupInternal()
        {
            label.Setup(props.label);
            layout.Setup(props.layout);
            toggle.Setup(new ToggleProps()
            {
                value = props.isOpen,
                interactable = props.interactable,
                onValueChanged = x =>
                {
                    layout.gameObject.SetActive(x);
                    props.isOpenChanged?.Invoke(x);
                }
            });
        }
    }
}