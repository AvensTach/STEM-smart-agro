using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.SignalR.Client;
using SmartAgro.Shared;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Color = Microsoft.Maui.Graphics.Color;

namespace SmartAgro.Frontend;

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

    private int? _selectedSensorId = null;
    private string _currentStateFilter = "All";

    public ObservableCollection<SensorOverviewItem> SensorCards { get; set; } = new();
    public ObservableCollection<ReadingDisplayItem> DisplayedReadings { get; set; } = new();

    private ObservableCollection<DateTimePoint> _tempPoints = new();
    private ObservableCollection<DateTimePoint> _humPoints = new();

    public ObservableCollection<ISeries> TempSeries { get; set; }
    public ObservableCollection<ISeries> HumSeries { get; set; }
    public Axis[] XAxes { get; set; }

    public MainPage()
    {
        InitializeComponent();

        TempSeries = new ObservableCollection<ISeries>
        {
            new LineSeries<DateTimePoint>
            {
                Values = _tempPoints,
                Name = "Темп.",
                Stroke = new SolidColorPaint(SKColors.DarkRed) { StrokeThickness = 3 },
                GeometrySize = 6,
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometryStroke = new SolidColorPaint(SKColors.DarkRed) { StrokeThickness = 2 }
            }
        };

        HumSeries = new ObservableCollection<ISeries>
        {
            new LineSeries<DateTimePoint>
            {
                Values = _humPoints,
                Name = "Волог.",
                Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 3 },
                GeometrySize = 6,
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometryStroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 }
            }
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                
                Labeler = value =>
                {
                    if (double.IsNaN(value)) return "";
                    try
                    {
                        return new DateTime((long)value).ToString("HH:mm:ss");
                    }
                    catch
                    {
                        return "";
                    }
                },
                LabelsRotation = 45,
                TextSize = 10
            }
        };

        //BindingContext = this;
        //SensorsOverviewCollection.ItemsSource = SensorCards;
        //ReadingsCollectionView.ItemsSource = DisplayedReadings;

        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(100);

        
        BindingContext = this;
        SensorsOverviewCollection.ItemsSource = SensorCards;
        ReadingsCollectionView.ItemsSource = DisplayedReadings;

       
        await LoadEverythingAsync();
        await ConnectSignalRAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_hubConnection != null) await _hubConnection.DisposeAsync();
    }

    private async Task ConnectSignalRAsync()
    {
        if (_hubConnection != null) return;

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{BaseUrl}/hubs/microclimate", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.Reconnecting += (error) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SignalRStatusLabel.Text = "SignalR: Відновлення зв'язку...";
                    SignalRStatusLabel.TextColor = Color.FromArgb("#D97706");
                });
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += (connectionId) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SignalRStatusLabel.Text = "SignalR: Підключено (Real-Time)";
                    SignalRStatusLabel.TextColor = Color.FromArgb("#2D6A4F");
                });
                return Task.CompletedTask;
            };

            _hubConnection.Closed += (error) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SignalRStatusLabel.Text = "SignalR: Офлайн";
                    SignalRStatusLabel.TextColor = Color.FromArgb("#E63946");
                });
                return Task.CompletedTask;
            };

            _hubConnection.On<SensorReading>("ReceiveNewReading", (newReading) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    newReading.CreatedAt = newReading.CreatedAt.ToLocalTime();
                    _allReadings.Insert(0, newReading);
                    if (_allReadings.Count > 300) _allReadings.RemoveAt(_allReadings.Count - 1);

                    EnsureSensorExists(newReading.SensorId);
                    RebuildOverviewCards();
                    ApplyFilters();

                    if (_selectedSensorId == newReading.SensorId)
                    {
                        _tempPoints.Add(new DateTimePoint(newReading.CreatedAt, newReading.Temperature));
                        _humPoints.Add(new DateTimePoint(newReading.CreatedAt, newReading.SoilHumidity));

                        if (_tempPoints.Count > 20) _tempPoints.RemoveAt(0);
                        if (_humPoints.Count > 20) _humPoints.RemoveAt(0);
                    }

                    ConnectionStatusLabel.Text = $"SignalR: Нові дані (Об'єкт #{newReading.SensorId})";
                    ConnectionStatusLabel.TextColor = Color.FromArgb("#2D6A4F");
                });
            });

            await _hubConnection.StartAsync();
            SignalRStatusLabel.Text = "SignalR: Підключено (Real-Time)";
            SignalRStatusLabel.TextColor = Color.FromArgb("#2D6A4F");
        }
        catch (Exception)
        {
            SignalRStatusLabel.Text = "SignalR: Офлайн";
            SignalRStatusLabel.TextColor = Color.FromArgb("#E63946");
        }
    }

    private async Task LoadEverythingAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            if (string.IsNullOrEmpty(token))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try { await Shell.Current.GoToAsync("//LoginPage"); } catch { }
                });
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Отримуємо дані у фоні, щоб не блокувати UI
            List<SensorConfig> configs = null;
            try { configs = await _httpClient.GetFromJsonAsync<List<SensorConfig>>($"{BaseUrl}/api/sensors"); } catch { }

            var history = await _httpClient.GetFromJsonAsync<List<SensorReading>>($"{BaseUrl}/api/microclimate");

            // ПРИМУСОВО ОНОВЛЮЄМО ІНТЕРФЕЙС ТІЛЬКИ З ГОЛОВНОГО ПОТОКУ
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (configs != null) _sensors = configs;

                if (history != null && history.Count > 0)
                {
                    foreach (var item in history) { item.CreatedAt = item.CreatedAt.ToLocalTime(); EnsureSensorExists(item.SensorId); }
                    _allReadings = history;
                    ConnectionStatusLabel.Text = $"Онлайн • Об'єктів: {_sensors.Count} • Записів: {_allReadings.Count}";
                    ConnectionStatusLabel.TextColor = Color.FromArgb("#2D6A4F");
                }
                else
                {
                    _allReadings.Clear();
                    ConnectionStatusLabel.Text = "Очікування перших даних від датчиків...";
                    ConnectionStatusLabel.TextColor = Color.FromArgb("#D97706");
                }

                RebuildOverviewCards();
                ApplyFilters();
                UpdateCharts();
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ConnectionStatusLabel.Text = $"Помилка: {ex.Message}";
                ConnectionStatusLabel.TextColor = Color.FromArgb("#E63946");
            });
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void EnsureSensorExists(int sensorId)
    {
        if (!_sensors.Any(s => s.Id == sensorId))
            _sensors.Add(new SensorConfig { Id = sensorId, Name = $"Об'єкт #{sensorId}" });
    }

    private string GetSensorName(int sensorId) => _sensors.FirstOrDefault(s => s.Id == sensorId)?.Name ?? $"Об'єкт #{sensorId}";

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
                card.LastUpdatedDisplay = $"Оновлено {latest.CreatedAt:HH:mm:ss}";

                if (latest.Temperature > 28.0 && latest.SoilHumidity < 45.0) { card.StatusText = "Критично"; card.BadgeColor = Color.FromArgb("#C92A2A"); }
                else if (latest.Temperature > 28.0) { card.StatusText = "Спека"; card.BadgeColor = Color.FromArgb("#D9480F"); }
                else if (latest.SoilHumidity < 45.0) { card.StatusText = "Сухо"; card.BadgeColor = Color.FromArgb("#1864AB"); }
                else { card.StatusText = "Норма"; card.BadgeColor = Color.FromArgb("#2D6A4F"); }
            }
            SensorCards.Add(card);
        }
    }

    private void OnSensorCardSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is SensorOverviewItem selectedCard)
        {
            _selectedSensorId = selectedCard.SensorId;
            SelectedObjectBannerLabel.Text = $"Обрано: {selectedCard.SensorName}";
            ShowAllObjectsBtn.BackgroundColor = Color.FromArgb("#6C757D");
            SensorsOverviewCollection.SelectedItem = null;

            RebuildOverviewCards();
            ApplyFilters();
            UpdateCharts();
        }
    }

    private void OnShowAllObjectsClicked(object sender, EventArgs e)
    {
        _selectedSensorId = null;
        SelectedObjectBannerLabel.Text = "Обрано: Всі об'єкти";
        ShowAllObjectsBtn.BackgroundColor = Color.FromArgb("#1B4332");
        RebuildOverviewCards();
        ApplyFilters();
        UpdateCharts();
    }

    private void UpdateCharts()
    {
        _tempPoints.Clear();
        _humPoints.Clear();

        if (!_selectedSensorId.HasValue || !_allReadings.Any(r => r.SensorId == _selectedSensorId.Value))
        {
            ChartsPanel.IsVisible = false;
            return;
        }

        ChartsPanel.IsVisible = true;

        var historyForChart = _allReadings
            .Where(r => r.SensorId == _selectedSensorId.Value)
            .OrderBy(r => r.CreatedAt)
            .TakeLast(20);

        foreach (var r in historyForChart)
        {
            _tempPoints.Add(new DateTimePoint(r.CreatedAt, r.Temperature));
            _humPoints.Add(new DateTimePoint(r.CreatedAt, r.SoilHumidity));
        }
    }

    private void OnFilterAllClicked(object sender, EventArgs e) => SetStateFilter("All");
    private void OnFilterHotClicked(object sender, EventArgs e) => SetStateFilter("Hot");
    private void OnFilterDryClicked(object sender, EventArgs e) => SetStateFilter("Dry");

    private void SetStateFilter(string filter)
    {
        _currentStateFilter = filter;
        FilterAllBtn.BackgroundColor = filter == "All" ? Color.FromArgb("#2D6A4F") : Color.FromArgb("#E9ECEF");
        FilterHotBtn.BackgroundColor = filter == "Hot" ? Color.FromArgb("#E63946") : Color.FromArgb("#E9ECEF");
        FilterDryBtn.BackgroundColor = filter == "Dry" ? Color.FromArgb("#0077B6") : Color.FromArgb("#E9ECEF");
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<SensorReading> query = _allReadings;
        if (_selectedSensorId.HasValue) query = query.Where(r => r.SensorId == _selectedSensorId.Value);

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

    private async void OnAddNewSensorClicked(object sender, EventArgs e)
    {
        var sensorName = NewSensorNameEntry.Text;

        if (string.IsNullOrWhiteSpace(sensorName))
        {
            await DisplayAlert("Помилка", "Будь ласка, введіть назву об'єкта.", "ОК");
            return;
        }

        var newSensor = new SensorConfig { Name = sensorName };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/sensors", newSensor);

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Успіх", $"Об'єкт '{sensorName}' успішно додано!", "ОК");
                NewSensorNameEntry.Text = string.Empty;
                await LoadEverythingAsync();
            }
            else
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Помилка", $"Не вдалося додати об'єкт (Статус: {response.StatusCode}). {errorMsg}", "ОК");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Помилка з'єднання", ex.Message, "ОК");
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        SecureStorage.Default.Remove("jwt_token");

        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }

        await Shell.Current.GoToAsync("//LoginPage");
    }

    private void OnReadingSelected(object sender, SelectionChangedEventArgs e) => ReadingsCollectionView.SelectedItem = null;
    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadEverythingAsync();
}