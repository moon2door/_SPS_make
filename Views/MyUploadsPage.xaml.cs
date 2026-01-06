using _SPS.ViewModels;

namespace _SPS.Views;

public partial class MyUploadsPage : ContentPage
{
    public MyUploadsPage()
    {
        InitializeComponent();
    }

    // 화면이 나타날 때마다 목록 새로고침
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MyUploadsViewModel vm)
        {
            await vm.LoadMyPets();
        }
    }
}