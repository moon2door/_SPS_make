using _SPS.Views; // Views 폴더 사용 선언

namespace _SPS;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // "RegisterPage"라는 이름으로 경로를 등록합니다.
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(AddPetPage), typeof(AddPetPage));
        Routing.RegisterRoute(nameof(PetDetailPage), typeof(PetDetailPage));
        Routing.RegisterRoute(nameof(MyUploadsPage), typeof(MyUploadsPage));
    }
}