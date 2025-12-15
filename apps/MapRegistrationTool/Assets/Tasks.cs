using Cysharp.Threading.Tasks;
using Plerion.VPS;
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
                message => Debug.Log(message),
                message => Debug.LogWarning(message),
                message => Debug.LogError(message)
            );

            await Auth.Login(username, password);
            await UniTask.SwitchToMainThread();
            App.state.loggedIn.ExecuteSet(true);
        }
    }
}
