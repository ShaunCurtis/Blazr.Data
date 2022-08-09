# Blazor - Get your Data out of Your Components

This article takes the standard Blazor template and demonstrates how to move the data and it's management out of the UI.  There are many questions posted on forums and sites by programmers were the root cause of their problem is trying to manage data within the UI.  The quick answer to many is a bit more inter component wiring to patch it together, but fundimentally the design is flawed.  Add a bit more functionality and it all breaks again.

Here's a typical example:

```csharp
private WeatherForecast[]? forecasts;

protected override async Task OnInitializedAsync()
    => forecasts = await ForecastService.GetForecastAsync(DateTime.Now);
```

Recognise this block of code?  It comes directly from `FetchData` in the Blazor templates.  It's Microsoft distributed code, which gives it a stamp of approval it doesn't really deserve.

I've tried to keep things as simple as possible in the article.  The code is *For Demo Purposes*: it's not full production code.  I've left out stuff that would make it more difficult to read and understand.  Read my footnote in the Appendix for more information on the kind of stuff that's missing.

## Repository

The project and the latest version of this article are here:   [Blazr.Data Github Repository](https://github.com/ShaunCurtis/Blazr.Data).

## Starting Point

The starting solution for the code is the standard Blazor Server template.  I can keep things simpler in Server, and debugging is quicker and easier.   Note that the solution is implemented with `Nullable` enabled. 

## The Solution

First some re-organisation.  The UI is currently plugged directly into the back-end data service.  We need to re-build the data pipeline to look like this:

```text
UI <=> View Service <=> Data Service <=> Data Store
```

The WASM your data pipeline whould look like this:

```text
UI <=> View Service <=> API Data Service <=> [Network] <=> Controller <=> Server Data Service <=> Data Store
```

### WeatherForecastDataService

Rename `WeatherForecastService` to `WeatherForecastDataService`.  This combines the *Data Service* and *Data Store* layers.

It now:

1. Maintains an internal list of `WeatherForcast` objects.  
2. `GetRecordsAsync` provides a copy of `WeatherForcast` not a reference to the internal list.  What an *ORM* such as Entity Framework would do.
3. Returns result objects containing both status information and data.
4. Returns `IEnumerable` collections: not lists or arrays.
5. Has an `AddRecordAsync` method to add a record to the data store.

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
```

### Result Records

These are the objects returned by the data layer.  They are `records` because they need to be serializable to use in API calls. 

`RecordListResult` is returned by all collection/list queries.

```csharp
public record RecordListResult<TRecord>
{
    public IEnumerable<TRecord> Items { get; init; } = Enumerable.Empty<TRecord>();
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static RecordListResult<TRecord> Successful(IEnumerable<TRecord> items)
        => new RecordListResult<TRecord> { Items = items, Success = true };

    public static RecordListResult<TRecord> Failure(string message)
        => new RecordListResult<TRecord> { Success = false, Message = message };
}
```

`RecordCommandResult` is returned by all commands: Add/Delete/Update.

```csharp
public record RecordCommandResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static RecordCommandResult Successful()
        => new RecordCommandResult { Success = true };

    public static RecordCommandResult Failure(string message)
        => new RecordCommandResult { Success = false, Message = message };
}
```

### WeatherForecastViewService

`WeatherForecastViewService` is the *View Service*.  It provides the data to the UI.

It:

1. Obtains the registered Data service through DI on object instantiation.
2. Provides methods to get and add records.
3. Provides the record collection.
4. Provides an event the UI can use for list update notifications.

```csharp
public class WeatherForecastViewService
{
    private WeatherForecastDataService _dataService;

    public WeatherForecastViewService(WeatherForecastDataService weatherForecastDataService)
        => _dataService = weatherForecastDataService;

    public IEnumerable<WeatherForecast> Records { get; private set; } = Enumerable.Empty<WeatherForecast>();

    public WeatherForecast? Record { get; private set; }

    public string LatestErrorMessage { get; private set; } = string.Empty;

    public event EventHandler? ListUpdated;

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
```

### Service Registration

Register these two services in `Program`. `WeatherForecastViewService` is Scoped: each SPA session has it's own instance.  

```csharp
builder.Services.AddSingleton<WeatherForecastDataService>();
builder.Services.AddScoped<WeatherForecastViewService>();
```

### FetchData

We can now update the `FetchData` UI Component.  It:

1. Injects the registerted instance of `WeatherForecastViewService`.
2. Loads the View in `OnInitializedAsync`.
3. Uses `Service.Records` as it's data source.  There's no data held directly in the component.  

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

This emulates an edit form.  It's designed to be *inline*, so has control over show/hide.  It could be a modal dialog.

It has one *Parameter*, no public properties and one public method.  

 - The parent form communicates directly with the component through the `Show` method
 - The component communicates with the parent through the `FormClosed` callback.

The internal Add and Exit methods close the component by setting `show` to false, and then invoke the callback to inform the parent of closure.

The form injects the registered instance of `WeatherForecastViewService` and uses `AddRecordAsync` to add a record to the data store.
 
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

Note that `ShowForm` calls `StateHasChanged`.  It's not a UI event handler, so there's no automated render events.

### FetchData

The modified `FetchData`.  

There's:
1. A button block for *Add A New Record*:  the block display is controlled by `addForm`.
2. A `WeatherForecastEditorForm` referenced to a local private field.
3. An event receiver for the View Service `ListUpdated` event.
4. A receiver for the edit form `FormClosed` callback.
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

Some notes:

1. You don't need to call `StateHasChanged` in UI event handlers such as `Exit` and `ShowForm`: the `ComponentBase` UI event handler calls them automatically.  The only call in the code is in `ShowForm` in the editor.  This is a standard method so there's no automated calls.
 
2. All calls into the data pipeline return *Result* objects.  These provide a mechanism for returning both the result and status information about the request.

3. Using events in the View layer provide a simple mechanism for maintaining state.  It's very easy to shortcut the design process and start wiring components together.  But you quickly code an unmanaged mess that's impossible to debug and cluttered with calls to `StateHasChanged` to try (and often fail to) keep everything in sync.

## Appendix

### What's missing

As I said in the introduction this code is *For Demo Purposes*.  There's nothing wrong with it: I just kept it simple.  Here are a few *complexities* that would appear in my production code:

1. My services would be heavy on generics to boilerplate a lot of the code.
2. View to Data services would be implemented through interfaces to decouple the Core/Business domain code from the Data domain code.
3. Each code domain would reside in different projects to enforce dependancy rules.
4. Collection requests would always be constrained with request objects defining paging.
5. Componentization of UI.  For example, the Add a New Record block would be a component or RenderFragment block.








