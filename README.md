# Serilog.Enrichers.HttpContextData
Enriches Serilog events with information from **HttpContext.Current**

To use the enricher first install the NuGet package

```powershell
Install-Package Serilog.Enrichers.HttpContextData
```
Then, apply the enricher to you ```LoggerConfiguration```:

```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.WithHttpContextData()
    // ...other configuration...
    .CreateLogger();
```

You will have enriched log events that look like the screenshot below (Serilog log event sinked to Seq)

![Serilog.Enrichers.HttpContextData on Seq](https://raw.githubusercontent.com/aliozgur/Serilog.Enrichers.HttpContextData/master/assets/ss.png)


## Dependencies
* **Serilog** version 1.0.1 or above 
* **System.Web** and **System.Web.Extensions** friends


## Log Filter And Settings

You can configure the HttpContextDataEnricher to enable filtering of captured log data by **HttpContextData** instance

```csharp

// Create a settings instance
HttpContextDataLogFilterSettings settings = new HttpContextDataLogFilterSettings();

// Append full stack trace if enriched log event has an Exception object
settings.AppendFullStackTrace = true;

//Form submitted values to replace on save - this prevents logging passwords, etc
settings.FormFilters = new List<HttpContextDataLogFilter>
                    {
                        new HttpContextDataLogFilter { Name= "Password", ReplaceWith="" } // Do not capture form field named Password. See "Filter Name and ReplaceWith conventions"
                        , new HttpContextDataLogFilter { Name = "Pwd", ReplaceWith = "*** WE DO NOT RECORDS PASSWORDS *** " } // Capture field named Pwd and replace the value 
                    };

//Cookie values to replace on save - this prevents logging auth tokens, etc.
settings.CookieFilters = new List<HttpContextDataLogFilter>
                    {
                        new HttpContextDataLogFilter { Name= "AUTH", ReplaceWith="" } // Do not capture cookie named AUTH. See "Filter Name and ReplaceWith conventions"
                        , new HttpContextDataLogFilter { Name = "SESS_ID", ReplaceWith = "*** WE DO NOT RECORDS COOKIES *** " } // Capture cookie named SESS_ID and replace the value 
                    };

//Request header values to replace on save - this prevents logging sensitive request headers.
settings.HeaderFilters = new List<HttpContextDataLogFilter>
                    {
                        new HttpContextDataLogFilter { Name= "", ReplaceWith="" } // This is a special case. See "Filter Name and ReplaceWith conventions"
                        , new HttpContextDataLogFilter { Name = "Accept", ReplaceWith = "***" } // Capture Accept header value and replace 
                        , new HttpContextDataLogFilter { Name = "Content-Type", ReplaceWith = "" } // Do not capture Content-Type header value. See "Filter Name and ReplaceWith conventions"
                    };

//Server variable values to replace on save - this prevents logging sensitive request headers.
settings.ServerVarFilters = new List<HttpContextDataLogFilter>
                    {
                        new HttpContextDataLogFilter { Name = "ALL_HTTP", ReplaceWith = "***" } // Capture ALL_HTTP server variable and replace 
                        , new HttpContextDataLogFilter { Name = "ALL_RAW", ReplaceWith = "" } // Do not capture ALL_RAW server variable. See "Filter Name and ReplaceWith conventions"
                    };

//The Regex pattern of data keys to include. For example, "Redis.*" would include all keys that start with Redis
//This is applied only if enriched log event has an Exception object. Matched data values from exceptions Data array are captured in CustomData property of the  HttpContextData instance
settings.DataIncludePattern = @"\b([0-9]{1,3}\.){3}[0-9]{1,3}$" 

```

## Filter Name and ReplaceWith conventions
While capturing context data two conventions are followed to discard a single value with a specified name or set of values.

If you have a **HttpContextDataLogFilter** instance with **empty string** (be warned not null)  **Name** inside a **HttpContextDataLogFilterSettings** filter all items will be discarded and not captured.

```csharp
HttpContextDataLogFilterSettings settings = new HttpContextDataLogFilterSettings();

// The second HttpContextDataLogFilter instance has an empty string name which will cause all header items to be discarded
settings.HeaderFilters = new List<HttpContextDataLogFilter>
                    {
                         new HttpContextDataLogFilter { Name = "Accept", ReplaceWith = "***" } // Capture Accept header value and replace 
                        , new HttpContextDataLogFilter { Name= "", ReplaceWith="" } // This is a special case. See "Filter Name and ReplaceWith conventions"
                        , new HttpContextDataLogFilter { Name = "Content-Type", ReplaceWith = "" } // Do not capture Content-Type header value. See "Filter Name and ReplaceWith conventions"
                    };

```
If you have a **HttpContextDataLogFilter** instance with **empty string** (be warned not null)  **Value** inside a **HttpContextDataLogFilterSettings** filter that specific item matching the **Name** value will be discarded

```csharp
HttpContextDataLogFilterSettings settings = new HttpContextDataLogFilterSettings();

// The second HttpContextDataLogFilter instance has an empty string value for "Content-Type" which will cause Content-Type header value to be discarded
settings.HeaderFilters = new List<HttpContextDataLogFilter>
                    {
                        new HttpContextDataLogFilter { Name = "Accept", ReplaceWith = "***" } // Capture Accept header value and replace 
                        , new HttpContextDataLogFilter { Name = "Content-Type", ReplaceWith = "" } // Do not capture Content-Type header value. See "Filter Name and ReplaceWith conventions"
                    };

```

**NEW With version 0.1.1** You can also specify a regular expression in the ```Name``` property of  an ```HttpContextDataLogFilter``` instance, in this case you will need to set the ```NameIsRegex``` property to true.

_Please note regex filters also follow the **the empty string** convention described above._

```csharp
var logFilterSettings = new HttpContextDataLogFilterSettings
        {
            ServerVarFilters = new List<HttpContextDataLogFilter>
            {
                new HttpContextDataLogFilter {Name = "AUTH_U.*", ReplaceWith = "", NameIsRegex = true }, // will remove all server variables matched by the regex specified in the Name property.
                new HttpContextDataLogFilter {Name = "AUTH_PASSWORD", ReplaceWith = "***"},
            }
        };
```


## Initializing the Enricher with settings

```csharp


// Prepare the log filter settings instance
HttpContextDataLogFilterSettings settings = new HttpContextDataLogFilterSettings();

// The second HttpContextDataLogFilter instance has an empty string value for "Content-Type" which will cause Content-Type header value to be discarded
settings.HeaderFilters = new List<HttpContextDataLogFilter>
                    {
                        new HttpContextDataLogFilter { Name = "Accept", ReplaceWith = "***" } // Capture Accept header value and replace 
                        , new HttpContextDataLogFilter { Name = "Content-Type", ReplaceWith = "" } // Do not capture Content-Type header value. See "Filter Name and ReplaceWith conventions"
                    };
//...
// Maybe, some more settings....
//...

//Create the enricher 
var contextEnricher = new HttpContextDataEnricher(LogEventLevel.Error, settings);
Log.Logger = new LoggerConfiguration()
    .Enrich.With(contextEnricher) // <<--- This is how we configure our enricher
    // ...other configuration...
    .CreateLogger();

```


## A Note About LogEventLevel

**HttpContextDataEnricher** uses _LogEventLevel.Error_ as the minimum log level which means only log events with log level of Error and above will be enriched.
But, you can customize  **MinimumLogLevel** as demonstrated in "Initializing the Enricher with settings" section 

## Using HttpContextData without enriching the log events  

Serilog supports deserializing log event property as structured objects through [destructuring operator](https://github.com/serilog/serilog/wiki/Structured-Data#preserving-object-structure).
You can create an **HttpContextData** instance and log context data without using the enricher. Here is an example

```csharp
var text = $"Destructured info log message on {DateTime.Now}";
var ctxData = new HttpContextData(this.HttpContext);
var dto = ctxData.ToDto(); // <--- ToDto() extension method creates a serialazable anonymous copy of HttpContextData instance                

// Log destructured httpcontext data
Log.Logger.Information("Here is the current context data {@dto}", dto);

```

## Authors
* Ali Özgür [@aliozgur](https://twitter.com/aliozgur)

## Thanks
**HttpContextData** and  **HttpContextDataLogFilter** classes include code inspired by Nick Craver's work on [StackExchange.Exceptional](https://github.com/NickCraver/StackExchange.Exceptional) 
