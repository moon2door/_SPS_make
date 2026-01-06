using _SPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query;
using System.Collections.ObjectModel;

namespace _SPS.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // [사용자 정보]
        [ObservableProperty] private string userEmail;
        [ObservableProperty] private bool isBusy;

        // [화면(MainPage.xaml)과 연결된 검색 속성]
        [ObservableProperty] private string searchSpecies;  // 견종 검색
        [ObservableProperty] private string searchLocation; // 지역 검색
        [ObservableProperty] private string searchGender = "전체";   // 성별 필터 (전체/수컷/암컷)
        [ObservableProperty] private string searchStatus = "전체";

        // 화면에 보여줄 목록
        public ObservableCollection<PetModel> Pets { get; } = new();

        // 원본 데이터 저장소 (필터링 전)
        private List<PetModel> _allPets = new();

        private readonly FirebaseClient _dbClient;
        private readonly FirebaseAuthClient _authClient;

        public MainViewModel()
        {
            _dbClient = new FirebaseClient(Constants.FirebaseDatabaseUrl);

            var config = new FirebaseAuthConfig
            {
                ApiKey = Constants.FirebaseApiKey,
                AuthDomain = Constants.AuthDomain,
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };
            _authClient = new FirebaseAuthClient(config);

            UserEmail = "로딩 중...";

            // 시작할 때 데이터와 내 정보 로드
            LoadUserInfo();
            LoadPetsCommand.Execute(null);
        }

        // [내 정보 가져오기]
        public async Task LoadUserInfo()
        {
            try
            {
                var myUid = _authClient.User?.Uid;
                if (string.IsNullOrEmpty(myUid))
                {
                    UserEmail = "로그인 필요";
                    return;
                }

                var myData = await _dbClient.Child("Users").Child(myUid).OnceSingleAsync<UserModel>();
                if (myData != null)
                {
                    UserEmail = myData.Nickname;
                }
                else
                {
                    UserEmail = "사용자";
                }
            }
            catch
            {
                UserEmail = "사용자";
            }
        }

        // [검색 및 필터 적용 로직]
        [RelayCommand]
        public void SearchPets()
        {
            if (_allPets == null) return;

            // 1. 원본에서 시작
            var filtered = _allPets.AsEnumerable();

            // 2. 견종 검색
            if (!string.IsNullOrWhiteSpace(SearchSpecies))
            {
                filtered = filtered.Where(p => p.Species != null &&
                                          p.Species.Contains(SearchSpecies, StringComparison.OrdinalIgnoreCase));
            }

            // 3. 지역 검색
            if (!string.IsNullOrWhiteSpace(SearchLocation))
            {
                filtered = filtered.Where(p => p.Location != null &&
                                          p.Location.Contains(SearchLocation, StringComparison.OrdinalIgnoreCase));
            }

            // 4. 성별 필터 ("전체"가 아니고, 값이 있을 때만)
            if (!string.IsNullOrWhiteSpace(SearchGender) && SearchGender != "전체")
            {
                filtered = filtered.Where(p => p.Gender != null && p.Gender.Contains(SearchGender));
            }

            if (!string.IsNullOrWhiteSpace(SearchStatus) && SearchStatus != "전체")
            {
                // "보호중 (Under Care)" 처럼 괄호가 있을 수 있으므로 Contains나 StartsWith 사용
                // 여기서는 앞 두 글자(보호중, 실종)만 맞아도 되게 Contains로 처리
                filtered = filtered.Where(p => p.Status != null && p.Status.Contains(SearchStatus.Split(' ')[0]));
            }

            // 5. 결과 업데이트 (최신순 정렬)
            UpdateList(filtered.Reverse().ToList());
        }

        // [필터 초기화]
        [RelayCommand]
        public void ResetFilter()
        {
            SearchSpecies = "";
            SearchLocation = "";
            SearchGender = "전체";
            SearchStatus = "전체";
            SearchPets(); // 전체 목록 다시 보여주기
        }

        // 화면 리스트 갱신 헬퍼
        private void UpdateList(List<PetModel> list)
        {
            Pets.Clear();
            foreach (var item in list)
            {
                Pets.Add(item);
            }
        }

        // [데이터 불러오기]
        [RelayCommand]
        public async Task LoadPets()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                var collection = await _dbClient.Child("Pets").OnceAsync<PetModel>();
                _allPets.Clear();

                foreach (var item in collection)
                {
                    var pet = item.Object;
                    pet.Key = item.Key;
                    _allPets.Add(pet);
                }

                // 데이터 로드 후 필터 적용
                SearchPets();
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

        // [상세 페이지 이동]
        [RelayCommand]
        private async Task GoToDetail(PetModel pet)
        {
            if (pet == null) return;
            var param = new Dictionary<string, object>
            {
                { "Pet", pet },
                { "IsReadOnly", true }
            };
            await Shell.Current.GoToAsync(nameof(Views.PetDetailPage), param);
        }

        // [전화 걸기]
        [RelayCommand]
        private void CallOwner(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return;
            if (PhoneDialer.Default.IsSupported)
                PhoneDialer.Default.Open(phoneNumber);
            else
                Application.Current.MainPage.DisplayAlert("알림", "전화 걸기를 지원하지 않습니다.", "확인");
        }

        // ★ [복구] 펫 추가 페이지 이동
        [RelayCommand]
        private async Task NavigateToAddPet() => await Shell.Current.GoToAsync(nameof(Views.AddPetPage));

        // ★ [복구] 내 업로드 목록 이동
        [RelayCommand]
        private async Task NavigateToMyUploads() => await Shell.Current.GoToAsync(nameof(Views.MyUploadsPage));

        // ★ [복구] 로그아웃
        [RelayCommand]
        private async Task Logout()
        {
            if (await Application.Current.MainPage.DisplayAlert("로그아웃", "로그아웃 하시겠습니까?", "예", "아니요"))
                await Shell.Current.GoToAsync("///LoginPage");
        }
    }
}