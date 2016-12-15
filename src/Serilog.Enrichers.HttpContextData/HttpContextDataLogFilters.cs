// Includes code inspired from Nick Craver's StackExchange.Exceptional 
// See https://github.com/NickCraver/StackExchange.Exceptional

using System.Collections.Generic;

namespace Serilog.Enrichers.HttpContextData
{

    public class HttpContextDataLogFilterSettings
    {

        /// <summary>
        /// The Regex pattern of data keys to include. For example, "Redis.*" would include all keys that start with Redis
        /// </summary>
        public string DataIncludePattern { get; set; }

        /// <summary>
        /// Form submitted values to replace on save - this prevents logging passwords, etc.
        /// </summary>
        public List<HttpContextDataLogFilter> FormFilters { get; set; }

        /// <summary>
        /// Cookie values to replace on save - this prevents logging auth tokens, etc.
        /// </summary>
        public List<HttpContextDataLogFilter> CookieFilters { get; set; }

        /// <summary>
        /// Request header values to replace on save - this prevents logging sensitive request headers.
        /// </summary>
        public List<HttpContextDataLogFilter> HeaderFilters { get; set; }

        /// <summary>
        /// Server variable values to replace on save - this prevents logging sensitive request headers.
        /// </summary>
        public List<HttpContextDataLogFilter> ServerVarFilters { get; set; }


        public bool AppendFullStackTrace { get; set; } = false;


    }
}

public class HttpContextDataLogFilter
{
    /// <summary>
    /// The form parameter name to ignore
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The value to log instead of the real value
    /// </summary>
    /// <remarks>If value is empty string the key will be totally removed</remarks>
    public string ReplaceWith { get; set; }

}
