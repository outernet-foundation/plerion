using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nessle;
using ObserveThing;
using UnityEngine;

using TMPro;

using static Nessle.UIBuilder;

namespace PlerionClient.Client
{
    public static partial class UIElements
    {
        public struct ObjectInspectorProps
        {
            public object target;
            public FoldoutProps foldout;
            public IValueObservable<bool> isReadonly;
        }

        public static IControl ObjectInspector(ObjectInspectorProps props = default)
        {
            props.foldout.layout.childControlHeight = props.foldout.layout.childControlHeight ?? Props.From(true);
            props.foldout.layout.childControlWidth = props.foldout.layout.childControlWidth ?? Props.From(true);
            props.foldout.layout.childForceExpandHeight = props.foldout.layout.childForceExpandHeight ?? Props.From(false);
            props.foldout.layout.childForceExpandWidth = props.foldout.layout.childForceExpandWidth ?? Props.From(false);
            props.foldout.layout.spacing = props.foldout.layout.spacing ?? Props.From(10f);
            props.foldout.layout.padding = props.foldout.layout.padding ?? Props.From(new RectOffset(50, 0, 0, 0));

            return Foldout(props.foldout).Children(ObjectFieldInspectors(props.target, props.isReadonly.SelectDynamic(x => !x)));
        }

        public static IEnumerable<IControl> ObjectFieldInspectors(object target, IValueObservable<bool> interactable = default)
        {
            return target.GetType()
                .GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x is FieldInfo || x is PropertyInfo)
                .Select(memberInfo =>
                {
                    string name = default;
                    Type type = default;
                    Func<object, object> getter = default;
                    Action<object, object> setter = default;

                    if (memberInfo is FieldInfo fieldInfo)
                    {
                        name = fieldInfo.Name;
                        type = fieldInfo.FieldType;
                        getter = fieldInfo.GetValue;
                        setter = fieldInfo.SetValue;
                    }
                    else
                    {
                        var propertyInfo = memberInfo as PropertyInfo;
                        name = propertyInfo.Name;
                        type = propertyInfo.PropertyType;
                        getter = propertyInfo.GetValue;
                        setter = propertyInfo.SetValue;
                    }

                    if (type.IsEnum || type.IsValueType || type == typeof(string))
                    {
                        return PrimitiveInspector(
                           Props.From(name),
                           type,
                           Props.From(getter(target)),
                           x => setter(target, x),
                           interactable
                        );
                    }
                    else
                    {
                        Log.Warn(LogGroup.Default, $"Unhandled field or property type {type.Name}");
                        return Text(new TextProps()
                        {
                            value = Props.From($"Unhandled Field: {name}"),
                            style = new TextStyleProps() { color = Props.From(Color.yellow) }
                        });
                    }
                    //    else if (type.IsArray || type is IList)
                    //    {
                    //        var objList = getter(target);

                    //        if (objList == null)
                    //        {
                    //            objList = Activator.CreateInstance(type);
                    //            setter(target, objList);
                    //        }

                    //        return ListInspector(Props.From(name), objList as IList);
                    //    }
                    //    else
                    //    {
                    //        var obj = getter(target);

                    //        if (obj == null)
                    //        {
                    //            obj = Activator.CreateInstance(type);
                    //            setter(target, obj);
                    //        }

                    //        return ObjectInspector(Props.From(name), obj);
                    //    }
                });
        }

        // private static IControl ListInspector(IValueObservable<string> label, IList list)
        // {
        //     var elementType = list.GetType().GetElementType();
        //     IControl content = VerticalLayout(new LayoutProps()
        //     {
        //         spacing = Props.From(10f),
        //         childControlHeight = Props.From(true),
        //         childControlWidth = Props.From(true),
        //         childForceExpandWidth = Props.From(true)
        //     });

        //     Func<object, int, IControl> constructElementControl = (obj, index) =>
        //     {
        //         IControl subControl = default;
        //         IValueObservable<int> indexObs = content.children.FollowIndexDynamic(index);
        //         int currIndex = -1;

        //         if (elementType.IsArray || elementType is IList)
        //         {
        //             subControl = ListInspector(indexObs.SelectDynamic(x => x.ToString()), obj as IList);
        //         }
        //         else if (elementType.IsEnum || elementType.IsValueType || elementType == typeof(string))
        //         {
        //             var primControl = PrimitiveInspector(
        //                 indexObs.SelectDynamic(x => x.ToString()),
        //                 elementType,
        //                 Props.From(obj),
        //                 x => list[currIndex] = x
        //             );

        //             subControl = primControl;
        //         }
        //         else
        //         {
        //             subControl = ObjectInspector(indexObs.SelectDynamic(x => x.ToString()), obj);
        //         }

        //         return HorizontalLayout(new LayoutProps()
        //         {
        //             childControlWidth = Props.From(true),
        //             childControlHeight = Props.From(true)
        //         })
        //         .Children(
        //             subControl,
        //             LabeledButton(new LabeledButtonProps()
        //             {
        //                 label = Props.From("Remove"),
        //                 onClick = () =>
        //                 {
        //                     list.RemoveAt(currIndex);
        //                     var self = content.children[currIndex];
        //                     content.children.RemoveAt(currIndex);
        //                     self.Dispose();
        //                 }
        //             })
        //         );
        //     };

        //     for (int i = 0; i < list.Count; i++)
        //         content.children.Add(constructElementControl(list[i], i));

        //     return Foldout(new FoldoutProps()
        //     {
        //         label = new TextProps() { value = label },
        //         layout = new LayoutProps()
        //         {
        //             childControlHeight = Props.From(true),
        //             childControlWidth = Props.From(true),
        //             childForceExpandHeight = Props.From(false),
        //             childForceExpandWidth = Props.From(false),
        //             padding = Props.From(new RectOffset(70, 0, 0, 0)),
        //             spacing = Props.From(10f)
        //         }
        //     })
        //     .Children(
        //         content,
        //         LabeledButton(new LabeledButtonProps()
        //         {
        //             label = Props.From("Add"),
        //             onClick = () =>
        //             {
        //                 var element = elementType.IsValueType ? Activator.CreateInstance(elementType) : null;
        //                 list.Add(element);
        //                 content.children.Add(constructElementControl(element, list.Count - 1));
        //             }
        //         })
        //     );
        // }

        public static IControl PrimitiveInspector(IValueObservable<string> label, Type type, IValueObservable<object> value, Action<object> onValueChanged, IValueObservable<bool> interactable = default)
        {
            var nullableUnderlyingType = Nullable.GetUnderlyingType(type);
            if (nullableUnderlyingType != null)
            {
                var initValue = value.Peek();
                var selectedValue = new ValueObservable<object>(initValue ?? Activator.CreateInstance(nullableUnderlyingType));

                var isNull = new ValueObservable<bool>(initValue == null);
                var subControl = PrimitiveControl(nullableUnderlyingType, selectedValue, x => selectedValue.From(x), interactable);

                var control = HorizontalLayout(new LayoutProps()
                {
                    childControlWidth = Props.From(true),
                    childControlHeight = Props.From(true),
                    spacing = Props.From(30f)
                }).Children(
                    Toggle(new ToggleProps()
                    {
                        value = isNull.SelectDynamic(x => !x),
                        onValueChanged = x => isNull.From(!x),
                        interactable = interactable
                    }),
                    VerticalLayout(new LayoutProps()
                    {
                        childControlWidth = Props.From(true),
                        childControlHeight = Props.From(true),
                        spacing = Props.From(10f)
                    }).Children(
                        Text(new TextProps()
                        {
                            value = label,
                            style = new TextStyleProps()
                            {
                                verticalAlignment = Props.From(VerticalAlignmentOptions.Capline),
                                overflowMode = Props.From(TextOverflowModes.Ellipsis),
                                textWrappingMode = Props.From(TextWrappingModes.NoWrap)
                            }
                        }).FlexibleWidth(true),
                        subControl.Active(isNull.SelectDynamic(x => !x)).FlexibleWidth(true)
                    )
                );

                control.AddBinding(
                    value.Subscribe(x =>
                    {
                        isNull.From(x.currentValue == null);

                        if (x.currentValue != null)
                            selectedValue.From(x.currentValue);
                    }),
                    Observables.Combine(
                        selectedValue,
                        isNull,
                        (selected, isNull) => isNull ? null : selected
                    ).Subscribe(x => onValueChanged?.Invoke(ConstructNullable(type, x.currentValue)))
                );

                return control;
            }

            return VerticalLayout().Children(
                Text(new TextProps()
                {
                    value = label,
                    style = new TextStyleProps()
                    {
                        verticalAlignment = Props.From(VerticalAlignmentOptions.Capline),
                        overflowMode = Props.From(TextOverflowModes.Ellipsis),
                        textWrappingMode = Props.From(TextWrappingModes.NoWrap)
                    }
                }).FlexibleWidth(true),
                PrimitiveControl(type, value, onValueChanged).FlexibleWidth(true)
            );
        }

        private static object ConstructNullable(Type nullableType, object value)
        {
            return value == null ?
                Activator.CreateInstance(nullableType) :
                Activator.CreateInstance(nullableType, value);
        }

        public static IControl PrimitiveControl(Type type, IValueObservable<object> value, Action<object> onValueChanged, IValueObservable<bool> interactable = default)
        {
            if (type.IsEnum)
            {
                return Dropdown(new DropdownProps()
                {
                    options = Props.From(Enum.GetNames(type)),
                    value = value.SelectDynamic(x => x == null ? default : Convert.ToInt32(x)),
                    onValueChanged = x => onValueChanged?.Invoke(Enum.ToObject(type, x)),
                    interactable = interactable
                });
            }
            else if (type == typeof(bool))
            {
                return Toggle(new ToggleProps()
                {
                    value = value.SelectDynamic(x => x == null ? default : (bool)x),
                    onValueChanged = x => onValueChanged?.Invoke(x),
                    interactable = interactable
                });
            }
            else if (type == typeof(int))
            {
                return IntField(new InputFieldProps<int>()
                {
                    value = value.SelectDynamic(x => x == null ? default : (int)x),
                    onValueChanged = x => onValueChanged?.Invoke(x),
                    interactable = interactable
                });
            }
            else if (type == typeof(float))
            {
                return FloatField(new InputFieldProps<float>()
                {
                    value = value.SelectDynamic(x => x == null ? default : (float)x),
                    onValueChanged = x => onValueChanged?.Invoke(x),
                    interactable = interactable
                });
            }
            else if (type == typeof(double))
            {
                return DoubleField(new InputFieldProps<double>()
                {
                    value = value.SelectDynamic(x => x == null ? default : (double)x),
                    onValueChanged = x => onValueChanged?.Invoke(x),
                    interactable = interactable
                });
            }
            else if (type == typeof(string))
            {
                return InputField(new InputFieldProps()
                {
                    value = value.SelectDynamic(x => x == null ? default : (string)x),
                    onValueChanged = x => onValueChanged?.Invoke(x),
                    interactable = interactable
                });
            }
            else
            {
                Log.Warn(LogGroup.Default, $"Unhandled primitive type {type.Name}");
                return Text(new TextProps()
                {
                    value = value.SelectDynamic(x => x == null ? "NULL" : x.ToString()),
                    style = new TextStyleProps()
                    {
                        color = Props.From(Color.yellow),
                        textWrappingMode = Props.From(TextWrappingModes.NoWrap),
                        overflowMode = Props.From(TextOverflowModes.Ellipsis),
                        verticalAlignment = Props.From(VerticalAlignmentOptions.Capline)
                    }
                });
            }
        }
    }
}