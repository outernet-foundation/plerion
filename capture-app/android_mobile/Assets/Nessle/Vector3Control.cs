using System;
using ObserveThing;
using TMPro;
using UnityEngine;
using static Nessle.UIBuilder;

namespace Nessle
{
    // public class Vector3Props
    // {
    //     public ValueObservable<Vector3> value { get; } = new ValueObservable<Vector3>();
    //     public LayoutProps layout { get; } = new LayoutProps();

    //     public TextProps xLabel { get; } = new TextProps();
    //     public TextProps yLabel { get; } = new TextProps();
    //     public TextProps zLabel { get; } = new TextProps();

    //     // public FloatFieldProps xField { get; } = new FloatFieldProps();
    //     // public FloatFieldProps yField { get; } = new FloatFieldProps();
    //     // public FloatFieldProps zField { get; } = new FloatFieldProps();
    // }

    // public static Control<Vector3Props> Vector3(Vector3Props props = default)
    // {
    //     props = props ?? new Vector3Props();
    //     return new Control<Vector3Props>(props, "Vector3").Children(
    //         HorizontalLayout(props.layout).Elements(
    //             HorizontalLayout().ChildControlHeight(true).Spacing(10).ChildForceExpandHeight(true).Elements(
    //                 Text(props.xLabel).Value("x").Alignment(TextAlignmentOptions.MidlineGeoAligned),
    //                 InputField(props.xField)
    //                     .Text(props.value.SelectDynamic(x => x.x.ToString()))
    //                     .ContentType(TMP_InputField.ContentType.DecimalNumber)
    //                     .FlexibleWidth(true)
    //                     .OnChange(x => props.value.value = new Vector3(float.TryParse(x, out var v) ? v : 0, props.value.value.y, props.value.value.z))
    //             ),
    //             HorizontalLayout().ChildControlHeight(true).Spacing(10).ChildForceExpandHeight(true).Elements(
    //                 Text(props.yLabel).Value("y").Alignment(TextAlignmentOptions.MidlineGeoAligned),
    //                 InputField(props.yField)
    //                     .Text(props.value.SelectDynamic(x => x.y.ToString()))
    //                     .ContentType(TMP_InputField.ContentType.DecimalNumber)
    //                     .FlexibleWidth(true)
    //                     .OnChange(x => props.value.value = new Vector3(float.TryParse(x, out var v) ? v : 0, props.value.value.y, props.value.value.z))
    //             ),
    //             HorizontalLayout().ChildControlHeight(true).Spacing(10).ChildForceExpandHeight(true).Elements(
    //                 Text(props.zLabel).Value("z").Alignment(TextAlignmentOptions.MidlineGeoAligned),
    //                 FloatField(props.zField)
    //                     .Text(props.value.SelectDynamic(x => x.z.ToString()))
    //                     .FlexibleWidth(true)
    //                     .OnChange((float z) => props.value.value = new Vector3(props.value.value.x, props.value.value.y, z))
    //             )
    //         )
    //     );
    // }

    public static class Vector3Extensions
    {
        // public static IControl<T> OnChange<T>(this IControl<T> control, Action<float> onChange)
        //     where T : UIBuilder.FloatFieldProps
        // {
        //     return control;
        // }
    }
}