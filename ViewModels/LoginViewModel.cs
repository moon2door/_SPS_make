using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query; 
using _SPS.Models; 
using _SPS.Views;

namespace _SPS.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        [NotifyCanExecuteChangedFor(nameof(NavigateToRegisterCommand))]
        private bool isBusy;

        public bool CanExecute => !IsBusy;

        private readonly FirebaseAuthClient _authClient;
        private readonly FirebaseClient _dbClient; 

        public LoginViewModel()
        {
            var config = new FirebaseAuthConfig
            {
                ApiKey = Constants.FirebaseApiKey,
                AuthDomain = Constants.AuthDomain,
                Providers = new FirebaseAuthProvider[]
                {
                    new EmailProvider()
                }
            };
            _authClient = new FirebaseAuthClient(config);

            _dbClient = new FirebaseClient(Constants.FirebaseDatabaseUrl);
        }

        [RelayCommand(CanExecute = nameof(CanExecute))]
        private async Task Login()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Please enter your email and password.", "Confirmation");
                return;
            }

            IsBusy = true;

            try
            {
                var userCredential = await _authClient.SignInWithEmailAndPasswordAsync(Email, Password);
                var user = userCredential.User;
                var uid = user.Uid;

                string displayName = user.Info.Email;

                try
                {
                    var userInfo = await _dbClient
                        .Child("Users")
                        .Child(uid)
                        .OnceSingleAsync<UserModel>();

                    if (userInfo != null && !string.IsNullOrEmpty(userInfo.Nickname))
                    {
                        displayName = userInfo.Nickname; 
                    }
                }
                catch
                {
                    // DB에서 가져오기 실패하면 그냥 이메일 사용 
                }

                await Application.Current.MainPage.DisplayAlert("Success", $"Welcome, {displayName}!", "Start");

                await Shell.Current.GoToAsync("///MainTabs");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Login failed", "Please verify your email or password.", "Confirmation");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecute))]
        private async Task NavigateToRegister()
        {
            await Shell.Current.GoToAsync(nameof(RegisterPage));
        }
    }
}