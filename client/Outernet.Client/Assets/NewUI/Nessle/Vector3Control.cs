using ObserveThing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nessle
{
    public class Vector3Control : UnityValueControl<Vector3>
    {
        public UnityComponentControl<HorizontalLayoutGroup> layout { get; }

        public UnityComponentControl<TextMeshProUGUI> xLabel { get; }
        public InputFieldControl xField { get; }

        public UnityComponentControl<TextMeshProUGUI> yLabel { get; }
        public InputFieldControl yField { get; }

        public UnityComponentControl<TextMeshProUGUI> zLabel { get; }
        public InputFieldControl zField { get; }

        public Vector3Control(
            UnityComponentControl<HorizontalLayoutGroup> layout = default,
            UnityComponentControl<TextMeshProUGUI> xLabel = default, InputFieldControl xField = default,
            UnityComponentControl<TextMeshProUGUI> yLabel = default, InputFieldControl yField = default,
            UnityComponentControl<TextMeshProUGUI> zLabel = default, InputFieldControl zField = default
        ) : base(layout?.gameObject ?? null)
        {
            this.Children(
                this.layout = layout ?? new UnityComponentControl<HorizontalLayoutGroup>(gameObject).Spacing(10).Children(
                    UIBuilder.HorizontalLayout().ControlChildSize(true).Spacing(10).ChildForceExpandHeight(true).Children(
                        this.xLabel = xLabel ?? UIBuilder.Label().Text("x").Alignment(TextAlignmentOptions.MidlineGeoAligned),
                        this.xField = (xField ?? UIBuilder.InputField().ContentType(TMP_InputField.ContentType.DecimalNumber).InputText(0).FlexibleWidth(true))
                            .OnEndEdit(x => value = new Vector3(float.TryParse(x, out var v) ? v : 0, value.z))
                    ),
                    UIBuilder.HorizontalLayout().ControlChildSize(true).Spacing(10).ChildForceExpandHeight(true).Children(
                        this.yLabel = yLabel ?? UIBuilder.Label().Text("y").Alignment(TextAlignmentOptions.MidlineGeoAligned),
                        this.yField = (yField ?? UIBuilder.InputField().ContentType(TMP_InputField.ContentType.DecimalNumber).InputText(0).FlexibleWidth(true))
                            .OnEndEdit(x => value = new Vector3(value.x, float.TryParse(x, out var v) ? v : 0, value.z))
                    ),
                    UIBuilder.HorizontalLayout().ControlChildSize(true).Spacing(10).ChildForceExpandHeight(true).Children(
                        this.zLabel = zLabel ?? UIBuilder.Label().Text("z").Alignment(TextAlignmentOptions.MidlineGeoAligned),
                        this.zField = (zField ?? UIBuilder.InputField().ContentType(TMP_InputField.ContentType.DecimalNumber).InputText(0).FlexibleWidth(true))
                            .OnEndEdit(x => value = new Vector3(value.x, value.y, float.TryParse(x, out var v) ? v : 0))
                    )
                )
            );
        }

        protected override void HandleValueChanged()
        {
            xField.component.text = value.x.ToString();
            yField.component.text = value.y.ToString();
            zField.component.text = value.z.ToString();
        }
    }

    public static class Vector3ControlExtensions
    {
        public static T InputValue<T>(this T control, Vector3 value)
            where T : Vector3Control
        {
            control.value = value;
            return control;
        }

        public static T InputValue<T>(this T control, IValueObservable<Vector3> value)
            where T : Vector3Control
        {
            control.AddBinding(value.Subscribe(x => control.value = x.currentValue));
            return control;
        }
    }
}
