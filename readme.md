# Blazor - Get your Data out of Your Components

This article was sparked by the number of questions I see where the root cause of the problem is trying to manage data in UI components.  Here's a typical example:

```csharp
private WeatherForecast[]? forecasts;

protected override async Task OnInitializedAsync()
    => forecasts = await ForecastService.GetForecastAsync(DateTime.Now);
```

You may recognise this code.  It comes directly from `FetchData` in the Blazor templates: it's Microsoft distributed code.  Unfortunately that doesn't make it a good practice.  It just misleads.

So what should it look like?  This article keeps things as simple as possible.  The code is *For Demo Purposes*.  It's not production code because I've left out a lot of stuff that would make it more difficult to understand.  Read my footnote in the Appendix for the type of *stuff* that's missing.

## Repository

You can find the project and the latest version of this article at: https://github.com/ShaunCurtis/Blazr.Data

## Starting Point

The starting solution is the standard Blazor Server template.

Everything will run in a WASM project, but you should implement an API `WeatherForecastDataService` => Controller => Server `WeatherForecastDataService` data pipeline to emulate a real life application.

Debugging is also much easier and straight forward on Server.   Note: the solution is implemented with `Nullable` enabled. 

## The Solution

First we need to re-organise our services.  The UI is currently plugged directly into the back-end data service.  We need to build a  UI => ViewService => DataService => DataStore pipeline.

### WeatherForecast

Add a `Uid` field to provide a unique Id for the record.  All records should have some form of Id!

```csharp
public class WeatherForecast
{
    public Guid Uid { get; set; }
    public DateTime Date { get; set; }
    public int TemperatureC { get; set; }
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    public string? Summary { get; set; }
}
``` 

### WeatherForecastService

Rename `WeatherForecastService` to `WeatherForecastDataService`. 

It now:

1. Maintains an internal list of `WeatherForcast` objects.  When `GetRecordsAsync` is called, it provides a copy of this list, not a reference to the internal list.  This emulates what a ORM such as Entity Framework would do.
2. Returns result objects that contains both status information and data.
3. Returns `IEnumerable` collections: not lists or arrays.
4. Has an `AddRecordAsync` method to add a record to the "data store".

```csharp
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
                Uid = record.Uid,
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
                Uid = item.Uid,
                Date = DateTime.Now,
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
            Uid = Guid.NewGuid(),
            Date = startDate.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }).ToList();
    }
}
```

### WeatherForecastViewService

Add a new service class to provide the data to the UI.

It:

1. Gets the Data service through DI on object instantiation.
2. Holds the record collection.
3. Provides methods to get and add records.
4. Provides an event the UI can use for list update notifications.

```csharp
public class WeatherForecastViewService
{
    private WeatherForecastDataService _dataService;

    public WeatherForecastViewService(WeatherForecastDataService weatherForecastDataService)
        => _dataService = weatherForecastDataService;

    public IEnumerable<WeatherForecast> Records { get; private set; } = Enumerable.Empty<WeatherForecast>();

    public WeatherForecast? Record { get; private set; }

    public string LatestErroMessage { get; private set; } = string.Empty;

    public event EventHandler? ListUpdated;

    public async ValueTask<bool> GetRecordsAsync()
    {
        var result = await _dataService.GetRecordsAsync();
        this.LatestErroMessage = result.Message;
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
        this.LatestErroMessage = result.Message;

        if (result.Success)
        {
            if (await this.GetRecordsAsync())
                this.ListUpdated?.Invoke(this, EventArgs.Empty);
        }
        return result.Success;
    }
}
```

### Services

Make sure these two services are registered in `Program`. `WeatherForecastViewService` is Scoped, one per SPA session.  

```csharp
builder.Services.AddSingleton<WeatherForecastDataService>();
builder.Services.AddScoped<WeatherForecastViewService>();
```

### FetchData

We can now update `FetchData`.  It injects `WeatherForecastViewService`, loads the data in `OnInitializedAsync` and accesses the view record collection directly.  There's no data held in the UI.  

```csharp
@page "/fetchdata"
@using Blazr.Data.Data
@inject WeatherForecastViewService Service

<PageTitle>Weather forecast</PageTitle>

<h1>Weather Forecasts</h1>

<p>This component demonstrates fetching data from a service.</p>

@if (this.Service.Records == null)
{
    <p><em>Loading...</em></p>
}
else
{
    <table class="table">
        <thead>
            <tr>
                <th>Date</th>
                <th>Temp. (C)</th>
                <th>Temp. (F)</th>
                <th>Summary</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var forecast in this.Service.Records)
            {
                <tr>
                    <td>@forecast.Date.ToShortDateString()</td>
                    <td>@forecast.TemperatureC</td>
                    <td>@forecast.TemperatureF</td>
                    <td>@forecast.Summary</td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    protected override async Task OnInitializedAsync()
        => await Service.GetRecordsAsync();
}
```

## Adding an Editor

### WeatherForecastEditorForm

This form emulates an edit form.  It's designed to be *inline*, so has control over show/hide.  It could be a modal dialog.

It has one *Parameter*, no public properties and one public method.  The parent form communicates directly with it through the `Show` method and the component communicates back to the parent through the `FormClosed` callback.

The Add and Exit methods set `show` to false, and invoke the callback to inform the parent of closure.

The form injects `WeatherForecastViewService` and uses this to add the record to the data store.
 
```csharp
@inject WeatherForecastViewService Service 

@if (this.show)
{
    <div class="m-2 p-3 bg-light border border-1 border-primary">
    <h4>NewWeatherForecastForm</h4>
    <div class="container-fluid">
        <div class="row">
            <div class="col-12 text-secondary">
                Normally edit controls appear here
            </div>
        </div>
        <div class="row">
            <div class="col-12 text-end">
                <button class="btn btn-sm btn-success" @onclick=AddRecord>Add Record</button>
                <button class="btn btn-sm btn-dark" @onclick=Exit>Exit</button>
            </div>
        </div>
    </div>
    </div>
}

@code {
    [Parameter] public EventCallback FormClosed { get; set; }

    private bool show { get; set; } = false;

    private async Task AddRecord()
    {
        await Service.AddRecordAsync();
        show = false;
        await FormClosed.InvokeAsync();
    }

    private void Exit()
    {
        show = false;
        FormClosed.InvokeAsync();
    }

    public void ShowForm()
    {
        show = true;
        StateHasChanged();
    }
}
```

### WeatherForecasts

This is our new `FetchData`.  

There's:
1. A button block for *Add 
A New Record* which is controlled by `addForm`.
2. A `WeatherForecastEditorForm` referenced to a local private field.
3. An event receiver for the service `ListUpdated` event.
4. A receiver for the editor `FormClosed` callback.
5. `IDisposable` implemented to de-register the event handler correctly.

```csharp
@page "/fetchdata"
@using Blazr.Data.Data
@inject WeatherForecastViewService Service

<PageTitle>Weather forecast</PageTitle>

<WeatherForecastEditorForm @ref=this.form FormClosed=this.OnFormClosed />

@if (!addForm)
{
    <div class="container-fluid">
        <div class="row">
            <div class="col-12 text-end">
                <button class="btn btn-sm btn-primary" @onclick=ShowAddForm>Add A New Record</button>
            </div>
        </div>
    </div>
}

<h2>Weather Forecasts</h2>

//... the weather forecast table

@code {
    private bool addForm = false;
    private WeatherForecastEditorForm form = default!;

    protected override async Task OnInitializedAsync()
    {
        await Service.GetRecordsAsync();
        this.Service.ListUpdated += OnListUpdate;
    }

    private void ShowAddForm()
    {
        addForm = true;
        form.ShowForm();
    }

    private void OnFormClosed()
        => addForm = false;

    private void OnListUpdate(object? sender, EventArgs e)
        => this.InvokeAsync(StateHasChanged);

    public void Dispose()
        => this.Service.ListUpdated += OnListUpdate;
}
```

## Summary

This article shows how to move data management into a view service, and how to use an event driven model to update components.

It's very easy to shortcut the design process and start wiring components together.  But you soon end up with a unmanaged mess that's impossible to debug and cluttered with calls to `StateHasChanged` to try (and often fail to) keep everything in sync.

## Appendix

### What's missing

As I said in the introduction this code is *For Demo Purposes*.  There's nothing wrong with it: there are things missing that you would add/change in a production environment.  Here are a few examples:

1. My services would make heavy use of generics to boilerplate a lot of the code.
2. The View to Data service would be implemented through an interface to decouple the Core/Business domain code from the Data domain code.
3. Each code domain would reside in different projects to enforce dependancy rules.
4. Collection requests would always be constrained with request objects defining paging.
5. Componentization of UI.  For example, the Add a New Record block would be a component or RenderFragment block.








