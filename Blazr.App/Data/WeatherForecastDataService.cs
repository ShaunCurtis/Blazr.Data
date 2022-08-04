namespace Blazr.App.Data;

public class WeatherForecastDataService
{
    private List<WeatherForecast>? WeatherForecasts { get; set; }

    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    public ValueTask<RecordCommandResult> AddRecordAsync(WeatherForecast record)
    {
        if (WeatherForecasts is not null)
        {
            var insertRecord = new WeatherForecast
            {
                Date = record.Date,
                TemperatureC = record.TemperatureC,
                Summary = record.Summary
            };

            WeatherForecasts.Add(insertRecord);

            return ValueTask.FromResult(RecordCommandResult.Successful());
        }

        return ValueTask.FromResult(RecordCommandResult.Failure("Can't add record"));
    }

    public ValueTask<RecordListResult<WeatherForecast>> GetRecordsAsync()
    {
        if (WeatherForecasts is null)
            GetForecasts();

        var list = new List<WeatherForecast>();
        foreach (var item in WeatherForecasts!)
            list.Add(new WeatherForecast
            {
                Date = item.Date,
                TemperatureC = item.TemperatureC,
                Summary = item.Summary
            });

        return ValueTask.FromResult( RecordListResult<WeatherForecast>.Successful(list.AsEnumerable()));
    }

    private void GetForecasts()
    {
        var startDate = DateTime.Now;
        this.WeatherForecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = startDate.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }).ToList();
    }
}
