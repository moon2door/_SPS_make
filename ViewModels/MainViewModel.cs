using _SPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Linq;

namespace _SPS.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // ==========================================
        // 1. 유저 모드 및 UI 속성
        // ==========================================
        [ObservableProperty] private bool isShelterMode;
        [ObservableProperty] private bool isAdopterMode;
        [ObservableProperty] private bool isSeekerMode;
        [ObservableProperty] private string welcomeMessage;
        [ObservableProperty] private string userEmail;

        // ==========================================
        // 2. 검색 및 데이터 속성
        // ==========================================
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string searchSpecies;
        [ObservableProperty] private string searchLocation;
        [ObservableProperty] private string searchGender = "All";
        [ObservableProperty] private string searchStatus = "All";

        public ObservableCollection<PetModel> Pets { get; } = new();
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

            UserEmail = "Loading...";
        }

        // 화면이 뜰 때 호출 (MainPage.xaml.cs에서 호출)
        public async Task OnAppearing()
        {
            // 1. 유저 타입 확인 (안전장치 포함)
            await CheckUserType();

            // 2. 데이터가 비어있으면 로드
            if (_allPets.Count == 0)
            {
                await LoadPets();
            }
        }

        // ★ 핵심 수정: 정보가 없으면 DB에서 가져오는 안전장치 추가
        private async Task CheckUserType()
        {
            string typeString = Preferences.Get("UserType", null);
            string nickname = Preferences.Get("UserNickname", null);

            // 저장된 정보가 없다면 (이미 로그인된 기존 유저 등) -> DB에서 조회
            if (string.IsNullOrEmpty(typeString) || string.IsNullOrEmpty(nickname))
            {
                var myUid = _authClient.User?.Uid;
                if (!string.IsNullOrEmpty(myUid))
                {
                    try
                    {
                        var user = await _dbClient.Child("Users").Child(myUid).OnceSingleAsync<UserModel>();
                        if (user != null)
                        {
                            // 정보 갱신 및 저장
                            typeString = user.UserType.ToString();
                            nickname = user.Nickname;
                            Preferences.Set("UserType", typeString);
                            Preferences.Set("UserNickname", nickname);
                        }
                    }
                    catch
                    {
                        // 인터넷 오류 등: 기본값 유지
                    }
                }
            }

            // 기본값 처리
            if (string.IsNullOrEmpty(nickname)) nickname = "Guest";
            if (string.IsNullOrEmpty(typeString)) typeString = "AdoptionApplicant";

            UserEmail = nickname;

            if (Enum.TryParse(typeString, out UserType type))
            {
                IsShelterMode = (type == UserType.ShelterAndRescue);
                IsAdopterMode = (type == UserType.AdoptionApplicant);
                IsSeekerMode = (type == UserType.LostPetSeeker);

                if (IsShelterMode) WelcomeMessage = $"{nickname} (Manager)";
                else if (IsSeekerMode) WelcomeMessage = $"Help find lost pets, {nickname}.";
                else WelcomeMessage = $"Find your companion, {nickname}.";
            }
        }

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
                SearchPets();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Failed to load: " + ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void SearchPets()
        {
            if (_allPets == null) return;
            var filtered = _allPets.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchSpecies))
                filtered = filtered.Where(p => p.Species != null && p.Species.Contains(SearchSpecies, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchLocation))
                filtered = filtered.Where(p => p.Location != null && p.Location.Contains(SearchLocation, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchGender) && SearchGender != "All")
                filtered = filtered.Where(p => p.Gender != null && p.Gender.Equals(SearchGender, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchStatus) && SearchStatus != "All")
                filtered = filtered.Where(p => p.Status != null && p.Status.Contains(SearchStatus.Split(' ')[0]));

            UpdateList(filtered.Reverse().ToList());
        }

        [RelayCommand]
        public void ResetFilter()
        {
            SearchSpecies = "";
            SearchLocation = "";
            SearchGender = "All";
            SearchStatus = "All";
            SearchPets();
        }

        private void UpdateList(List<PetModel> list)
        {
            Pets.Clear();
            foreach (var item in list) Pets.Add(item);
        }

        [RelayCommand]
        private async Task GoToDetail(PetModel pet)
        {
            if (pet == null) return;
            var param = new Dictionary<string, object> { { "Pet", pet }, { "IsReadOnly", true } };
            await Shell.Current.GoToAsync(nameof(Views.PetDetailPage), param);
        }

        [RelayCommand]
        private void CallOwner(string phoneNumber)
        {
            if (!string.IsNullOrWhiteSpace(phoneNumber) && PhoneDialer.Default.IsSupported)
                PhoneDialer.Default.Open(phoneNumber);
        }

        [RelayCommand]
        private async Task GoToAddPet() => await Shell.Current.GoToAsync(nameof(Views.AddPetPage));

        [RelayCommand]
        private async Task NavigateToMyUploads() => await Shell.Current.GoToAsync(nameof(Views.MyUploadsPage));

        [RelayCommand]
        private async Task Logout()
        {
            if (await Application.Current.MainPage.DisplayAlert("LogOut", "Would you like to log out?", "Yes", "No"))
            {
                Preferences.Clear();
                await Shell.Current.GoToAsync("//LoginPage");
            }
        }
    }
}