using System.Collections.ObjectModel;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using SmartAgro.Shared;
using Color = Microsoft.Maui.Graphics.Color;

namespace SmartAgro.Frontend
{
    // Допоміжна модель для верхніх карток об'єктів
    public class SensorOverviewItem
    {
        public int SensorId { get; set; }
        public string SensorName { get; set; } = string.Empty;
        public string TempDisplay { get; set; } = "-- °C";
        public string HumDisplay { get; set; } = "-- %";
        public string LightDisplay { get; set; } = "-- lx";
        public string LastUpdatedDisplay { get; set; } = "Немає даних";
        public string StatusText { get; set; } = "Очікування";
        public Color BadgeColor { get; set; } = Color.FromArgb("#6C757D");
        public Color CardBackground { get; set; } = Colors.White;
        public Color CardBorder { get; set; } = Color.FromArgb("#E0E0E0");
    }

    // Допоміжна модель для рядка історії (з назвою об'єкта)
    public class ReadingDisplayItem
    {
        public int Id { get; set; }
        public int SensorId { get; set; }
        public string SensorName { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public double SoilHumidity { get; set; }
        public double LightLevel { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public partial class MainPage : ContentPage
    {
        private readonly HttpClient _httpClient;
        private HubConnection? _hubConnection;
        private bool _isLoading;

        private string BaseUrl => DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5297"
            : "http://localhost:5297";

        private List<SensorConfig> _sensors = new();
        private List<SensorReading> _allReadings = new();

        private int? _selectedSensorId = null; // null = Всі об'єкти
        private string _currentStateFilter = "All"; // "All", "Hot", "Dry"

        public ObservableCollection<SensorOverviewItem> SensorCards { get; set; } = new();
        public ObservableCollection<ReadingDisplayItem> DisplayedReadings { get; set; } = new();

        public MainPage()
        {
            InitializeComponent();

            SensorsOverviewCollection.ItemsSource = SensorCards;
            ReadingsCollectionView.ItemsSource = DisplayedReadings;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadEverythingAsync();
            await ConnectSignalRAsync();
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }
        }

        // --- 1. ПІДКЛЮЧЕННЯ ДО SIGNALR ДЛЯ МИТТЄВОГО ОНОВЛЕННЯ ---
        private async Task ConnectSignalRAsync()
        {
            if (_hubConnection != null) return;

            try
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl($"{BaseUrl}/hubs/microclimate")
                    .WithAutomaticReconnect()
                    .Build();

                // Слухаємо подію "ReceiveNewReading", яку відправляє MicroclimateController
                _hubConnection.On<SensorReading>("ReceiveNewReading", (newReading) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        newReading.CreatedAt = newReading.CreatedAt.ToLocalTime();

                        // Додаємо новий запис на початок списку
                        _allReadings.Insert(0, newReading);
                        if (_allReadings.Count > 200)
                            _allReadings.RemoveAt(_allReadings.Count - 1);

                        // Переконуємося, що такий об'єкт є у списку конфігурації
                        EnsureSensorExists(newReading.SensorId);

                        // Оновлюємо картки об'єктів та історію
                        RebuildOverviewCards();
                        ApplyFilters();

                        ConnectionStatusLabel.Text = $"🟢 Live-подія від об'єкта #{newReading.SensorId} о {DateTime.Now:HH:mm:ss}";
                        ConnectionStatusLabel.TextColor = Color.FromArgb("#2D6A4F");
                    });
                });

                await _hubConnection.StartAsync();
                SignalRStatusLabel.Text = "⚡ SignalR: Підключено (Real-Time)";
                SignalRStatusLabel.TextColor = Color.FromArgb("#2D6A4F");
            }
            catch (Exception)
            {
                SignalRStatusLabel.Text = "⚠️ SignalR: Офлайн";
                SignalRStatusLabel.TextColor = Color.FromArgb("#E63946");
            }
        }

        // --- 2. ЗАВАНТАЖЕННЯ КОНФІГУРАЦІЇ ОБ'ЄКТІВ (/api/sensors) ТА ІСТОРІЇ (/api/microclimate) ---
        private async Task LoadEverythingAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                // 1. Завантажуємо список об'єктів (SensorConfig)
                try
                {
                    var configs = await _httpClient.GetFromJsonAsync<List<SensorConfig>>($"{BaseUrl}/api/sensors");
                    if (configs != null && configs.Count > 0)
                    {
                        _sensors = configs;
                    }
                }
                catch
                {
                    // Якщо /api/sensors ще порожній або недоступний, побудуємо список із наявних SensorId в історії
                }

                // 2. Завантажуємо останні 50 записів мікроклімату
                var history = await _httpClient.GetFromJsonAsync<List<SensorReading>>($"{BaseUrl}/api/microclimate");
                if (history != null)
                {
                    foreach (var item in history)
                    {
                        item.CreatedAt = item.CreatedAt.ToLocalTime();
                        EnsureSensorExists(item.SensorId);
                    }
                    _allReadings = history;
                }

                // Оновлюємо випадаючий список у симуляторі
                SimSensorPicker.ItemsSource = null;
                SimSensorPicker.ItemsSource = _sensors;
                if (_sensors.Count > 0 && SimSensorPicker.SelectedIndex == -1)
                    SimSensorPicker.SelectedIndex = 0;

                RebuildOverviewCards();
                ApplyFilters();

                ConnectionStatusLabel.Text = $"🟢 Онлайн • Об'єктів: {_sensors.Count} • Записів: {_allReadings.Count}";
                ConnectionStatusLabel.TextColor = Color.FromArgb("#2D6A4F");
            }
            catch (Exception ex)
            {
                ConnectionStatusLabel.Text = $"🔴 Помилка підключення: {ex.Message}";
                ConnectionStatusLabel.TextColor = Color.FromArgb("#E63946");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void EnsureSensorExists(int sensorId)
        {
            if (!_sensors.Any(s => s.Id == sensorId))
            {
                _sensors.Add(new SensorConfig
                {
                    Id = sensorId,
                    Name = $"Об'єкт #{sensorId}"
                });
            }
        }

        private string GetSensorName(int sensorId)
        {
            return _sensors.FirstOrDefault(s => s.Id == sensorId)?.Name ?? $"Об'єкт #{sensorId}";
        }

        // --- 3. ПОБУДОВА ВЕРХНІХ КАРТОК ДЛЯ КОЖНОГО ОБ'ЄКТА ---
        private void RebuildOverviewCards()
        {
            SensorCards.Clear();

            foreach (var sensor in _sensors.OrderBy(s => s.Id))
            {
                var latest = _allReadings.FirstOrDefault(r => r.SensorId == sensor.Id);
                bool isSelected = _selectedSensorId == sensor.Id;

                var card = new SensorOverviewItem
                {
                    SensorId = sensor.Id,
                    SensorName = sensor.Name,
                    CardBackground = isSelected ? Color.FromArgb("#E8F5E9") : Colors.White,
                    CardBorder = isSelected ? Color.FromArgb("#2D6A4F") : Color.FromArgb("#E0E0E0")
                };

                if (latest != null)
                {
                    card.TempDisplay = $"{latest.Temperature:F1} °C";
                    card.HumDisplay = $"{latest.SoilHumidity:F1} %";
                    card.LightDisplay = $"{latest.LightLevel:F0} lx";
                    card.LastUpdatedDisplay = $"Оновлено о {latest.CreatedAt:HH:mm:ss}";

                    if (latest.Temperature > 28.0 && latest.SoilHumidity < 45.0)
                    {
                        card.StatusText = "🚨 Критично";
                        card.BadgeColor = Color.FromArgb("#C92A2A");
                    }
                    else if (latest.Temperature > 28.0)
                    {
                        card.StatusText = "🔥 Спека";
                        card.BadgeColor = Color.FromArgb("#D9480F");
                    }
                    else if (latest.SoilHumidity < 45.0)
                    {
                        card.StatusText = "🌵 Сухо";
                        card.BadgeColor = Color.FromArgb("#1864AB");
                    }
                    else
                    {
                        card.StatusText = "✅ Норма";
                        card.BadgeColor = Color.FromArgb("#2D6A4F");
                    }
                }

                SensorCards.Add(card);
            }
        }

        // --- 4. ВИБІР КОНКРЕТНОГО ОБ'ЄКТА ТА ФІЛЬТРАЦІЯ ІСТОРІЇ ---
        private void OnSensorCardSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is SensorOverviewItem selectedCard)
            {
                _selectedSensorId = selectedCard.SensorId;
                SelectedObjectBannerLabel.Text = $"Обрано: {selectedCard.SensorName} (ID: #{selectedCard.SensorId})";
                HistoryHeaderLabel.Text = $"📋 Історія телеметрії: {selectedCard.SensorName}";
                ShowAllObjectsBtn.BackgroundColor = Color.FromArgb("#6C757D");

                // Синхронізуємо вибір у симуляторі
                var matchingConfig = _sensors.FirstOrDefault(s => s.Id == selectedCard.SensorId);
                if (matchingConfig != null)
                    SimSensorPicker.SelectedItem = matchingConfig;

                SensorsOverviewCollection.SelectedItem = null;
                RebuildOverviewCards();
                ApplyFilters();
            }
        }

        private void OnShowAllObjectsClicked(object sender, EventArgs e)
        {
            _selectedSensorId = null;
            SelectedObjectBannerLabel.Text = "Обрано: Всі об'єкти";
            HistoryHeaderLabel.Text = "📋 Історія телеметрії (Всі об'єкти)";
            ShowAllObjectsBtn.BackgroundColor = Color.FromArgb("#1B4332");

            RebuildOverviewCards();
            ApplyFilters();
        }

        private void OnFilterAllClicked(object sender, EventArgs e) => SetStateFilter("All");
        private void OnFilterHotClicked(object sender, EventArgs e) => SetStateFilter("Hot");
        private void OnFilterDryClicked(object sender, EventArgs e) => SetStateFilter("Dry");

        private void SetStateFilter(string filter)
        {
            _currentStateFilter = filter;

            FilterAllBtn.BackgroundColor = filter == "All" ? Color.FromArgb("#2D6A4F") : Color.FromArgb("#E9ECEF");
            FilterAllBtn.TextColor = filter == "All" ? Colors.White : Color.FromArgb("#495057");

            FilterHotBtn.BackgroundColor = filter == "Hot" ? Color.FromArgb("#E63946") : Color.FromArgb("#E9ECEF");
            FilterHotBtn.TextColor = filter == "Hot" ? Colors.White : Color.FromArgb("#495057");

            FilterDryBtn.BackgroundColor = filter == "Dry" ? Color.FromArgb("#0077B6") : Color.FromArgb("#E9ECEF");
            FilterDryBtn.TextColor = filter == "Dry" ? Colors.White : Color.FromArgb("#495057");

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            IEnumerable<SensorReading> query = _allReadings;

            // Фільтр за обраним об'єктом
            if (_selectedSensorId.HasValue)
            {
                query = query.Where(r => r.SensorId == _selectedSensorId.Value);
            }

            // Фільтр за станом
            query = _currentStateFilter switch
            {
                "Hot" => query.Where(r => r.Temperature > 28.0),
                "Dry" => query.Where(r => r.SoilHumidity < 45.0),
                _ => query
            };

            DisplayedReadings.Clear();
            foreach (var r in query)
            {
                DisplayedReadings.Add(new ReadingDisplayItem
                {
                    Id = r.Id,
                    SensorId = r.SensorId,
                    SensorName = GetSensorName(r.SensorId),
                    Temperature = r.Temperature,
                    SoilHumidity = r.SoilHumidity,
                    LightLevel = r.LightLevel,
                    CreatedAt = r.CreatedAt
                });
            }
        }

        // --- 5. РУЧНИЙ СИМУЛЯТОР ДАТЧИКА З ПІДТРИМКОЮ [ApiKey] ---
        private void OnToggleSimulatorClicked(object sender, EventArgs e)
        {
            SimulatorPanel.IsVisible = !SimulatorPanel.IsVisible;
            ToggleSimulatorBtn.Text = SimulatorPanel.IsVisible ? "Згорнути ▲" : "Розгорнути ▼";
        }

        private void OnSimSliderChanged(object sender, ValueChangedEventArgs e)
        {
            if (SimTempValueLabel == null) return;
            SimTempValueLabel.Text = $"{SimTempSlider.Value:F1} °C";
            SimHumValueLabel.Text = $"{SimHumSlider.Value:F1} %";
            SimLightValueLabel.Text = $"{(int)SimLightSlider.Value} lx";
        }

        private async void OnSendCustomReadingClicked(object sender, EventArgs e)
        {
            int targetSensorId = (SimSensorPicker.SelectedItem as SensorConfig)?.Id ?? 1;

            var customReading = new SensorReading
            {
                SensorId = targetSensorId,
                Temperature = Math.Round(SimTempSlider.Value, 1),
                SoilHumidity = Math.Round(SimHumSlider.Value, 1),
                LightLevel = Math.Round(SimLightSlider.Value, 0),
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/microclimate")
                {
                    Content = JsonContent.Create(customReading)
                };

                // Передаємо X-Api-Key, бо на MicroclimateController стоїть атрибут [ApiKey]
                if (!string.IsNullOrWhiteSpace(ApiKeyEntry.Text))
                {
                    request.Headers.Add("X-Api-Key", ApiKeyEntry.Text.Trim());
                }

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Відмова сервера",
                        $"Код: {(int)response.StatusCode} ({response.StatusCode}).\nПеревір, чи правильно введено X-Api-Key у полі симулятора!",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Помилка з'єднання", ex.Message, "OK");
            }
        }

        // --- 6. ДЕТАЛЬНЕ ВІКНО ПРИ КЛІКУ НА ЗАПИС ІСТОРІЇ ---
        private async void OnReadingSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is ReadingDisplayItem selected)
            {
                ReadingsCollectionView.SelectedItem = null;

                string statusAdvice = "✅ Показники об'єкта в межах норми.";
                if (selected.Temperature > 28.0) statusAdvice = "🔥 Увага: Перевищення температури (>28°C)!";
                if (selected.SoilHumidity < 45.0) statusAdvice += "\n🌵 Увага: Дефіцит вологи в ґрунті (<45%)!";

                await DisplayAlert(
                    $"{selected.SensorName} (Запис #{selected.Id})",
                    $"🏢 Об'єкт: {selected.SensorName} (ID: {selected.SensorId})\n" +
                    $"📅 Час: {selected.CreatedAt:dd.MM.yyyy HH:mm:ss}\n\n" +
                    $"🌡️ Температура: {selected.Temperature:F1} °C\n" +
                    $"💧 Вологість ґрунту: {selected.SoilHumidity:F1} %\n" +
                    $"☀️ Освітлення: {selected.LightLevel:F0} lx\n\n" +
                    $"{statusAdvice}",
                    "Закрити");
            }
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await LoadEverythingAsync();
        }
    }
}