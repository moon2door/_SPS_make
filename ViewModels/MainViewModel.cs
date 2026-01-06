using _SPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth; // 인증 기능 추가
using Firebase.Auth.Providers; // 인증 제공자 추가
using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.Maui.ApplicationModel.Communication;
using System.Collections.ObjectModel;

namespace _SPS.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // 화면의 "사용자: OOO" 부분에 들어갈 변수
        [ObservableProperty] private string userEmail;
        [ObservableProperty] private bool isBusy;

        // [검색 및 필터 속성]
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Pets))]
        private string searchText;

        [ObservableProperty] private string filterBreed;
        [ObservableProperty] private string filterAge;
        [ObservableProperty] private string filterCondition;

        // 필터 창 보임/숨김 상태
        [ObservableProperty] private bool isFilterVisible;

        public ObservableCollection<PetModel> Pets { get; } = new();

        private List<PetModel> _allPets = new();

        private readonly FirebaseClient _dbClient;
        private readonly FirebaseAuthClient _authClient; // 내 ID 확인용

        public MainViewModel()
        {
            _dbClient = new FirebaseClient(Constants.FirebaseDatabaseUrl);

            // 로그인 정보 확인을 위해 Auth 클라이언트 설정
            var config = new FirebaseAuthConfig
            {
                ApiKey = Constants.FirebaseApiKey,
                AuthDomain = Constants.AuthDomain,
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };
            _authClient = new FirebaseAuthClient(config);

            // 초기값 (로딩 전)
            UserEmail = "로딩 중...";
        }

        // ★ [추가됨] 내 닉네임 가져오기
        public async Task LoadUserInfo()
        {
            try
            {
                var myUid = _authClient.User?.Uid;
                if (string.IsNullOrEmpty(myUid)) return;

                // DB의 Users -> [내ID] 위치에서 정보를 가져옴
                var myData = await _dbClient
                    .Child("Users")
                    .Child(myUid)
                    .OnceSingleAsync<UserModel>();

                if (myData != null)
                {
                    // 가져온 닉네임을 화면에 표시!
                    UserEmail = myData.Nickname;
                }
            }
            catch
            {
                // 에러나면 그냥 기본값 유지
                UserEmail = "사용자";
            }
        }

        [RelayCommand]
        private void ToggleFilter()
        {
            IsFilterVisible = !IsFilterVisible;
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            if (_allPets == null || !_allPets.Any()) return;

            var filtered = _allPets.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(p =>
                    (p.Name != null && p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Species != null && p.Species.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                );
            }

            if (!string.IsNullOrWhiteSpace(FilterBreed))
            {
                filtered = filtered.Where(p => p.Species != null && p.Species.Contains(FilterBreed, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(FilterAge))
            {
                filtered = filtered.Where(p => p.Age != null && p.Age.Contains(FilterAge));
            }

            if (!string.IsNullOrWhiteSpace(FilterCondition))
            {
                filtered = filtered.Where(p => p.Condition != null && p.Condition.Contains(FilterCondition, StringComparison.OrdinalIgnoreCase));
            }

            UpdateList(filtered.ToList());
        }

        [RelayCommand]
        private void ResetFilter()
        {
            SearchText = string.Empty;
            FilterBreed = string.Empty;
            FilterAge = string.Empty;
            FilterCondition = string.Empty;
            UpdateList(_allPets);
            IsFilterVisible = false;
        }

        [RelayCommand]
        private void CallOwner(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                Application.Current.MainPage.DisplayAlert("알림", "연락처 정보가 없습니다.", "확인");
                return;
            }

            if (PhoneDialer.Default.IsSupported)
            {
                PhoneDialer.Default.Open(phoneNumber);
            }
            else
            {
                Application.Current.MainPage.DisplayAlert("알림", "전화 걸기를 지원하지 않는 기기입니다.", "확인");
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        private void UpdateList(List<PetModel> list)
        {
            Pets.Clear();
            foreach (var item in list)
            {
                Pets.Add(item);
            }
        }

        public async Task LoadPets()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                var collection = await _dbClient
                    .Child("Pets")
                    .OnceAsync<PetModel>();

                _allPets.Clear();

                foreach (var item in collection)
                {
                    var pet = item.Object;
                    pet.Key = item.Key;
                    _allPets.Add(pet);
                }
                ApplyFilter();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("오류", "로딩 실패: " + ex.Message, "확인");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task NavigateToAddPet() => await Shell.Current.GoToAsync(nameof(Views.AddPetPage));

        [RelayCommand]
        private async Task NavigateToMyUploads() => await Shell.Current.GoToAsync(nameof(Views.MyUploadsPage));

        [RelayCommand]
        private async Task Logout()
        {
            if (await Application.Current.MainPage.DisplayAlert("로그아웃", "로그아웃 하시겠습니까?", "예", "아니요"))
                await Shell.Current.GoToAsync("///LoginPage");
        }

        [RelayCommand]
        private async Task GoToDetail(PetModel selectedPet)
        {
            if (selectedPet == null) return;
            var param = new Dictionary<string, object>
            {
                { "Pet", selectedPet },
                { "IsReadOnly", true } // ★ 홈에서는 무조건 수정 불가!
            };
            await Shell.Current.GoToAsync(nameof(Views.PetDetailPage), param);
        }
    }
}