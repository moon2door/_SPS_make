using _SPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Storage;
using System.Text;
using System.Text.Json; 

namespace _SPS.ViewModels
{
    public partial class AddPetViewModel : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private string species;
        [ObservableProperty] private string gender;
        [ObservableProperty] private string age;
        [ObservableProperty] private string description;
        [ObservableProperty] private string status = "Protected";
        [ObservableProperty] private string weight;
        [ObservableProperty] private string condition;
        [ObservableProperty] private string feature;
        [ObservableProperty] private string contact;
        [ObservableProperty] private string location;

        [ObservableProperty] private bool isBusy;

        [ObservableProperty] private ImageSource petImageSource1; 
        [ObservableProperty] private ImageSource petImageSource2;
        [ObservableProperty] private ImageSource petImageSource3;
        [ObservableProperty] private ImageSource petImageSource4;

        private FileResult _file1;
        private FileResult _file2;
        private FileResult _file3;
        private FileResult _file4;

        private readonly FirebaseClient _dbClient;
        private readonly FirebaseAuthClient _authClient;

        private const string GeminiApiKey = "AIzaSyC8pIZR6BmYk0mI7Ak4AxXKVbdSYMbd_DM";

        public AddPetViewModel()
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

        [RelayCommand]
        private async Task PickImage(string slot)
        {
            try
            {
                var result = await MediaPicker.Default.PickPhotoAsync();
                if (result != null)
                {
                    var stream = await result.OpenReadAsync();
                    var imgSource = ImageSource.FromStream(() => stream);

                    switch (slot)
                    {
                        case "1": 
                            _file1 = result;
                            PetImageSource1 = imgSource;

                            bool answer = await Application.Current.MainPage.DisplayAlert(
                                "AI Analysis", "Shall we analyze the breed using this photo (front view)?", "Yes", "No");
                            if (answer)
                            {
                                await AnalyzeImageWithGemini(result);
                            }
                            break;

                        case "2": 
                            _file2 = result;
                            PetImageSource2 = imgSource;
                            break;

                        case "3": 
                            _file3 = result;
                            PetImageSource3 = imgSource;
                            break;

                        case "4": 
                            _file4 = result;
                            PetImageSource4 = imgSource;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Photo selection failed: " + ex.Message, "Confirmation");
            }
        }

        private async Task AnalyzeImageWithGemini(FileResult file)
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                using var stream = await file.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                byte[] imageBytes = memoryStream.ToArray();
                string base64Image = Convert.ToBase64String(imageBytes);

                var promptText = "Analyze the dog in this photo and return ONLY a JSON object in English. Do not say anything else.\n\n" +
                                 "Format:\n" +
                                 "{\n" +
                                 "\"breed\": \"Dog breed (e.g., Golden Retriever)\",\n" +
                                 "\"age\": \"Estimated age (numbers only, e.g., 3)\",\n" +
                                 "\"weight\": \"Estimated weight in kg (numbers only, e.g., 15.5)\",\n" +
                                 "\"condition\": \"Brief health condition in English (e.g., Healthy coat)\",\n" +
                                 "\"feature\": \"Notable features in English (e.g., Floppy ears)\"\n" +
                                 "}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = promptText },
                                new {
                                    inline_data = new {
                                        mime_type = "image/jpeg",
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    }
                };

                using var client = new HttpClient();
                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-pro:generateContent?key={GeminiApiKey}", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var resultJson = await response.Content.ReadAsStringAsync();

                    using var doc = JsonDocument.Parse(resultJson);
                    var candidates = doc.RootElement.GetProperty("candidates");

                    if (candidates.GetArrayLength() > 0)
                    {
                        var text = candidates[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();

                        var cleanJson = text.Replace("```json", "").Replace("```", "").Trim();
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var petData = JsonSerializer.Deserialize<GeminiPetData>(cleanJson, options);

                        if (petData != null)
                        {
                            Species = petData.breed;
                            Age = petData.age;
                            Weight = petData.weight;
                            Condition = petData.condition;
                            Feature = petData.feature;

                            await Application.Current.MainPage.DisplayAlert("Success", "AI Analysis complete!", "Confirmation");
                        }
                    }
                }
                else
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    await Application.Current.MainPage.DisplayAlert("API Error", $"Response code: {response.StatusCode}\n{errorMsg}", "Confirmation");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Analysis failed", ex.Message, "Confirmation");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task GetCurrentLocation()
        {
            IsBusy = true;
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                if (status == PermissionStatus.Granted)
                {
                    var location = await Geolocation.Default.GetLocationAsync();
                    if (location != null)
                    {
                        var placemarks = await Geocoding.Default.GetPlacemarksAsync(location.Latitude, location.Longitude);
                        var placemark = placemarks?.FirstOrDefault();
                        if (placemark != null)
                        {
                            Location = $"{placemark.AdminArea} {placemark.Locality} {placemark.Thoroughfare}";
                        }
                    }
                }
                else
                {
                    Location = "You have no location permissions.";
                }
            }
            catch
            {
                Location = "Location not found.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SavePet()
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Species) || string.IsNullOrWhiteSpace(Gender))
            {
                await Application.Current.MainPage.DisplayAlert("Notice", "Name, species, and gender are required fields.", "Confirmation");
                return;
            }

            IsBusy = true;
            try
            {
                if (_authClient.User == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", "You don't have any login information.", "Confirmation");
                    return;
                }

                string url1 = await UploadImage(_file1);
                string url2 = await UploadImage(_file2);
                string url3 = await UploadImage(_file3);
                string url4 = await UploadImage(_file4);

                var newPet = new PetModel
                {
                    Name = Name,
                    Species = Species,
                    Gender = Gender,
                    Status = Status,
                    Age = Age,
                    Weight = Weight,
                    Condition = Condition,
                    Feature = Feature,
                    Contact = Contact,
                    Location = Location,
                    Description = Description,
                    OwnerId = _authClient.User.Uid,

                    ImageUrl1 = url1,
                    ImageUrl2 = url2,
                    ImageUrl3 = url3,
                    ImageUrl4 = url4
                };

                await _dbClient.Child("Pets").PostAsync(newPet);
                await Application.Current.MainPage.DisplayAlert("Success", "Registered.", "Confirmation");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "Confirmation");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<string> UploadImage(FileResult file)
        {
            if (file == null) return ""; 

            using var stream = await file.OpenReadAsync();
            var fileName = $"{Guid.NewGuid()}.png";

            return await new FirebaseStorage(Constants.FirebaseStorageBucket)
                .Child("PetImages").Child(fileName).PutAsync(stream);
        }

        public class GeminiPetData
        {
            public string breed { get; set; }
            public string age { get; set; }
            public string weight { get; set; }
            public string condition { get; set; }
            public string feature { get; set; }
        }
    }
}