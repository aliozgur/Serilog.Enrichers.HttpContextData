// Includes code inspired from Nick Craver's StackExchange.Exceptional 
// See https://github.com/NickCraver/StackExchange.Exceptional

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Serilog.Configuration;
using Serilog.Events;

namespace Serilog.Enrichers.HttpContextData
{
    public static class HttpContextEnricherExtensions
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        public const string UnknownIP = "0.0.0.0";
        private static readonly Regex IPv4Regex = new Regex(@"\b([0-9]{1,3}\.){3}[0-9]{1,3}$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public static long ToEpochTime(this DateTime dt)
        {
            return (long)(dt - Epoch).TotalSeconds;
        }

        public static long? ToEpochTime(this DateTime? dt)
        {
            return dt.HasValue ? (long?)ToEpochTime(dt.Value) : null;
        }
        public static bool HasValue(this string s)
        {
            return !string.IsNullOrEmpty(s);
        }

        public static bool IsBuiltInException(this Exception e)
        {
            return e.GetType().Module.ScopeName == "CommonLanguageRuntimeLibrary";
        }

        public static Dictionary<string, string> ToJsonDictionary(this List<HttpContextData.NameValuePair> collection)
        {
            var result = new Dictionary<string, string>();
            if (collection == null) return result;
            foreach (var pair in collection)
            {
                if (pair.Name.HasValue())
                {
                    result[pair.Name] = pair.Value;
                }
            }
            return result;
        }

        public static string GetRemoteIP(this NameValueCollection serverVariables)
        {
            var ip = serverVariables["REMOTE_ADDR"]; // could be a proxy -- beware
            var ipForwarded = serverVariables["HTTP_X_FORWARDED_FOR"];

            // check if we were forwarded from a proxy
            if (ipForwarded.HasValue())
            {
                ipForwarded = IPv4Regex.Match(ipForwarded).Value;
                if (ipForwarded.HasValue() && !IsPrivateIP(ipForwarded))
                    ip = ipForwarded;
            }

            return ip.HasValue() ? ip : UnknownIP;
        }

        /// <summary>
        /// returns true if this is a private network IP  
        /// http://en.wikipedia.org/wiki/Private_network
        /// </summary>
        private static bool IsPrivateIP(string s)
        {
            return (s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("127.0.0."));
        }

        /// <summary>
        /// Enrich log events with a MachineName property containing the current <see cref="Environment.MachineName"/>.
        /// </summary>
        /// <param name="enrichmentConfiguration">Logger enrichment configuration.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        public static LoggerConfiguration WithHttpContextData(
           this LoggerEnrichmentConfiguration enrichmentConfiguration)
        {
            if (enrichmentConfiguration == null) throw new ArgumentNullException(nameof(enrichmentConfiguration));
            return enrichmentConfiguration.With<HttpContextDataEnricher>();
        }

        public static void AddIfAbsent(this LogEventProperty prop, LogEvent logEvent)
        {
            logEvent.AddPropertyIfAbsent(prop);
        }
    }
}