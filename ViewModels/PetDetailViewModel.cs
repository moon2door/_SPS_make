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
        private bool isOwner;

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
                Name = value.Name;
                Species = value.Species;
                Gender = value.Gender;
                Status = value.Status;
                Age = value.Age;
                Description = value.Description;
                Weight = value.Weight;
                Condition = value.Condition;
                Feature = value.Feature;
                Contact = value.Contact;
                Location = value.Location;
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
                Pet.Name = Name;
                Pet.Species = Species;
                Pet.Age = Age;
                Pet.Description = Description;
                Pet.Weight = Weight;
                Pet.Condition = Condition;
                Pet.Feature = Feature;
                Pet.Contact = Contact;
                Pet.Location = Location;

                await _dbClient.Child("Pets").Child(Pet.Key).PutAsync(Pet);
                await Application.Current.MainPage.DisplayAlert("Success", "It has been revised.", "Confirmation");
                await Shell.Current.GoToAsync("..");
            }
        }

        [RelayCommand]
        private async Task DeletePet()
        {
            if (!CanEdit) return; 

            bool confirm = await Application.Current.MainPage.DisplayAlert("Delete", "Are you sure you want to delete this?", "Delete", "Cancle");
            if (confirm)
            {
                await _dbClient.Child("Pets").Child(Pet.Key).DeleteAsync();
                await Application.Current.MainPage.DisplayAlert("Deleted", "It has been deleted.", "Confirmation");
                await Shell.Current.GoToAsync("..");
            }
        }

        [RelayCommand]
        private async Task SharePet()
        {
            if (Pet == null) return;

            try
            {
                string shareText = $"[{Pet.Status}] Looking for family!\n\n" +
                                   $"Name: {Name}\n" +
                                   $"Breed: {Species}\n" +
                                   $"Region: {Location}\n" +
                                   $"Features: {Feature}\n\n" +
                                   $"#stray dog #Poppy #companion dog #{Species} #{Location}";

                string imagePath = null;
                if (!string.IsNullOrEmpty(Pet.ImageUrl1))
                {
                    using var client = new HttpClient();
                    var imageBytes = await client.GetByteArrayAsync(Pet.ImageUrl1);

                    imagePath = Path.Combine(FileSystem.CacheDirectory, "share_pet.png");
                    File.WriteAllBytes(imagePath, imageBytes);
                }

                if (imagePath != null)
                {
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "Animal Information Sharing",
                        File = new ShareFile(imagePath),
                        PresentationSourceBounds = DeviceInfo.Platform == DevicePlatform.iOS && DeviceInfo.Idiom == DeviceIdiom.Tablet
                                                    ? new Rect(0, 20, 0, 0) 
                                                    : Rect.Zero
                    });
                }
                else
                {
                    // 이미지가 없으면 텍스트만 공유
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Text = shareText,
                        Title = "Animal Information Sharing"
                    });
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "An issue occurred during sharing.: " + ex.Message, "Confirmation");
            }
        }
    }
}