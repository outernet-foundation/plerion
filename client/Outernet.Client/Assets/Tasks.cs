using Cysharp.Threading.Tasks;
using Plerion.Core;
using UnityEngine;

namespace Outernet.Client.AuthoringTools
{
    public static class Tasks
    {
        public static async UniTask Login(string apiUrl, string username, string password)
        {
            Auth.tokenUrl = $"{apiUrl}/auth/realms/plerion-dev/protocol/openid-connect/token";
            Auth.username = username;
            Auth.password = password;

            await UniTask.WhenAll(
                Auth.Login(),
                VisualPositioningSystem.Login(username, password)
            );

            await UniTask.SwitchToMainThread();
            App.state.loggedIn.ExecuteSet(true);
        }
    }
}
