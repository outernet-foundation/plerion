using System;
using ObserveThing;
using TMP_ContentType = TMPro.TMP_InputField.ContentType;
using FofX.Stateful;
using static Nessle.UIBuilder;

namespace Nessle
{
    public static class InputFieldExtensions
    {
        public static T Text<T>(this T control, string text)
            where T : IControl<InputFieldProps>
        {
            control.props.inputText.text.value = text;
            return control;
        }

        public static T Text<T>(this T control, IValueObservable<string> text)
            where T : IControl<InputFieldProps>
        {
            control.props.inputText.text.From(text);
            return control;
        }

        public static T OnChange<T>(this T control, Action<string> onChange)
            where T : IControl<InputFieldProps>
        {
            control.AddBinding(control.props.inputText.text.Subscribe(x => onChange(x.currentValue)));
            return control;
        }

        public static T ContentType<T>(this T control, TMP_ContentType contentType)
            where T : IControl<InputFieldProps>
        {
            control.props.contentType.value = contentType;
            return control;
        }

        public static T ContentType<T>(this T control, IValueObservable<TMP_ContentType> contentType)
            where T : IControl<InputFieldProps>
        {
            control.props.contentType.From(contentType);
            return control;
        }

        public static T ReadOnly<T>(this T control, bool readOnly)
            where T : IControl<InputFieldProps>
        {
            control.props.readOnly.value = readOnly;
            return control;
        }

        public static T ReadOnly<T>(this T control, IValueObservable<bool> readOnly)
            where T : IControl<InputFieldProps>
        {
            control.props.readOnly.From(readOnly);
            return control;
        }

        public static T Interactable<T>(this T control, bool interactable)
            where T : IControl<InputFieldProps>
        {
            control.props.interactable.value = interactable;
            return control;
        }

        public static T Interactable<T>(this T control, IValueObservable<bool> interactable)
            where T : IControl<InputFieldProps>
        {
            control.props.interactable.From(interactable);
            return control;
        }

        public static T OnEndEdit<T>(this T control, Action<string> onEndEdit)
            where T : IControl<InputFieldProps>
        {
            control.props.onEndEdit += onEndEdit;
            return control;
        }

        // public static T Selected<T>(this T control, bool selected)
        //     where T : IControl<InputFieldProps>
        // {
        //     control.props.selected.value = selected;
        //     return control;
        // }

        // public static T Selected<T>(this T control, IValueObservable<bool> selected)
        //     where T : IControl<InputFieldProps>
        // {
        //     control.props.selected.From(selected);
        //     return control;
        // }

        // public static T OnSelected<T>(this T control, Action onSelected)
        //     where T : IControl<InputFieldProps>
        // {
        //     control.AddBinding(control.props.selected.Subscribe(x =>
        //     {
        //         if (x.currentValue)
        //             onSelected();
        //     }));

        //     return control;
        // }

        // public static T OnDeselected<T>(this T control, Action onDeselected)
        //     where T : IControl<InputFieldProps>
        // {
        //     control.AddBinding(control.props.selected.Subscribe(x =>
        //     {
        //         if (!x.currentValue)
        //             onDeselected();
        //     }));

        //     return control;
        // }

        public static T BindValue<T>(this T control, ObservablePrimitive<string> bindTo)
            where T : IControl<InputFieldProps>
        {
            control.AddBinding(
                bindTo.Subscribe(x => control.props.inputText.text.value = x.currentValue),
                control.props.inputText.text.Subscribe(x => bindTo.ExecuteSetOrDelay(x.currentValue))
            );

            return control;
        }

        public static TControl BindValue<TControl, TValue>(this TControl control, ObservablePrimitive<TValue> bindTo, Func<string, TValue> toState, Func<TValue, string> toControl)
            where TControl : IControl<InputFieldProps>
        {
            control.AddBinding(
                bindTo.Subscribe(x => control.props.inputText.text.value = toControl(x.currentValue)),
                control.props.inputText.text.Subscribe(x => bindTo.ExecuteSetOrDelay(toState(x.currentValue)))
            );

            return control;
        }
    }
}
