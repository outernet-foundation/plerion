using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Plerion.VPS;

namespace Outernet.Client.AuthoringTools
{
    public class LoginScreen : MonoBehaviour
    {
        public TMP_InputField username;
        public TMP_InputField password;
        public Button loginButton;

        private void Awake()
        {
            loginButton.onClick.AddListener(() =>
            {
                VisualPositioningSystem.Initialize(username.text, password.text);
                Auth.username = username.text;
                Auth.password = password.text;
                App.state.loggedIn.ExecuteSet(true);
            });

            App.state.loggedIn.OnChange(x =>
            {
                if (x)
                {
                    gameObject.SetActive(false);
                    return;
                }

                gameObject.SetActive(true);
                username.text = null;
                password.text = null;
            });
        }
    }
}