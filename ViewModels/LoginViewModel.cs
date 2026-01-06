using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database; // DB 사용 추가
using Firebase.Database.Query; // 쿼리 사용 추가
using _SPS.Models; // 유저 모델 사용 추가
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
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
        private readonly FirebaseClient _dbClient; // ★ DB 클라이언트 추가

        public LoginViewModel()
        {
            // 인증 초기화
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

            // ★ DB 초기화
            _dbClient = new FirebaseClient(Constants.FirebaseDatabaseUrl);
        }

        [RelayCommand(CanExecute = nameof(CanExecute))]
        private async Task Login()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Application.Current.MainPage.DisplayAlert("오류", "이메일과 비밀번호를 입력해주세요.", "확인");
                return;
            }

            IsBusy = true;

            try
            {
                // 1. 로그인 시도
                var userCredential = await _authClient.SignInWithEmailAndPasswordAsync(Email, Password);
                var user = userCredential.User;
                var uid = user.Uid;

                // 기본값은 이메일로 설정
                string displayName = user.Info.Email;

                // 2. ★ DB에서 닉네임 가져오기 시도
                try
                {
                    var userInfo = await _dbClient
                        .Child("Users")
                        .Child(uid)
                        .OnceSingleAsync<UserModel>();

                    if (userInfo != null && !string.IsNullOrEmpty(userInfo.Nickname))
                    {
                        displayName = userInfo.Nickname; // 닉네임으로 교체
                    }
                }
                catch
                {
                    // DB에서 가져오기 실패하면 그냥 이메일 사용 (무시)
                }

                // 3. 환영 메시지 띄우기 (닉네임 사용)
                await Application.Current.MainPage.DisplayAlert("성공", $"{displayName}님 환영합니다!", "시작");

                await Shell.Current.GoToAsync("///MainTabs");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("로그인 실패", "이메일 또는 비밀번호를 확인해주세요.", "확인");
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