using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FofX;
using FofX.Stateful;
using Plerion.VPS;
using UnityEngine;

namespace PlerionClient.Client
{
    public class AuthManager : MonoBehaviour
    {
        private TaskHandle _loginTask = TaskHandle.Complete;

        private void Awake()
        {
            App.RegisterObserver(HandleLoginRequestedChanged, App.state.loginRequested);
        }

        private void OnDestroy()
        {
            App.DeregisterObserver(HandleLoginRequestedChanged);
            _loginTask.Cancel();
        }

        private void HandleLoginRequestedChanged(NodeChangeEventArgs args)
        {
            _loginTask.Cancel();

            if (!App.state.loginRequested.value)
                return;

            _loginTask = TaskHandle.Execute(token =>
                LogIn(
                    App.state.plerionApiUrl.value,
                    App.state.plerionAuthUrl.value,
                    App.state.plerionAuthClient.value,
                    App.state.username.value,
                    App.state.password.value,
                    token
                )
            );
        }

        private async UniTask LogIn(
            string apiUrl,
            string authUrl,
            string authClient,
            string username,
            string password,
            CancellationToken cancellationToken = default
        )
        {
            App.ExecuteActionOrDelay(new SetAuthStatusAction(AuthStatus.LoggingIn));

            try
            {
                await VisualPositioningSystem.Initialize(
                    apiUrl,
                    authUrl,
                    authClient,
                    username,
                    password,
                    message => Log.Info(LogGroup.Localizer, message),
                    message => Log.Warn(LogGroup.Localizer, message),
                    message => Log.Error(LogGroup.Localizer, message),
                    (message, exception) => Log.Error(LogGroup.Localizer, exception, message)
                );
            }
            catch (Exception exc)
            {
                App.ExecuteActionOrDelay(new SetAuthStatusAction(AuthStatus.Error, exc.Message));
                throw exc;
            }

            await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);
            App.state.authStatus.ExecuteSetOrDelay(AuthStatus.LoggedIn);
        }
    }
}
