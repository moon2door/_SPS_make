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
    [QueryProperty(nameof(IsReadOnly), "IsReadOnly")] // ★ [추가] 읽기 전용 여부 받기
    public partial class PetDetailViewModel : ObservableObject
    {
        [ObservableProperty] private PetModel pet;

        [ObservableProperty] private string name;
        [ObservableProperty] private string species;
        [ObservableProperty] private string gender;
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
    }
}