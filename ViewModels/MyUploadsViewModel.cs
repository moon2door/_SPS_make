using _SPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using System.Collections.ObjectModel;

namespace _SPS.ViewModels
{
    public partial class MyUploadsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isBusy;

        public ObservableCollection<PetModel> MyPets { get; } = new();

        private readonly FirebaseClient _dbClient;
        private readonly FirebaseAuthClient _authClient;

        public MyUploadsViewModel()
        {
            _dbClient = new FirebaseClient(Constants.FirebaseDatabaseUrl);

            var config = new FirebaseAuthConfig
            {
                ApiKey = Constants.FirebaseApiKey,
                AuthDomain = Constants.AuthDomain,
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };
            _authClient = new FirebaseAuthClient(config);
        }

        public async Task LoadMyPets()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                var myUid = _authClient.User?.Uid;
                if (string.IsNullOrEmpty(myUid))
                {
                    await Application.Current.MainPage.DisplayAlert("Error", "You do not have login credentials.", "Confirmation");
                    return;
                }

                var collection = await _dbClient
                    .Child("Pets")
                    .OnceAsync<PetModel>();

                MyPets.Clear();

                foreach (var item in collection)
                {
                    var pet = item.Object;
                    pet.Key = item.Key;

                    if (pet.OwnerId == myUid) 
                    {
                        MyPets.Add(pet);
                    }
                }
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
        private async Task GoToDetail(PetModel selectedPet)
        {
            if (selectedPet == null) return;
            var param = new Dictionary<string, object>
            {
                { "Pet", selectedPet },
                { "IsReadOnly", false } 
            };
            await Shell.Current.GoToAsync(nameof(Views.PetDetailPage), param);
        }
    }
}