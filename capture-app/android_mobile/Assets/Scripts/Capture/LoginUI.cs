using UnityEngine;
using Nessle;
using TMPro;

using static Nessle.UIBuilder;
using ObserveThing;
using UnityEngine.UI;

namespace PlerionClient.Client
{
    public static partial class UIElements
    {
        public class LoginScreenProps
        {
            public ValueObservable<string> username { get; } = new ValueObservable<string>();
            public ValueObservable<string> password { get; } = new ValueObservable<string>();
            public ValueObservable<string> error { get; } = new ValueObservable<string>();
        }

        public static IControl<LoginScreenProps> LoginUI()
        {
            return Control("loginUI", new LoginScreenProps()).Setup(loginUI =>
            {
                loginUI.Children(
                    Image().Setup(background =>
                    {
                        background.props.color.From(elements.midgroundColor);
                        background.FillParent();
                        background.Children(
                            TightRowsWideColumns("login").Setup(content =>
                            {
                                content.Anchor(new Vector2(0.5f, 0.66f));
                                content.SetPivot(new Vector2(0.5f, 1f));
                                content.AnchoredPosition(Vector2.zero);
                                content.SizeDelta(new Vector2(900, 0));
                                content.FitContentVertical(ContentSizeFitter.FitMode.PreferredSize);
                                content.Children(
                                    LabeledControl("Username", 225, InputField().Setup(username => username.BindValue(x => x.inputText.text, loginUI.props.username))),
                                    LabeledControl("Password", 225, InputField().Setup(password =>
                                    {
                                        password.props.contentType.From(TMP_InputField.ContentType.Password);
                                        password.BindValue(x => x.inputText.text, loginUI.props.password);
                                    })),
                                    HorizontalLayout().Setup(loginBar =>
                                    {
                                        loginBar.props.childControlWidth.From(true);
                                        loginBar.props.childControlHeight.From(true);
                                        loginBar.props.childAlignment.From(TextAnchor.UpperRight);
                                        loginBar.Children(
                                            Button().Setup(loginButton =>
                                            {
                                                loginButton.LabelFrom("Log In");
                                                loginButton.props.onClick.From(() =>
                                                    App.ExecuteAction(new LogInAction(loginUI.props.username.value, loginUI.props.password.value))
                                                );
                                            })
                                        );
                                    }),
                                    Text().Setup(errorText =>
                                    {
                                        errorText.props.style.color.From(Color.red);
                                        errorText.props.text.From(loginUI.props.error);
                                        errorText.props.style.horizontalAlignment.From(HorizontalAlignmentOptions.Center);
                                        errorText.Active(loginUI.props.error.SelectDynamic(x => x != null));
                                    })
                                );
                            })
                        );
                    })
                );
            });
        }
    }
}