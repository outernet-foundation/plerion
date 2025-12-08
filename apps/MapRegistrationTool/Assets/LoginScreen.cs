using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;

namespace Outernet.MapRegistrationTool
{
    public class LoginScreen : MonoBehaviour
    {
        public TMP_InputField username;
        public TMP_InputField password;
        public TextMeshProUGUI errorText;
        public Button loginButton;

        private void Awake()
        {
            loginButton.onClick.AddListener(() => Login(App.plerionAuthUrl, App.plerionAuthClient, username.text, password.text).Forget());

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

        private async UniTask Login(string authUrl, string authClient, string username, string password)
        {
            try
            {
                await Tasks.Login(authUrl, authClient, username, password);
            }
            catch (Exception exc)
            {
                await UniTask.SwitchToMainThread();
                errorText.text = exc.Message;
                errorText.gameObject.SetActive(true);
                return;
            }
        }
    }
}