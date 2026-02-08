using _SPS.Views;

namespace _SPS
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage)); // ★ 이 줄이 반드시 있어야 합니다!
            Routing.RegisterRoute(nameof(PetDetailPage), typeof(PetDetailPage));
            Routing.RegisterRoute(nameof(AddPetPage), typeof(AddPetPage));
            Routing.RegisterRoute(nameof(MyUploadsPage), typeof(MyUploadsPage));
        }
    }
}