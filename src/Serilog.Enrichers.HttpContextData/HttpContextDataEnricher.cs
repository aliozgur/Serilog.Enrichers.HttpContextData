using System.Collections.Concurrent;
using System.Web;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Enrichers.HttpContextData
{
    public class HttpContextDataEnricher : ILogEventEnricher
    {
        private ConcurrentDictionary<string, LogEventProperty> _cachedProperties =
            new ConcurrentDictionary<string, LogEventProperty>();

        public LogEventLevel MinimumLogLevel { get; set; } = LogEventLevel.Error;
        public HttpContextDataLogFilterSettings FilterSettings { get; set; }

        public HttpContextDataEnricher()
        {
            
        }

        public HttpContextDataEnricher(LogEventLevel minimumLogLevel) : this()
        {
            MinimumLogLevel = minimumLogLevel;
        }

        public HttpContextDataEnricher(HttpContextDataLogFilterSettings filterSettings) : this(LogEventLevel.Error,filterSettings)
        {
        }

        public HttpContextDataEnricher(LogEventLevel minimumLogLevel, HttpContextDataLogFilterSettings filterSettings) : this(minimumLogLevel)
        {
            FilterSettings = filterSettings;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent.Level < MinimumLogLevel)
                return;    
                       
            AddLogEventProperties(logEvent, propertyFactory);
        }

        private void AddLogEventProperties(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {

            var currentCtx = new HttpContextWrapper(HttpContext.Current);
            var e = logEvent.Exception;
            var ctx = new HttpContextData(e, currentCtx, FilterSettings);
            if (e != null)
            {
                propertyFactory.CreateProperty("_" + nameof(ctx.ExceptionMessage), ctx.ExceptionMessage).AddIfAbsent(logEvent);
                propertyFactory.CreateProperty("_" + nameof(ctx.ExceptionDetail), ctx.ExceptionDetail).AddIfAbsent(logEvent);
                propertyFactory.CreateProperty("_" + nameof(ctx.ExceptionSource), ctx.ExceptionSource).AddIfAbsent(logEvent);
                propertyFactory.CreateProperty("_" + nameof(ctx.ExceptionType), ctx.ExceptionType).AddIfAbsent(logEvent);
            }

            propertyFactory.CreateProperty("_" + nameof(ctx.Host), ctx.Host).AddIfAbsent(logEvent);
            propertyFactory.CreateProperty("_" + nameof(ctx.HTTPMethod), ctx.HTTPMethod).AddIfAbsent(logEvent);
            propertyFactory.CreateProperty("_" + nameof(ctx.IPAddress), ctx.IPAddress).AddIfAbsent(logEvent);
            propertyFactory.CreateProperty("_" + nameof(ctx.Url), ctx.Url).AddIfAbsent(logEvent);
            propertyFactory.CreateProperty("_" + nameof(ctx.StatusCode), ctx.StatusCode).AddIfAbsent(logEvent);

            var kvps = ctx.CustomData;
            if (kvps != null)
            {
                foreach (var v in kvps)
                {
                    propertyFactory.CreateProperty("cd: " + v.Key, v.Value).AddIfAbsent(logEvent);
                }
            }

            var sv = ctx.ServerVariablesSerializable;
            foreach (var v in sv)
            {
                propertyFactory.CreateProperty("sv: " + v.Name, v.Value).AddIfAbsent(logEvent);
            }

            sv = ctx.RequestHeadersSerializable;
            foreach (var v in sv)
            {
                propertyFactory.CreateProperty("rh: " + v.Name, v.Value).AddIfAbsent(logEvent);
            }

            sv = ctx.QueryStringSerializable;
            foreach (var v in sv)
            {
                propertyFactory.CreateProperty("qs: " + v.Name, v.Value).AddIfAbsent(logEvent);
            }

            sv = ctx.CookiesSerializable;
            foreach (var v in sv)
            {
                propertyFactory.CreateProperty("cookie: " + v.Name, v.Value).AddIfAbsent(logEvent);
            }
        }
    }
}