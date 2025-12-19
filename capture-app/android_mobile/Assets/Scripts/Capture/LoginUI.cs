using UnityEngine;
using Nessle;
using TMPro;

using static Nessle.UIBuilder;
using ObserveThing;
using UnityEngine.UI;
using FofX.Stateful;
using ObserveThing.StatefulExtensions;

namespace PlerionClient.Client
{
    public static partial class UIElements
    {
        public static IControl LoginUI()
        {
            return Control(new GameObject("Login UI")).Children(
                Image(new ImageProps() { color = Props.From(elements.midgroundColor) })
                    .FillParent()
                    .Children(
                        TightRowsWideColumns()
                            .Anchor(new Vector2(0.5f, 0.66f))
                            .SetPivot(new Vector2(0.5f, 1f))
                            .AnchoredPosition(Vector2.zero)
                            .SizeDelta(new Vector2(900, 0))
                            .FitContentVertical(ContentSizeFitter.FitMode.PreferredSize)
                            .Children(
                                LabeledControl(new LabeledControlProps()
                                {
                                    label = new TextProps() { value = Props.From("Username") },
                                    labelWidth = Props.From(225f),
                                    control = InputField(new InputFieldProps()
                                    {
                                        value = App.state.username.AsObservable(),
                                        onValueChanged = x => App.state.username.ExecuteSetOrDelay(x)
                                    })
                                }),
                                LabeledControl(new LabeledControlProps()
                                {
                                    label = new TextProps() { value = Props.From("Password") },
                                    labelWidth = Props.From(225f),
                                    control = InputField(new InputFieldProps()
                                    {
                                        value = App.state.password.AsObservable(),
                                        contentType = Props.From(TMP_InputField.ContentType.Password),
                                        onValueChanged = x => App.state.password.ExecuteSetOrDelay(x)
                                    })
                                }),
                                HorizontalLayout(new LayoutProps()
                                {
                                    childControlWidth = Props.From(true),
                                    childControlHeight = Props.From(true),
                                    childAlignment = Props.From(TextAnchor.UpperRight)
                                }).Children(
                                        LabeledButton(new LabeledButtonProps()
                                        {
                                            label = Props.From("Log In"),
                                            onClick = () => App.state.loginRequested.ExecuteSetOrDelay(true)
                                        })
                                    ),
                                Text(new TextProps()
                                {
                                    value = App.state.authError.AsObservable(),
                                    style = new TextStyleProps()
                                    {
                                        color = Props.From(Color.red),
                                        horizontalAlignment = Props.From(HorizontalAlignmentOptions.Center)
                                    }
                                })
                                .Active(App.state.authError.AsObservable().SelectDynamic(x => !string.IsNullOrEmpty(x)))
                            )
                    )
            );
        }
    }
}