using _SPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query;

namespace _SPS.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private string nickname;

        // 추가된 입력 필드
        [ObservableProperty]
        private string organizationName;

        [ObservableProperty]
        private string address;

        [ObservableProperty]
        private string phoneNumber;

        // 사용자 유형 선택을 위한 속성
        [ObservableProperty]
        private UserType selectedUserType;

        // 피커(Picker)에 바인딩할 유형 목록
        public List<string> UserTypes { get; } = Enum.GetNames(typeof(UserType)).ToList();

        // 뷰에서 문자열로 선택된 값을 Enum으로 변환하기 위한 프로퍼티
        private string _selectedUserTypeName;
        public string SelectedUserTypeName
        {
            get => _selectedUserTypeName;
            set
            {
                if (SetProperty(ref _selectedUserTypeName, value))
                {
                    if (Enum.TryParse(value, out UserType result))
                    {
                        SelectedUserType = result;
                    }
                }
            }
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private bool isBusy;

        public bool CanExecute => !IsBusy;

        private readonly FirebaseAuthClient _authClient;
        private readonly FirebaseClient _dbClient;

        public RegisterViewModel()
        {
            var config = new FirebaseAuthConfig
            {
                ApiKey = Constants.FirebaseApiKey,
                AuthDomain = Constants.AuthDomain,
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };
            _authClient = new FirebaseAuthClient(config);
            _dbClient = new FirebaseClient(Constants.FirebaseDatabaseUrl);

            // 기본값 설정
            SelectedUserTypeName = UserTypes.FirstOrDefault();
        }

        [RelayCommand(CanExecute = nameof(CanExecute))]
        private async Task Register()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Nickname))
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Please fill in all required fields (Email, Password, Nickname).", "OK");
                return;
            }

            // 기타 기관인 경우 기관명/주소 필수 체크 (필요시 로직 강화 가능)
            if (SelectedUserType == UserType.OtherOrganization && (string.IsNullOrWhiteSpace(OrganizationName) || string.IsNullOrWhiteSpace(Address)))
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Organization Name and Address are required for Organizations.", "OK");
                return;
            }

            IsBusy = true;

            try
            {
                // 1. Firebase Auth에 유저 생성
                var userCredential = await _authClient.CreateUserWithEmailAndPasswordAsync(Email, Password, Nickname);
                var uid = userCredential.User.Uid;

                // 2. DB에 저장할 모델 생성
                var newUser = new UserModel
                {
                    Uid = uid,
                    Email = Email,
                    Nickname = Nickname,
                    UserType = SelectedUserType,
                    OrganizationName = OrganizationName,
                    Address = Address,
                    PhoneNumber = PhoneNumber,
                    CreationDate = DateTime.Now
                };

                // 3. Realtime Database에 저장
                await _dbClient
                    .Child("Users")
                    .Child(uid)
                    .PutAsync(newUser);

                await Application.Current.MainPage.DisplayAlert("Success", "Your registration has been completed.", "Go to Login");

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Registration failed", $"Error: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}