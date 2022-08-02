namespace Blazr.Data.Data;

public class WeatherForecastViewService
{
    private WeatherForecastDataService _dataService;

    public IEnumerable<WeatherForecast> Records { get; private set; } = Enumerable.Empty<WeatherForecast>();

    public WeatherForecast? Record { get; private set; }

    public string LatestErrorMessage { get; private set; } = string.Empty;

    public event EventHandler? ListUpdated;

    public WeatherForecastViewService(WeatherForecastDataService weatherForecastDataService)
        => _dataService = weatherForecastDataService;

    public async ValueTask<bool> GetRecordsAsync()
    {
        var result = await _dataService.GetRecordsAsync();
        this.LatestErrorMessage = result.Message;
        if (result.Success)
        {
            this.Records = result.Items;
            return true;
        }

        return false;
    }

    public async ValueTask<bool> AddRecordAsync()
    {
        this.Record = new WeatherForecast
        {
            Uid = Guid.NewGuid(),
            Date = DateTime.Now,
            TemperatureC = 20,
            Summary = "Testing"
        };
        var result = await _dataService.AddRecordAsync(Record);
        this.LatestErrorMessage = result.Message;

        if (result.Success)
        {
            if (await this.GetRecordsAsync())
                this.ListUpdated?.Invoke(this, EventArgs.Empty);
        }

        return result.Success;
    }
}
