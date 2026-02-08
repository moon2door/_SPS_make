using _SPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Storage;
using Microsoft.Maui.Storage; // Preferences 사용
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
        [ObservableProperty] private string status = "Under Care";
        [ObservableProperty] private string weight;
        [ObservableProperty] private string condition;
        [ObservableProperty] private string feature;
        [ObservableProperty] private string contact;
        [ObservableProperty] private string location;

        // 화면 UI 동적 변경용
        [ObservableProperty] private string pageTitle = "New Animal Registration";
        [ObservableProperty] private string buttonText = "Register";

        [ObservableProperty] private bool isBusy;

        [ObservableProperty] private ImageSource petImageSource1;
        [ObservableProperty] private ImageSource petImageSource2;
        [ObservableProperty] private ImageSource petImageSource3;
        [ObservableProperty] private ImageSource petImageSource4;

        private FileResult _file1;
        private FileResult _file2;
        private FileResult _file3;
        private FileResult _file4;
        private bool _isSeeker;

        private readonly FirebaseClient _dbClient;
        private readonly FirebaseAuthClient _authClient;

        // 실제 키 유지
        private const string GeminiApiKey = "AIzaSyBOaIUBsLfo3hXsPJq8YaA1-iWu1Faex5U";

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

            CheckUserMode();
        }

        private void CheckUserMode()
        {
            string typeString = Preferences.Get("UserType", "");
            _isSeeker = (typeString == "LostPetSeeker");

            if (_isSeeker)
            {
                PageTitle = "Report Lost Pet";
                ButtonText = "Report Missing";
                Status = "Missing";
            }
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
                                "AI Analysis",
                                "Do you want to run a breed analysis on this front-view photo?",
                                "Yes", "No");
                            if (answer) await AnalyzeImageWithGemini(result);
                            break;
                        case "2": _file2 = result; PetImageSource2 = imgSource; break;
                        case "3": _file3 = result; PetImageSource3 = imgSource; break;
                        case "4": _file4 = result; PetImageSource4 = imgSource; break;
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Photo selection failed: " + ex.Message, "OK");
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
                string base64Image = Convert.ToBase64String(memoryStream.ToArray());

                string mimeType = "image/jpeg";
                if (file.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) mimeType = "image/png";
                else if (file.FileName.EndsWith(".heic", StringComparison.OrdinalIgnoreCase)) mimeType = "image/heic";
                else if (file.FileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) mimeType = "image/webp";

                var promptText = "Analyze the dog in this photo strictly. Identify the breed accurately.\n" +
                                         "Return ONLY a pure JSON object in English without markdown code blocks.\n\n" +
                                         "JSON Format:\n" +
                                         "{\n" +
                                         "\"breed\": \"Specific Breed Name\",\n" +
                                         "\"age\": \"Estimated age (Number ONLY, e.g., 3)\",\n" +  // 수정됨
                                         "\"weight\": \"Estimated weight in kg (Number ONLY, e.g., 5)\",\n" + // 수정됨
                                         "\"condition\": \"Brief health/physical condition\",\n" +
                                         "\"feature\": \"Distinctive visual features\"\n" +
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
                                new { inline_data = new { mime_type = mimeType, data = base64Image } } // [수정] mimeType 변수 사용
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
                        var text = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                        var cleanJson = text.Replace("```json", "").Replace("```", "").Trim();

                        try
                        {
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var petData = JsonSerializer.Deserialize<GeminiPetData>(cleanJson, options);
                            if (petData != null)
                            {
                                Species = petData.breed;
                                Age = petData.age;
                                Weight = petData.weight;
                                Condition = petData.condition;
                                Feature = petData.feature;
                                await Application.Current.MainPage.DisplayAlert("Analysis Complete", $"Identified as: {Species}", "OK");
                            }
                        }
                        catch { await Application.Current.MainPage.DisplayAlert("Error", "Failed to parse AI response.", "OK"); }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    await Application.Current.MainPage.DisplayAlert("API Failure", $"Code: {response.StatusCode}\nError: {errorContent}", "OK");
                }
            }
            catch (Exception ex) { await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK"); }
            finally { IsBusy = false; }
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
                    var location = await Geolocation.Default.GetLocationAsync();
                    if (location != null)
                    {
                        var placemarks = await Geocoding.Default.GetPlacemarksAsync(location.Latitude, location.Longitude);
                        var placemark = placemarks?.FirstOrDefault();
                        if (placemark != null)
                        {
                            Location = $"{placemark.AdminArea} {placemark.Locality} {placemark.Thoroughfare} ({placemark.PostalCode})";
                        }
                    }
                }
            }
            catch { Location = "Location not found."; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SavePet()
        {
            if (IsBusy)
            {
                await Application.Current.MainPage.DisplayAlert("Wait", "AI Analysis or image processing is in progress. Please wait a moment.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Species) || string.IsNullOrWhiteSpace(Gender))
            {
                await Application.Current.MainPage.DisplayAlert("Notice", "Name, Breed, and Gender are required.", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                if (_authClient.User == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", "You must be logged in.", "OK");
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

                // [매칭 로직 추가] Seeker라면 저장 후 매칭 제안
                if (_isSeeker)
                {
                    bool wantMatch = await Application.Current.MainPage.DisplayAlert(
                        "Matching Service",
                        "Want to browse animals currently under protection?",
                        "Yes", "No");

                    if (wantMatch)
                    {
                        // 메인 페이지로 이동하며 검색어 전달
                        var route = $"//MainPage?MatchSpecies={Species}&MatchLocation={Location}";
                        await Shell.Current.GoToAsync(route);
                    }
                    else
                    {
                        await Shell.Current.GoToAsync("//MainPage");
                    }
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Success", "Animal Registered Successfully.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
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
            return await new FirebaseStorage(Constants.FirebaseStorageBucket).Child("PetImages").Child(fileName).PutAsync(stream);
        }

        public class GeminiPetData { public string breed { get; set; } public string age { get; set; } public string weight { get; set; } public string condition { get; set; } public string feature { get; set; } }
    }
}