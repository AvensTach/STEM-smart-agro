using System.Net.Http.Json;
using System.Text.Json;

namespace SmartAgro.Frontend;

public partial class LoginPage : ContentPage
{
    private readonly HttpClient _httpClient;
    private string BaseUrl => DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5297" : "http://localhost:5297";

    public LoginPage()
    {
        InitializeComponent();

        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Авто-логін: якщо токен вже є, одразу кидаємо на головний екран
        var token = await SecureStorage.Default.GetAsync("jwt_token");
        if (!string.IsNullOrEmpty(token))
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            await DisplayAlert("Помилка", "Введіть логін та пароль", "ОК");
            return;
        }

        LoginBtn.IsEnabled = false;
        LoginBtn.Text = "Вхід...";

        try
        {
            var userDto = new { Username = UsernameEntry.Text.Trim(), Password = PasswordEntry.Text };
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/auth/login", userDto);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<AuthResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result != null && !string.IsNullOrEmpty(result.Token))
                {
                    // Зберігаємо токен у захищене сховище пристрою
                    await SecureStorage.Default.SetAsync("jwt_token", result.Token);
                    await Shell.Current.GoToAsync("//MainPage");
                }
            }
            else
            {
                await DisplayAlert("Помилка входу", "Невірний логін або пароль", "ОК");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Помилка мережі", ex.Message, "ОК");
        }
        finally
        {
            LoginBtn.IsEnabled = true;
            LoginBtn.Text = "Увійти";
        }
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            await DisplayAlert("Помилка", "Введіть логін та пароль для реєстрації", "ОК");
            return;
        }

        RegisterBtn.IsEnabled = false;

        try
        {
            var userDto = new { Username = UsernameEntry.Text.Trim(), Password = PasswordEntry.Text };
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/auth/register", userDto);

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Успіх", "Акаунт створено! Тепер ви можете увійти.", "ОК");
            }
            else
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Помилка реєстрації", errorMsg, "ОК");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Помилка", ex.Message, "ОК");
        }
        finally
        {
            RegisterBtn.IsEnabled = true;
        }
    }

    private class AuthResponse
    {
        public string Token { get; set; }
    }
}