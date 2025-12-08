using Cysharp.Threading.Tasks;
using Plerion.Core;
using UnityEngine;

namespace Outernet.MapRegistrationTool
{
    public static class Tasks
    {
        public static async UniTask Login(string authUrl, string authClient, string username, string password)
        {
            Auth.Initialize(
                authUrl,
                authClient,
                username,
                password,
                message => Debug.Log(message),
                message => Debug.LogWarning(message),
                message => Debug.LogError(message));

            await Auth.Login();
            await UniTask.SwitchToMainThread();
            App.state.loggedIn.ExecuteSet(true);
        }
    }
}