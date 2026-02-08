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

        // [추가] 화면 제목과 버튼 텍스트를 동적으로 변경
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
        private bool _isSeeker; // 실종자 모드 여부

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

            // [추가] 유저 모드 체크
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
                Status = "Missing"; // 기본값 변경
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

                            // 실종자도 AI 분석을 쓸 수 있게 유지 (문구는 동일하게)
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

                var promptText = "Analyze the dog in this photo strictly. Identify the breed accurately.\n" +
                                 "Return ONLY a pure JSON object in English without markdown code blocks.\n\n" +
                                 "JSON Format:\n" +
                                 "{\n" +
                                 "\"breed\": \"Specific Breed Name\",\n" +
                                 "\"age\": \"Estimated age (e.g., 2 years)\",\n" +
                                 "\"weight\": \"Estimated weight (e.g., 12kg)\",\n" +
                                 "\"condition\": \"Brief health/physical condition\",\n" +
                                 "\"feature\": \"Distinctive visual features\"\n" +
                                 "}";

                var requestBody = new
                {
                    contents = new[] { new { parts = new object[] { new { text = promptText }, new { inline_data = new { mime_type = "image/jpeg", data = base64Image } } } } }
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
                else { await Application.Current.MainPage.DisplayAlert("API Error", "Failed to connect to AI service.", "OK"); }
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
            if (IsBusy) return;

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

                // [수정] Seeker 모드일 때 매칭 프로세스 시작
                if (_isSeeker)
                {
                    // 1. 매칭 제안 질문
                    bool wantMatch = await Application.Current.MainPage.DisplayAlert(
                        "Matching Service",
                        "Want to browse animals currently under protection?",
                        "Yes", "No");

                    if (wantMatch)
                    {
                        // 2. YES: 메인 화면으로 이동하면서 검색 파라미터 전달
                        // (현재 입력한 종과 위치 정보를 넘김)
                        var route = $"//MainPage?MatchSpecies={Species}&MatchLocation={Location}";
                        await Shell.Current.GoToAsync(route);
                    }
                    else
                    {
                        // 3. NO: 포기(로그아웃) vs 입양(MainPage)
                        string action = await Application.Current.MainPage.DisplayActionSheet(
                            "What would you like to do next?", "Cancel", null, "Logout", "Look for Adoption");

                        if (action == "Logout")
                        {
                            Preferences.Clear();
                            await Shell.Current.GoToAsync("//LoginPage");
                        }
                        else if (action == "Look for Adoption")
                        {
                            // 그냥 필터 없이 메인으로 이동
                            await Shell.Current.GoToAsync("//MainPage");
                        }
                    }
                }
                else
                {
                    // Shelter 모드: 기존대로 복귀
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