using System.Collections.ObjectModel;
using System.Net.Http.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using SmartAgro.Shared;
using Color = Microsoft.Maui.Graphics.Color;

namespace SmartAgro.Frontend
{
    public partial class MainPage : ContentPage
    {
        private readonly HttpClient _httpClient;
        private readonly IDispatcherTimer _timer;
        private bool _isLoading;

        // Зберігаємо всі завантажені дані для миттєвої фільтрації без зайвих запитів
        private List<SensorReading> _allReadings = new();
        private string _currentFilter = "All"; // "All", "Hot", "Dry"

        public ObservableCollection<SensorReading> DisplayedReadings { get; set; } = new();

        // Автоматичний вибір адреси: 10.0.2.2 для Android-емулятора, localhost для Windows
        private string ApiUrl => DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5297/api/microclimate"
            : "http://localhost:5297/api/microclimate";

        public MainPage()
        {
            InitializeComponent();
            ReadingsCollectionView.ItemsSource = DisplayedReadings;

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(5);
            _timer.Tick += async (s, e) => await LoadDataAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDataAsync();

            if (AutoRefreshSwitch.IsToggled)
                _timer.Start();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _timer.Stop();
        }

        // --- 1. ЗАВАНТАЖЕННЯ ДАНИХ З БЕКЕНДУ (GET) ---
        private async Task LoadDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                var data = await _httpClient.GetFromJsonAsync<List<SensorReading>>(ApiUrl);

                if (data != null && data.Count > 0)
                {
                    foreach (var item in data)
                    {
                        item.CreatedAt = item.CreatedAt.ToLocalTime();
                    }

                    _allReadings = data;

                    // Оновлюємо верхні картки та індикатор стану за найновішим записом
                    UpdateDashboardMetrics(_allReadings.First());

                    // Застосовуємо обраний фільтр до списку
                    ApplyFilter();

                    ConnectionStatusLabel.Text = $"🟢 Онлайн • Оновлено о {DateTime.Now:HH:mm:ss} (Записів: {_allReadings.Count})";
                    ConnectionStatusLabel.TextColor = Color.FromArgb("#2D6A4F");
                }
                else
                {
                    ConnectionStatusLabel.Text = "🟡 Підключено, але база даних порожня";
                    ConnectionStatusLabel.TextColor = Color.FromArgb("#D97706");
                }
            }
            catch (Exception)
            {
                ConnectionStatusLabel.Text = $"🔴 Помилка підключення до {ApiUrl}";
                ConnectionStatusLabel.TextColor = Color.FromArgb("#E63946");
            }
            finally
            {
                _isLoading = false;
            }
        }

        // --- 2. ОНОВЛЕННЯ КАРТОК, ПРОГРЕС-БАРІВ ТА РОЗУМНОГО АЛЕРТУ ---
        private void UpdateDashboardMetrics(SensorReading latest)
        {
            CurrentTempLabel.Text = $"{latest.Temperature:F1} °C";
            CurrentHumLabel.Text = $"{latest.SoilHumidity:F1} %";
            CurrentLightLabel.Text = $"{latest.LightLevel} lx";

            // Нормалізуємо значення від 0.0 до 1.0 для ProgressBar
            TempProgressBar.Progress = Math.Clamp((latest.Temperature - 10.0) / 30.0, 0.0, 1.0);
            HumProgressBar.Progress = Math.Clamp(latest.SoilHumidity / 100.0, 0.0, 1.0);
            LightProgressBar.Progress = Math.Clamp(latest.LightLevel / 1000.0, 0.0, 1.0);

            // Логіка визначення критичних станів
            if (latest.Temperature > 28.0 && latest.SoilHumidity < 45.0)
            {
                SetAlertState("🚨", "Критичний стан: Спека та Посуха!",
                    $"Температура {latest.Temperature:F1}°C, вологість впала до {latest.SoilHumidity:F1}%. Негайно увімкніть полив та вентиляцію!",
                    "#FFE3E3", "#FFA8A8", "#C92A2A");
            }
            else if (latest.Temperature > 28.0)
            {
                SetAlertState("🔥", "Увага: Перегрів теплиці!",
                    $"Поточна температура ({latest.Temperature:F1}°C) перевищує норму (28°C). Рекомендується провітрювання.",
                    "#FFF4E6", "#FFD8A8", "#D9480F");
            }
            else if (latest.SoilHumidity < 45.0)
            {
                SetAlertState("🌵", "Низька вологість ґрунту!",
                    $"Вологість становить лише {latest.SoilHumidity:F1}% (норма від 45%). Час запустити крапельний полив.",
                    "#E7F5FF", "#A5D8FF", "#1864AB");
            }
            else
            {
                SetAlertState("✅", "Мікроклімат у нормі",
                    "Усі показники знаходяться в оптимальних межах для росту рослин.",
                    "#D8F3DC", "#95D5B2", "#1B4332");
            }
        }

        private void SetAlertState(string icon, string title, string subtitle, string bgHex, string strokeHex, string textHex)
        {
            AlertIcon.Text = icon;
            AlertTitle.Text = title;
            AlertSubtitle.Text = subtitle;
            AlertBanner.BackgroundColor = Color.FromArgb(bgHex);
            AlertBanner.Stroke = Color.FromArgb(strokeHex);
            AlertTitle.TextColor = Color.FromArgb(textHex);
            AlertSubtitle.TextColor = Color.FromArgb(textHex);
        }

        // --- 3. РУЧНИЙ СИМУЛЯТОР ДАТЧИКА (POST-ЗАПИТ) ---
        private void OnToggleSimulatorClicked(object sender, EventArgs e)
        {
            SimulatorPanel.IsVisible = !SimulatorPanel.IsVisible;
            ToggleSimulatorBtn.Text = SimulatorPanel.IsVisible ? "Згорнути ▲" : "Розгорнути ▼";
        }

        private void OnSimSliderChanged(object sender, ValueChangedEventArgs e)
        {
            if (SimTempValueLabel == null) return; // Захист під час ініціалізації XAML

            SimTempValueLabel.Text = $"{SimTempSlider.Value:F1} °C";
            SimHumValueLabel.Text = $"{SimHumSlider.Value:F1} %";
            SimLightValueLabel.Text = $"{(int)SimLightSlider.Value} lx";
        }

        private async void OnSendCustomReadingClicked(object sender, EventArgs e)
        {
            var customReading = new SensorReading
            {
                Temperature = Math.Round(SimTempSlider.Value, 1),
                SoilHumidity = Math.Round(SimHumSlider.Value, 1),
                LightLevel = (int)SimLightSlider.Value,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(ApiUrl, customReading);
                if (response.IsSuccessStatusCode)
                {
                    await LoadDataAsync(); // Одразу оновлюємо екран після відправки
                }
                else
                {
                    await DisplayAlert("Помилка", $"Сервер повернув код: {response.StatusCode}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Помилка з'єднання", $"Не вдалося відправити дані: {ex.Message}", "OK");
            }
        }

        // --- 4. ФІЛЬТРАЦІЯ ІСТОРІЇ ---
        private void OnFilterAllClicked(object sender, EventArgs e) => SetFilter("All");
        private void OnFilterHotClicked(object sender, EventArgs e) => SetFilter("Hot");
        private void OnFilterDryClicked(object sender, EventArgs e) => SetFilter("Dry");

        private void SetFilter(string filter)
        {
            _currentFilter = filter;

            // Оновлюємо кольори кнопок
            FilterAllBtn.BackgroundColor = filter == "All" ? Color.FromArgb("#2D6A4F") : Color.FromArgb("#E9ECEF");
            FilterAllBtn.TextColor = filter == "All" ? Colors.White : Color.FromArgb("#495057");

            FilterHotBtn.BackgroundColor = filter == "Hot" ? Color.FromArgb("#E63946") : Color.FromArgb("#E9ECEF");
            FilterHotBtn.TextColor = filter == "Hot" ? Colors.White : Color.FromArgb("#495057");

            FilterDryBtn.BackgroundColor = filter == "Dry" ? Color.FromArgb("#0077B6") : Color.FromArgb("#E9ECEF");
            FilterDryBtn.TextColor = filter == "Dry" ? Colors.White : Color.FromArgb("#495057");

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<SensorReading> filtered = _currentFilter switch
            {
                "Hot" => _allReadings.Where(r => r.Temperature > 28.0),
                "Dry" => _allReadings.Where(r => r.SoilHumidity < 45.0),
                _ => _allReadings
            };

            DisplayedReadings.Clear();
            foreach (var item in filtered)
            {
                DisplayedReadings.Add(item);
            }
        }

        // --- 5. КЛІК НА КАРТКУ В ІСТОРІЇ (ДЕТАЛЬНИЙ ЗВІТ) ---
        private async void OnReadingSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is SensorReading selected)
            {
                // Скидаємо виділення, щоб можна було клікнути повторно
                ReadingsCollectionView.SelectedItem = null;

                string statusAdvice = "Показники в межах норми.";
                if (selected.Temperature > 28.0) statusAdvice = "⚠️ Зафіксовано перевищення температури!";
                if (selected.SoilHumidity < 45.0) statusAdvice += "\n⚠️ Зафіксовано дефіцит вологи в ґрунті!";

                await DisplayAlert(
                    $"Запис #{selected.Id} ({selected.CreatedAt:HH:mm:ss})",
                    $"📅 Дата: {selected.CreatedAt:dd.MM.yyyy HH:mm:ss}\n" +
                    $"🌡️ Температура: {selected.Temperature:F1} °C\n" +
                    $"💧 Вологість ґрунту: {selected.SoilHumidity:F1} %\n" +
                    $"☀️ Освітлення: {selected.LightLevel} lx\n\n" +
                    $"{statusAdvice}",
                    "Закрити");
            }
        }

        // --- 6. КЕРУВАННЯ ТАЙМЕРОМ ТА КНОПКОЮ ОНОВЛЕННЯ ---
        private void OnAutoRefreshToggled(object sender, ToggledEventArgs e)
        {
            if (e.Value) _timer.Start();
            else _timer.Stop();
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await LoadDataAsync();
        }
    }
}