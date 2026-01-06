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

        // [추가된 속성] 성별 (Male / Female 등)
        [ObservableProperty] private string gender;

        [ObservableProperty] private string age;
        [ObservableProperty] private string description;
        [ObservableProperty] private string status = "보호중";

        // [추가 정보]
        [ObservableProperty] private string weight;
        [ObservableProperty] private string condition;
        [ObservableProperty] private string feature;
        [ObservableProperty] private string contact;
        [ObservableProperty] private string location;

        [ObservableProperty] private bool isBusy;

        // [수정] 사진 4장을 위한 이미지 소스 (화면 표시용)
        [ObservableProperty] private ImageSource petImageSource1; // 전면
        [ObservableProperty] private ImageSource petImageSource2; // 측면
        [ObservableProperty] private ImageSource petImageSource3; // 자유
        [ObservableProperty] private ImageSource petImageSource4; // 보호자와 함께

        // [수정] 실제 파일 데이터를 담아둘 변수 4개
        private FileResult _file1;
        private FileResult _file2;
        private FileResult _file3;
        private FileResult _file4;

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

        // [수정] 사진 선택 기능 (몇 번째 칸인지 slot으로 구분)
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
                        case "1": // 전면 사진 (AI 분석 대상)
                            _file1 = result;
                            PetImageSource1 = imgSource;

                            // 1번 사진일 때만 AI 분석 여부 묻기
                            bool answer = await Application.Current.MainPage.DisplayAlert(
                                "AI 분석", "이 사진(전면)으로 견종을 분석할까요?", "예", "아니요");
                            if (answer)
                            {
                                await AnalyzeImageWithGemini(result);
                            }
                            break;

                        case "2": // 측면
                            _file2 = result;
                            PetImageSource2 = imgSource;
                            break;

                        case "3": // 자유
                            _file3 = result;
                            PetImageSource3 = imgSource;
                            break;

                        case "4": // 보호자와 함께
                            _file4 = result;
                            PetImageSource4 = imgSource;
                            break;
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

                // 2. 프롬프트 설정 - JSON 형식 요청
                var promptText = "Analyze the dog in this photo and return ONLY a JSON object in English. Do not say anything else.\n\n" +
                                 "Format:\n" +
                                 "{\n" +
                                 "\"breed\": \"Dog breed (e.g., Golden Retriever)\",\n" +
                                 "\"age\": \"Estimated age (numbers only, e.g., 3)\",\n" +
                                 "\"weight\": \"Estimated weight in kg (numbers only, e.g., 15.5)\",\n" +
                                 "\"condition\": \"Brief health condition in English (e.g., Healthy coat)\",\n" +
                                 "\"feature\": \"Notable features in English (e.g., Floppy ears)\"\n" +
                                 "}";

                // 3. API 요청 본문
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

                // 4. API 호출
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
                            // 6. 화면에 값 자동 입력
                            Species = petData.breed;
                            Age = petData.age;
                            Weight = petData.weight;
                            Condition = petData.condition;
                            Feature = petData.feature;

                            await Application.Current.MainPage.DisplayAlert("성공", "AI 분석 완료!", "확인");
                        }
                    }
                }
                else
                {
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

            // [수정] 유효성 검사: 이름, 종, 성별 필수
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Species) || string.IsNullOrWhiteSpace(Gender))
            {
                await Application.Current.MainPage.DisplayAlert("알림", "이름, 종, 성별은 필수 입력 항목입니다.", "확인");
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

                // [수정] 4개의 이미지를 각각 업로드 (Helper 메서드 사용)
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

                    // [수정] 4개의 이미지 URL 저장
                    ImageUrl1 = url1,
                    ImageUrl2 = url2,
                    ImageUrl3 = url3,
                    ImageUrl4 = url4
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

        // [추가] 이미지 업로드 헬퍼 메서드
        private async Task<string> UploadImage(FileResult file)
        {
            if (file == null) return ""; // 파일이 없으면 빈 문자열 반환

            using var stream = await file.OpenReadAsync();
            var fileName = $"{Guid.NewGuid()}.png";

            // Firebase Storage에 업로드하고 다운로드 URL 반환
            return await new FirebaseStorage(Constants.FirebaseStorageBucket)
                .Child("PetImages").Child(fileName).PutAsync(stream);
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