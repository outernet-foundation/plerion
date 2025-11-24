using FofX.Stateful;

namespace PlerionClient.Client
{
    public class LogInAction : ObservableNodeAction<AppState>
    {
        private string _username;
        private string _password;

        public LogInAction(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public override void Execute(AppState target)
        {
            target.username.value = _username;
            target.password.value = _password;
            target.loginRequested.value = true;
        }
    }

    public class LogOutAction : ObservableNodeAction<AppState>
    {
        public override void Execute(AppState target)
        {
            target.username.Reset();
            target.password.Reset();
            target.vpsAuthComplete.value = false;
            target.apiAuthComplete.value = false;
            target.loginRequested.value = false;
        }
    }
}