using _SPS.Models;
using _SPS.Views;
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

        // 화면에 보여줄 내 동물 목록
        public ObservableCollection<PetModel> MyPets { get; } = new();

        private readonly FirebaseClient _dbClient;
        private readonly FirebaseAuthClient _authClient;

        public MyUploadsViewModel()
        {
            _dbClient = new FirebaseClient(Constants.FirebaseDatabaseUrl);

            // 현재 로그인한 사용자 정보를 가져오기 위해 필요
            var config = new FirebaseAuthConfig
            {
                ApiKey = Constants.FirebaseApiKey,
                AuthDomain = Constants.AuthDomain,
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };
            _authClient = new FirebaseAuthClient(config);
        }

        // [핵심 기능] 내가 등록한 동물만 불러오기
        public async Task LoadMyPets()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. 현재 로그인된 내 ID 확인
                var myUid = _authClient.User?.Uid;
                if (string.IsNullOrEmpty(myUid))
                {
                    await Application.Current.MainPage.DisplayAlert("오류", "로그인 정보가 없습니다.", "확인");
                    return;
                }

                // 2. 전체 데이터 가져오기
                var collection = await _dbClient
                    .Child("Pets")
                    .OnceAsync<PetModel>();

                MyPets.Clear();

                // 3. 내 ID(OwnerId)와 일치하는 것만 리스트에 담기
                foreach (var item in collection)
                {
                    var pet = item.Object;
                    pet.Key = item.Key;

                    if (pet.OwnerId == myUid) // ★ 여기서 필터링!
                    {
                        MyPets.Add(pet);
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("오류", "불러오기 실패: " + ex.Message, "확인");
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
                { "IsReadOnly", false } // ★ 내 글 관리에서는 수정 가능!
            };
            await Shell.Current.GoToAsync(nameof(Views.PetDetailPage), param);
        }
    }
}