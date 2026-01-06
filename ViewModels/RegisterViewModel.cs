using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query; // DB 저장을 위해 필요
using _SPS.Models; // UserModel을 쓰기 위해 필요
using System.Threading.Tasks;

namespace _SPS.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private string nickname; // 닉네임 입력 필드 추가

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private bool isBusy;

        public bool CanExecute => !IsBusy;

        // Firebase 연결 도구들
        private readonly FirebaseAuthClient _authClient;
        private readonly FirebaseClient _dbClient;

        public RegisterViewModel()
        {
            // 1. 인증(로그인) 초기화
            var config = new FirebaseAuthConfig
            {
                ApiKey = Constants.FirebaseApiKey,
                AuthDomain = Constants.AuthDomain,
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };
            _authClient = new FirebaseAuthClient(config);

            // 2. 데이터베이스 초기화 (URL 사용)
            _dbClient = new FirebaseClient(Constants.FirebaseDatabaseUrl);
        }

        [RelayCommand(CanExecute = nameof(CanExecute))]
        private async Task Register()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Nickname))
            {
                await Application.Current.MainPage.DisplayAlert("오류", "모든 항목을 입력해주세요.", "확인");
                return;
            }

            IsBusy = true;

            try
            {
                // 1단계: Firebase Auth에 이메일/비번으로 계정 생성
                var userCredential = await _authClient.CreateUserWithEmailAndPasswordAsync(Email, Password, Nickname);
                var uid = userCredential.User.Uid; // 생성된 고유 ID 가져오기

                // 2단계: DB에 저장할 데이터 뭉치(모델) 만들기
                var newUser = new UserModel
                {
                    Uid = uid,
                    Nickname = Nickname,
                    CreationDate = DateTime.Now
                };

                // 3단계: Realtime Database의 "Users" 폴더 아래에 내 ID로 저장
                // 경로: Users -> [내UID] -> { 데이터 }
                await _dbClient
                    .Child("Users")
                    .Child(uid)
                    .PutAsync(newUser);

                await Application.Current.MainPage.DisplayAlert("성공", "회원가입이 완료되었습니다!", "로그인하러 가기");

                // 4단계: 가입 성공 후 로그인 페이지로 뒤로 가기
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("가입 실패", $"오류: {ex.Message}", "확인");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}