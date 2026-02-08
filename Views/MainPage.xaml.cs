namespace _SPS.Views
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // ViewModel의 비동기 메서드 호출
            if (BindingContext is ViewModels.MainViewModel viewModel)
            {
                await viewModel.OnAppearing();
            }
        }
    }
}