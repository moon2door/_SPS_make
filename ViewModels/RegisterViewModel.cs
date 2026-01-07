using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query; 
using _SPS.Models; 

namespace _SPS.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private string nickname; 

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private bool isBusy;

        public bool CanExecute => !IsBusy;

        private readonly FirebaseAuthClient _authClient;
        private readonly FirebaseClient _dbClient;

        public RegisterViewModel()
        {
            var config = new FirebaseAuthConfig
            {
                ApiKey = Constants.FirebaseApiKey,
                AuthDomain = Constants.AuthDomain,
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };
            _authClient = new FirebaseAuthClient(config);

            _dbClient = new FirebaseClient(Constants.FirebaseDatabaseUrl);
        }

        [RelayCommand(CanExecute = nameof(CanExecute))]
        private async Task Register()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Nickname))
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Please fill in all fields.", "Confirmation");
                return;
            }

            IsBusy = true;

            try
            {
                var userCredential = await _authClient.CreateUserWithEmailAndPasswordAsync(Email, Password, Nickname);
                var uid = userCredential.User.Uid; 

                var newUser = new UserModel
                {
                    Uid = uid,
                    Nickname = Nickname,
                    CreationDate = DateTime.Now
                };

                await _dbClient
                    .Child("Users")
                    .Child(uid)
                    .PutAsync(newUser);

                await Application.Current.MainPage.DisplayAlert("Success", "Your registration has been completed.", "Go to Login");

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Registration failed", $"Error: {ex.Message}", "Confirmation");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}