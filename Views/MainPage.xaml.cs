using _SPS.ViewModels;

namespace _SPS.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    // 화면이 나타날 때마다 실행되는 함수
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is MainViewModel viewModel)
        {
            // 1. 동물 목록 불러오기
            await viewModel.LoadPets();

            // 2. ★ [추가됨] 내 닉네임 불러오기
            await viewModel.LoadUserInfo();
        }
    }
}