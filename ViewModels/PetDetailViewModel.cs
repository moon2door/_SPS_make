using _SPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query;
using System.Collections.ObjectModel;
using System.Net.Http;

namespace _SPS.ViewModels
{
    [QueryProperty(nameof(Pet), "Pet")]
    [QueryProperty(nameof(IsReadOnly), "IsReadOnly")] // ★ [추가] 읽기 전용 여부 받기
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

        // 내가 쓴 글인지 확인
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEdit))] // 값이 바뀌면 CanEdit도 다시 계산
        private bool isOwner;

        // ★ [추가] 읽기 전용 모드인지 확인 (Home에서 오면 True)
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEdit))] // 값이 바뀌면 CanEdit도 다시 계산
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

                // [추가] 성별 연결
                Gender = value.Gender;
                Status = value.Status;

                Age = value.Age;
                Description = value.Description;
                Weight = value.Weight;
                Condition = value.Condition;
                Feature = value.Feature;
                Contact = value.Contact;
                Location = value.Location;

                // [추가] 4장의 사진 중 있는 것만 골라서 리스트에 담기
                PetImages.Clear();
                if (!string.IsNullOrEmpty(value.ImageUrl1)) PetImages.Add(value.ImageUrl1);
                if (!string.IsNullOrEmpty(value.ImageUrl2)) PetImages.Add(value.ImageUrl2);
                if (!string.IsNullOrEmpty(value.ImageUrl3)) PetImages.Add(value.ImageUrl3);
                if (!string.IsNullOrEmpty(value.ImageUrl4)) PetImages.Add(value.ImageUrl4);

                // 만약 사진이 한 장도 없다면 기본 이미지(플레이스홀더)라도 하나 넣음
                if (PetImages.Count == 0) PetImages.Add("dotnet_bot.png");

                var myUid = _authClient.User?.Uid;
                IsOwner = !string.IsNullOrEmpty(myUid) && value.OwnerId == myUid;
            }
        }

        [RelayCommand]
        private async Task UpdatePet()
        {
            if (!CanEdit) return; // 이중 안전장치

            bool confirm = await Application.Current.MainPage.DisplayAlert("수정", "정보를 수정하시겠습니까?", "예", "아니요");
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
                await Application.Current.MainPage.DisplayAlert("성공", "수정되었습니다.", "확인");
                await Shell.Current.GoToAsync("..");
            }
        }

        [RelayCommand]
        private async Task DeletePet()
        {
            if (!CanEdit) return; // 이중 안전장치

            bool confirm = await Application.Current.MainPage.DisplayAlert("삭제", "정말 삭제하시겠습니까?", "삭제", "취소");
            if (confirm)
            {
                await _dbClient.Child("Pets").Child(Pet.Key).DeleteAsync();
                await Application.Current.MainPage.DisplayAlert("삭제됨", "삭제되었습니다.", "확인");
                await Shell.Current.GoToAsync("..");
            }
        }

        [RelayCommand]
        private async Task SharePet()
        {
            if (Pet == null) return;

            try
            {
                // 1. 공유할 텍스트 만들기 (해시태그 포함)
                string shareText = $"[{Pet.Status}] 가족을 찾습니다!\n\n" +
                                   $"🐶 이름: {Name}\n" +
                                   $"🐕 견종: {Species}\n" +
                                   $"📍 지역: {Location}\n" +
                                   $"📝 특징: {Feature}\n\n" +
                                   $"#유기견 #사지말고입양하세요 #강아지 #반려견 #{Species} #{Location}";

                // 2. 이미지가 있는 경우 이미지를 다운로드해서 공유
                string imagePath = null;
                if (!string.IsNullOrEmpty(Pet.ImageUrl1))
                {
                    // 로딩 표시 같은 게 필요하면 여기서 IsBusy = true;
                    using var client = new HttpClient();
                    var imageBytes = await client.GetByteArrayAsync(Pet.ImageUrl1);

                    // 캐시 폴더에 임시 파일로 저장
                    imagePath = Path.Combine(FileSystem.CacheDirectory, "share_pet.png");
                    File.WriteAllBytes(imagePath, imageBytes);
                }

                // 3. 공유 요청 실행
                if (imagePath != null)
                {
                    // 이미지 + 텍스트 공유
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "동물 정보 공유",
                        File = new ShareFile(imagePath),
                        PresentationSourceBounds = DeviceInfo.Platform == DevicePlatform.iOS && DeviceInfo.Idiom == DeviceIdiom.Tablet
                                                    ? new Rect(0, 20, 0, 0) // 아이패드 대응
                                                    : Rect.Zero
                    });
                }
                else
                {
                    // 이미지가 없으면 텍스트만 공유
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Text = shareText,
                        Title = "동물 정보 공유"
                    });
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("오류", "공유 중 문제가 발생했습니다: " + ex.Message, "확인");
            }
        }
    }
}