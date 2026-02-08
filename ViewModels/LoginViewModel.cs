using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query; // 쿼리 확장 메서드 사용
using _SPS.Models;
using Microsoft.Maui.Storage; // Preferences 사용

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
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };
            _authClient = new FirebaseAuthClient(config);
            _dbClient = new FirebaseClient(Constants.FirebaseDatabaseUrl);
        }

        [RelayCommand(CanExecute = nameof(CanExecute))]
        private async Task Login()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Please enter email and password.", "OK");
                return;
            }

            IsBusy = true;

            try
            {
                // 1. Firebase Auth 로그인
                var userCredential = await _authClient.SignInWithEmailAndPasswordAsync(Email, Password);
                var uid = userCredential.User.Uid;

                // 2. Realtime Database에서 사용자 정보(UserType 등) 가져오기
                var userModel = await _dbClient
                    .Child("Users")
                    .Child(uid)
                    .OnceSingleAsync<UserModel>();

                if (userModel != null)
                {
                    // 3. 앱 내부에 사용자 정보 저장 (세션 유지)
                    Preferences.Set("UserUid", userModel.Uid);
                    Preferences.Set("UserType", userModel.UserType.ToString());
                    Preferences.Set("UserNickname", userModel.Nickname);

                    // 4. 메인 화면으로 이동
                    await Shell.Current.GoToAsync("//MainPage");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Error", "User data not found.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Login Failed", $"Error: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task GoToRegister()
        {
            await Shell.Current.GoToAsync("RegisterPage");
        }
    }
}