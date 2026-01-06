using _SPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Storage;
using Microsoft.Maui.Devices.Sensors;
using System.Text;
using System.Text.Json; // JSON 처리를 위한 필수 네임스페이스

namespace _SPS.ViewModels
{
    public partial class AddPetViewModel : ObservableObject
    {
        // [기본 정보]
        [ObservableProperty] private string name;
        [ObservableProperty] private string species;
        [ObservableProperty] private string age;
        [ObservableProperty] private string description;

        // [추가 정보]
        [ObservableProperty] private string weight;
        [ObservableProperty] private string condition;
        [ObservableProperty] private string feature;
        [ObservableProperty] private string contact;
        [ObservableProperty] private string location;

        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private ImageSource petImageSource;
        [ObservableProperty] private bool isImagePlaceholderVisible = true;

        private FileResult _selectedImageFile;
        private readonly FirebaseClient _dbClient;
        private readonly FirebaseAuthClient _authClient;

        // ★ [중요] 여기에 본인의 실제 Gemini API 키를 입력하세요!
        private const string GeminiApiKey = "AIzaSyBsI2Z_CsRTO_-R0-bS4dee33Tsfs-pOdg";

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
        private async Task PickImage()
        {
            try
            {
                var result = await MediaPicker.Default.PickPhotoAsync();
                if (result != null)
                {
                    _selectedImageFile = result;
                    var stream = await result.OpenReadAsync();
                    PetImageSource = ImageSource.FromStream(() => stream);

                    IsImagePlaceholderVisible = false;

                    bool answer = await Application.Current.MainPage.DisplayAlert(
                        "AI 분석", "사진을 분석하여 정보를 자동으로 입력할까요?", "예", "아니요");

                    if (answer)
                    {
                        await AnalyzeImageWithGemini(result);
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("오류", "사진 선택 실패: " + ex.Message, "확인");
            }
        }

        // [핵심 기능] Gemini에게 사진 보내고 정보 받아오기
        private async Task AnalyzeImageWithGemini(FileResult file)
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. 이미지를 데이터로 변환
                using var stream = await file.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                byte[] imageBytes = memoryStream.ToArray();
                string base64Image = Convert.ToBase64String(imageBytes);

                // 2. 프롬프트(질문) 설정 - JSON 형식으로 달라고 강력하게 요청
                var promptText = "Analyze the dog in this photo and return ONLY a JSON object in English. Do not say anything else.\n\n" +
                                 "Format:\n" +
                                 "{\n" +
                                 "\"breed\": \"Dog breed (e.g., Golden Retriever)\",\n" +
                                 "\"age\": \"Estimated age (numbers only, e.g., 3)\",\n" +
                                 "\"weight\": \"Estimated weight in kg (numbers only, e.g., 15.5)\",\n" +
                                 "\"condition\": \"Brief health condition in English (e.g., Healthy coat and looks well)\",\n" +
                                 "\"feature\": \"Notable features in English (e.g., Floppy ears and gentle-looking)\"\n" +
                                 "}";

                // 3. API 요청 본문 만들기
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

                // 4. Gemini API 호출
                using var client = new HttpClient();
                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-pro:generateContent?key={GeminiApiKey}", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var resultJson = await response.Content.ReadAsStringAsync();

                    // 5. 응답 파싱 시작!
                    using var doc = JsonDocument.Parse(resultJson);
                    var candidates = doc.RootElement.GetProperty("candidates");

                    if (candidates.GetArrayLength() > 0)
                    {
                        // Gemini의 실제 답변 텍스트 추출
                        var text = candidates[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();

                        // 마크다운(```json) 제거 및 공백 정리
                        var cleanJson = text.Replace("```json", "").Replace("```", "").Trim();

                        // JSON을 C# 객체로 변환
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var petData = JsonSerializer.Deserialize<GeminiPetData>(cleanJson, options);

                        if (petData != null)
                        {
                            // 6. 화면에 값 채워넣기 (자동 입력)
                            Species = petData.breed;
                            Age = petData.age;
                            Weight = petData.weight;
                            Condition = petData.condition;
                            Feature = petData.feature;

                            await Application.Current.MainPage.DisplayAlert("성공", "AI가 분석한 정보가 입력되었습니다.", "확인");
                        }
                    }
                }
                else
                {
                    // API 호출 실패 시
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    await Application.Current.MainPage.DisplayAlert("API 오류", $"응답 코드: {response.StatusCode}\n{errorMsg}", "확인");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("분석 실패", ex.Message, "확인");
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
                    Location = "위치 권한이 없습니다.";
                }
            }
            catch
            {
                Location = "위치를 찾을 수 없습니다.";
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
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Species))
            {
                await Application.Current.MainPage.DisplayAlert("알림", "이름과 종은 필수입니다.", "확인");
                return;
            }

            IsBusy = true;
            try
            {
                if (_authClient.User == null)
                {
                    await Application.Current.MainPage.DisplayAlert("오류", "로그인 정보가 없습니다.", "확인");
                    return;
                }

                string imageUrl = "";
                if (_selectedImageFile != null)
                {
                    using var stream = await _selectedImageFile.OpenReadAsync();
                    var fileName = $"{Guid.NewGuid()}.png";
                    var storageTask = new FirebaseStorage(Constants.FirebaseStorageBucket)
                        .Child("PetImages").Child(fileName).PutAsync(stream);
                    imageUrl = await storageTask;
                }

                var newPet = new PetModel
                {
                    Name = Name,
                    Species = Species,
                    Age = Age,
                    Weight = Weight,
                    Condition = Condition,
                    Feature = Feature,
                    Contact = Contact,
                    Location = Location,
                    Description = Description,
                    OwnerId = _authClient.User.Uid,
                    ImageUrl = imageUrl
                };

                await _dbClient.Child("Pets").PostAsync(newPet);
                await Application.Current.MainPage.DisplayAlert("성공", "등록되었습니다.", "확인");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("오류", ex.Message, "확인");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // [내부 클래스] JSON 데이터를 받기 위한 그릇
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