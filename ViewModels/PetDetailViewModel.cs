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
    [QueryProperty(nameof(Pet), "Pet")]
    [QueryProperty(nameof(IsReadOnly), "IsReadOnly")]
    public partial class PetDetailViewModel : ObservableObject
    {
        [ObservableProperty] private PetModel pet;
        [ObservableProperty] private string name;
        [ObservableProperty] private string species;
        [ObservableProperty] private string gender;
        [ObservableProperty] private string status;
        [ObservableProperty] private string age;
        [ObservableProperty] private string description;
        [ObservableProperty] private string weight;
        [ObservableProperty] private string condition;
        [ObservableProperty] private string feature;
        [ObservableProperty] private string contact;
        [ObservableProperty] private string location;

        [ObservableProperty]
        private ObservableCollection<string> petImages = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEdit))]
        [NotifyPropertyChangedFor(nameof(IsNotOwner))] // 변경시 알림
        private bool isOwner;

        public bool IsNotOwner => !IsOwner; // 작성자가 아닐 때

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEdit))]
        private bool isReadOnly;

        public bool CanEdit => IsOwner && !IsReadOnly;

        private readonly FirebaseClient _dbClient;
        private readonly FirebaseAuthClient _authClient;

        public PetDetailViewModel()
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

        partial void OnPetChanged(PetModel value)
        {
            if (value != null)
            {
                Name = value.Name; Species = value.Species; Gender = value.Gender;
                Status = value.Status; Age = value.Age; Description = value.Description;
                Weight = value.Weight; Condition = value.Condition; Feature = value.Feature;
                Contact = value.Contact; Location = value.Location;
                PetImages.Clear();
                if (!string.IsNullOrEmpty(value.ImageUrl1)) PetImages.Add(value.ImageUrl1);
                if (!string.IsNullOrEmpty(value.ImageUrl2)) PetImages.Add(value.ImageUrl2);
                if (!string.IsNullOrEmpty(value.ImageUrl3)) PetImages.Add(value.ImageUrl3);
                if (!string.IsNullOrEmpty(value.ImageUrl4)) PetImages.Add(value.ImageUrl4);
                if (PetImages.Count == 0) PetImages.Add("dotnet_bot.png");

                var myUid = _authClient.User?.Uid;
                IsOwner = !string.IsNullOrEmpty(myUid) && value.OwnerId == myUid;
            }
        }

        [RelayCommand]
        private async Task UpdatePet()
        {
            if (!CanEdit) return;
            bool confirm = await Application.Current.MainPage.DisplayAlert("Revision", "Would you like to modify the information?", "Yes", "No");
            if (confirm)
            {
                Pet.Name = Name; Pet.Species = Species; Pet.Age = Age;
                Pet.Description = Description; Pet.Weight = Weight; Pet.Condition = Condition;
                Pet.Feature = Feature; Pet.Contact = Contact; Pet.Location = Location;
                Pet.Status = Status; Pet.Gender = Gender;

                await _dbClient.Child("Pets").Child(Pet.Key).PutAsync(Pet);
                await Application.Current.MainPage.DisplayAlert("Success", "Information updated.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        [RelayCommand]
        private async Task DeletePet()
        {
            if (!CanEdit) return;
            bool confirm = await Application.Current.MainPage.DisplayAlert("Delete", "Are you sure you want to delete this?", "Delete", "Cancel");
            if (confirm)
            {
                await _dbClient.Child("Pets").Child(Pet.Key).DeleteAsync();
                await Application.Current.MainPage.DisplayAlert("Deleted", "Post has been deleted.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        // [입양 문의 로직]
        [RelayCommand]
        private async Task AdoptPet()
        {
            bool answer = await Application.Current.MainPage.DisplayAlert(
                "Adoption Inquiry",
                "Would you like to adopt this pet? We will connect you to the shelter.",
                "Yes (Call)", "No");

            if (answer)
            {
                if (!string.IsNullOrWhiteSpace(Contact) && PhoneDialer.Default.IsSupported)
                    PhoneDialer.Default.Open(Contact);
                else
                    await Application.Current.MainPage.DisplayAlert("Notice", $"Contact the shelter at: {Contact}", "OK");
            }
        }

        [RelayCommand]
        private async Task SharePet()
        {
            if (Pet == null) return;
            try
            {
                string shareText = $"[{Pet.Status}] Looking for family!\nName: {Name}\nBreed: {Species}\nLocation: {Location}";
                await Share.Default.RequestAsync(new ShareTextRequest { Text = shareText, Title = "Animal Sharing" });
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Share failed: " + ex.Message, "OK");
            }
        }
    }
}