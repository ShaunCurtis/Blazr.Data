# Blazor - Get your Data out of Your Components

Writing this article was sparked by the number of questions I answer where the root cause of the problem is trying to manage data in the UI components.  Something like this:

```csharp
private WeatherForecast[]? forecasts;

protected override async Task OnInitializedAsync()
    => forecasts = await ForecastService.GetForecastAsync(DateTime.Now);
```

You may recognise this code.  It comes directly from `FetchData` in the Blazor templates: it's Microsoft distributed code.  Unfortunately that doesn't make it a good practice.  It just misleads.

So what should it look like?  I'm going to keep this as simple as possible.  The code in this article is *For Demo Purposes*.  It's not production code because there's stuff missing that would clutter and make it more difficult to understand.  Read my footnote in the Appendix for the type of *stuff* thst is missing.

I have included the use of `TaskCompletionSource` which is a fairly advanced coding technique to control the EditForm.  It's just too good an example not to use it, and it makes the code much more elegant.  See the Appendix for a more detailed discussion on it's use.

## Repository

You can find the project and the latest version of this article at: https://github.com/ShaunCurtis/Blazr.Data

## Starting Point

The starting solution for this project is the standard Blazor Server template.

Everything will run in a WASM project, but you should really implement an API `WeatherForecastDataService` => Controller => Server `WeatherForecastDataService` data pipeline to emulate a real life application.

Debugging is also much easier and straight forward on Server. 

The solution is implemented with `Nullable` enabled. 

First we need to re-organise our services.

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

Rename this to `WeatherForecastDataService`. 

It now:

1. Maintains an internal list of `WeatherForcast` objects and when `GetRecordsAsync` is called it provides a copy of this list, not a reference to the list.  This now emulates what a ORM such as Entity Framework would do.
2. Returns result objects that contains both status information and data.
3. Returns `IEnumerable` collections: not lists or arrays.
3. Has an `AddRecordAsync` method to add a record to the list.

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

### NewWeatherForecastForm

This form emulates an edit form.  It's designed to be *inline*, so has control over show/hide.  It could be a modal dialog.

It has no *Parameters*, no public properties and only one public method.  The parent form communicates directly with it through the `Show` method.

It implements provider Task based management.  `TaskCompletionSource` is a Task provider.   When the consumer calls `ShowForm`, `show` is turned on and `StateHasChanged` called to queue a component render.  It creates a new task provider and passes the provider's `Task` back to the caller.

The Add and Exit methods set `show` to false, and call `StateHasChanged` to queue a render event.  They set `taskSource` as completed and complete.  We'll see the consumer side effect of this in the main form shortly.

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
    private TaskCompletionSource? taskProvider;

    private bool show { get; set; } = false;

    private async Task AddRecord()
    {
        await Service.AddRecordAsync();
        show = false;
        StateHasChanged();
        taskProvider?.SetResult();
    }

    private void Exit()
    {
        show = false;
        StateHasChanged();
        taskProvider?.SetResult();
    }

    public Task ShowForm()
    {
        show = true;
        StateHasChanged();
        taskProvider = new TaskCompletionSource();
        return taskProvider.Task;
    }
}
```

### WeatherForecasts

This is our new `FetchData`.  

There's:
1. A button block for *Add 
A New Record* which is controlled by `addForm`.
2. A `NewWeatherForecastForm` referenced to a local private field.
3. An event receiver for the service `ListUpdated` event.
4. `IDisposable` implemented to de-register the event handler correctly.

The interesting code is in `ShowAddForm`.  

1. It sets `addForm` to true and then calls `ShowForm` on `NewWeatherForecastForm`.  
2. Once `ShowForm` completes, it passes a running Task back to `ShowAddForm`.
3. `ShowAddForm` yields back to the UI handler while it awaits the completion of the task.  This also awaits and yields control back to the system.
4. The UI Renderer gets thread time and runs the queued Render events: show `NewWeatherForecastForm`, hide the button block, and any other events.

At this point `ShowAddForm` is *suspended*.  The code block following the `await` is scheduled as a continuation once the awaited task completes.    The thread is free to service any tasks it receives.  In our case either `Save` or `Exit` runs in `NewWeatherForecastForm` and sets the task to complete.  At which point:  

5. `ShowAddForm` now runs to completion, setting `AddForm` to false.
6. The UI event handler schedues a final render by calling `StateHasChanged` on the main form.
7. The UI Renderer gets thread time and runs the queued render events:  hide `NewWeatherForecastForm` and show the button block.  

```csharp
@page "/weatherforecasts"
@implements IDisposable
@using Blazr.Data.Data
@inject WeatherForecastViewService Service

<PageTitle>Weather forecast</PageTitle>

<NewWeatherForecastForm @ref=form />

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
    private NewWeatherForecastForm form = default!;

    protected override async Task OnInitializedAsync()
    {
        await Service.GetRecordsAsync();
        this.Service.ListUpdated += OnListUpdate;
    }

    private async Task ShowAddForm()
    {
        addForm = true;
        await form.ShowForm();
        addForm = false;
    }

    private void OnListUpdate(object? sender, EventArgs e)
        => this.InvokeAsync(StateHasChanged);

    public void Dispose()
        => this.Service.ListUpdated += OnListUpdate;
}
```

## Summary

This example shows how to move data management into a view service, and how to use an event driven model to update components.

It's very easy to start wiring components together.  But you soon end up with a unmanaged mess that's impossible to debug and cluttered with calls to `StateHasChanged` to try (and often failing to) keep everything in sync.

## Appendix

### What's missing

As I said in the introduction this code is *For Demo Purposes*.  It's not poor or unstructured, but there are things missing that you would add  in a production environment.  Here are a few examples:

1. My services would make heavy use of generics to boilerplate a lot of the code.
2. The View to Data service would be implemented through interfaces to decouple the Core/Business domain code from the Data domain code.
3. Each code domain would reside in different projects to enforce dependancies.
4. Collection requests would always be constrained with request objects defining paging.

### TaskCompletionSource

You are almost always the consumer of a `Task`, calling an `await`.  There are several examples in this project.  You can bulid Task based methods with `Task.Delay`, but you have no way of creating a true `Task` context.

`TaskCompletionSource` is a `Task` provider: a manually controlled task wrapper that generates a `Task` you control through the wrapper.

You normally declare one at the class level:

```csharp
private TaskCompletionSource? taskProvider;
```

And then create one when you need it:

```csharp
taskProvider = new TaskCompletionSource();
```

You can now pass a running `Task` back to a caller like this:

```csharp
return taskProvider.Task;
```

When whatever the class does completes you simply call: 

```
taskProvider?.SetResult();
```

The Task Manager sees this state change and runs the *continuation* - the rest of the code block in the caller.







