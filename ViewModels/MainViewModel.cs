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
        [ObservableProperty] private string userEmail;
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

            LoadUserInfo();
            LoadPetsCommand.Execute(null);
        }

        public async Task LoadUserInfo()
        {
            try
            {
                var myUid = _authClient.User?.Uid;
                if (string.IsNullOrEmpty(myUid))
                {
                    UserEmail = "Login required";
                    return;
                }

                var myData = await _dbClient.Child("Users").Child(myUid).OnceSingleAsync<UserModel>();
                if (myData != null)
                {
                    UserEmail = myData.Nickname;
                }
                else
                {
                    UserEmail = "Users";
                }
            }
            catch
            {
                UserEmail = "Users";
            }
        }

        [RelayCommand]
        public void SearchPets()
        {
            if (_allPets == null) return;

            var filtered = _allPets.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchSpecies))
            {
                filtered = filtered.Where(p => p.Species != null &&
                                          p.Species.Contains(SearchSpecies, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SearchLocation))
            {
                filtered = filtered.Where(p => p.Location != null &&
                                          p.Location.Contains(SearchLocation, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SearchGender) && SearchGender != "All")
            {
                filtered = filtered.Where(p => p.Gender != null && p.Gender.Contains(SearchGender));
            }

            if (!string.IsNullOrWhiteSpace(SearchStatus) && SearchStatus != "All")
            {
                filtered = filtered.Where(p => p.Status != null && p.Status.Contains(SearchStatus.Split(' ')[0]));
            }

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
            foreach (var item in list)
            {
                Pets.Add(item);
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
                await Application.Current.MainPage.DisplayAlert("Error", "Failed to load: " + ex.Message, "Confirmation");
            }
            finally
            {
                IsBusy = false;
            }
        }

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

        [RelayCommand]
        private void CallOwner(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return;
            if (PhoneDialer.Default.IsSupported)
                PhoneDialer.Default.Open(phoneNumber);
            else
                Application.Current.MainPage.DisplayAlert("Notice", "Does not support making phone calls.", "Confirmation");
        }

        [RelayCommand]
        private async Task NavigateToAddPet() => await Shell.Current.GoToAsync(nameof(Views.AddPetPage));

        [RelayCommand]
        private async Task NavigateToMyUploads() => await Shell.Current.GoToAsync(nameof(Views.MyUploadsPage));

        [RelayCommand]
        private async Task Logout()
        {
            if (await Application.Current.MainPage.DisplayAlert("LogOut", "Would you like to log out?", "Yes", "No"))
                await Shell.Current.GoToAsync("///LoginPage");
        }
    }
}