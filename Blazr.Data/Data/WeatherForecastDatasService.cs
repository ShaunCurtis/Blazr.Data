namespace Blazr.Data.Data
{
    public class WeatherForecastDatasService
    {
        private List<WeatherForecast>? WeatherForecasts { get; set; }

        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        public ValueTask<bool> Add(WeatherForecast record)
        {
            if (WeatherForecasts is not null)
            {
                WeatherForecasts.Add(record);
                return ValueTask.FromResult(true);
            }

            return ValueTask.FromResult(false);
        }

        public ValueTask<IEnumerable<WeatherForecast>> GetForecastAsync()
        {
            if (WeatherForecasts is null)
                GetForecasts();

            var list = new List<WeatherForecast>();
            foreach (var item in WeatherForecasts!)
                list.Add(new WeatherForecast
                {
                    Uid = item.Uid,
                    Date = DateTime.Now,
                    TemperatureC = item.TemperatureC,
                    Summary = item.Summary
                });

            return ValueTask.FromResult(list.AsEnumerable());
        }

        public void GetForecasts()
        {
            var startDate = DateTime.Now;
            this.WeatherForecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Uid = Guid.NewGuid(),
                Date = startDate.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            }).ToList();
        }
    }
}