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
    // 인터페이스 추가
    public partial class MainViewModel : ObservableObject, IQueryAttributable
    {
        [ObservableProperty] private bool isShelterMode;
        [ObservableProperty] private bool isAdopterMode;
        [ObservableProperty] private bool isSeekerMode;
        [ObservableProperty] private bool isSearchVisible; // 검색창 표시 여부
        [ObservableProperty] private string welcomeMessage;
        [ObservableProperty] private string userEmail;

        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string searchSpecies;
        [ObservableProperty] private string searchLocation;
        [ObservableProperty] private string searchAge; // 나이 검색 추가
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

        public async Task OnAppearing()
        {
            await CheckUserType();
            if (_allPets.Count == 0) await LoadPets();
        }

        // [매칭 파라미터 수신]
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("MatchSpecies") || query.ContainsKey("MatchLocation"))
            {
                string matchSpecies = query.ContainsKey("MatchSpecies") ? query["MatchSpecies"].ToString() : "";
                string matchLocation = query.ContainsKey("MatchLocation") ? query["MatchLocation"].ToString() : "";

                SearchSpecies = matchSpecies;

                // 위치는 괄호(우편번호) 제거 후 검색
                if (!string.IsNullOrEmpty(matchLocation))
                {
                    int parenIndex = matchLocation.IndexOf('(');
                    if (parenIndex > 0) SearchLocation = matchLocation.Substring(0, parenIndex).Trim();
                    else SearchLocation = matchLocation;
                }

                if (IsSeekerMode) WelcomeMessage = "Matching results for your lost pet...";
                if (_allPets.Count > 0) SearchPets();
            }
        }

        private async Task CheckUserType()
        {
            string typeString = Preferences.Get("UserType", null);
            string nickname = Preferences.Get("UserNickname", null);

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
                            typeString = user.UserType.ToString();
                            nickname = user.Nickname;
                            Preferences.Set("UserType", typeString);
                            Preferences.Set("UserNickname", nickname);
                        }
                    }
                    catch { }
                }
            }

            if (string.IsNullOrEmpty(nickname)) nickname = "Guest";
            if (string.IsNullOrEmpty(typeString)) typeString = "AdoptionApplicant";

            UserEmail = nickname;

            if (Enum.TryParse(typeString, out UserType type))
            {
                IsShelterMode = (type == UserType.ShelterAndRescue);
                IsAdopterMode = (type == UserType.AdoptionApplicant);
                IsSeekerMode = (type == UserType.LostPetSeeker);

                // Seeker도 검색창 보이게 설정
                IsSearchVisible = IsAdopterMode || IsSeekerMode;

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
            catch (Exception ex) { await Application.Current.MainPage.DisplayAlert("Error", "Failed to load: " + ex.Message, "OK"); }
            finally { IsBusy = false; }
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

            // 나이 필터 적용
            if (!string.IsNullOrWhiteSpace(SearchAge))
                filtered = filtered.Where(p => p.Age != null && p.Age.Contains(SearchAge));

            if (!string.IsNullOrWhiteSpace(SearchStatus) && SearchStatus != "All")
                filtered = filtered.Where(p => p.Status != null && p.Status.Contains(SearchStatus.Split(' ')[0]));

            UpdateList(filtered.Reverse().ToList());
        }

        [RelayCommand]
        public void ResetFilter()
        {
            SearchSpecies = ""; SearchLocation = ""; SearchAge = "";
            SearchGender = "All"; SearchStatus = "All";
            if (IsSeekerMode) WelcomeMessage = $"Help find lost pets, {UserEmail}.";
            SearchPets();
        }

        [RelayCommand]
        private async Task GetCurrentLocation()
        {
            IsBusy = true;
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted) status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (status == PermissionStatus.Granted)
                {
                    var loc = await Geolocation.Default.GetLocationAsync();
                    if (loc != null)
                    {
                        var placemarks = await Geocoding.Default.GetPlacemarksAsync(loc.Latitude, loc.Longitude);
                        var placemark = placemarks?.FirstOrDefault();
                        if (placemark != null) SearchLocation = $"{placemark.AdminArea} {placemark.Locality}";
                    }
                }
            }
            catch { }
            finally { IsBusy = false; }
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