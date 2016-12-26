// Includes code inspired from Nick Craver's StackExchange.Exceptional 
// See https://github.com/NickCraver/StackExchange.Exceptional

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Serilog.Enrichers.HttpContextData
{
    [Serializable]
    public class HttpContextData
    {
        internal const string CollectionErrorKey = "CollectionFetchError";

        private ConcurrentDictionary<string, string> _formLogFilters = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, string> _formRegexLogFilters = new ConcurrentDictionary<string, string>();

        private ConcurrentDictionary<string, string> _cookieLogFilters = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, string> _cookieRegexLogFilters = new ConcurrentDictionary<string, string>();

        private ConcurrentDictionary<string, string> _headerLogFilters = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, string> _headerRegexLogFilters = new ConcurrentDictionary<string, string>();

        private ConcurrentDictionary<string, string> _serverVarLogFilters = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, string> _serverVarRegexLogFilters = new ConcurrentDictionary<string, string>();
        private Regex _dataIncludeRegex;


        private HttpContextDataLogFilterSettings _filterSettings;
        private HttpContextDataLogFilterSettings FilterSettings
        {
            get { return _filterSettings; }
            set
            {
                _filterSettings = value;
                PrepareLogFilters();
            }
        }

        public HttpContextData(HttpContextBase context) : this(null, context, null)
        {
        }

        public HttpContextData(Exception e) : this(e, null, null)
        {
        }

        public HttpContextData(Exception e, HttpContextBase context, HttpContextDataLogFilterSettings filterSettings)
        {
            MachineName = Environment.MachineName;
            FilterSettings = filterSettings;

            if (e != null) SetExceptionProperties(e, (FilterSettings?.AppendFullStackTrace) ?? false);
            if (context != null) SetContextProperties(context);
        }


        private void PrepareLogFilters()
        {
            _dataIncludeRegex = null;

            _cookieLogFilters = new ConcurrentDictionary<string, string>();
            _cookieRegexLogFilters = new ConcurrentDictionary<string, string>();
            _filterSettings?.CookieFilters?.ToList().ForEach(flf =>
                {
                    if (!flf.NameIsRegex) _cookieLogFilters[flf.Name] = flf.ReplaceWith ?? "";
                    else if (!String.IsNullOrWhiteSpace(flf.Name)) _cookieRegexLogFilters[flf.Name] = flf.ReplaceWith ?? "";
                }
            );


            _formLogFilters = new ConcurrentDictionary<string, string>();
            _formRegexLogFilters = new ConcurrentDictionary<string, string>();
            _filterSettings?.FormFilters?.ToList().ForEach(flf =>
            {
                if (!flf.NameIsRegex) _formLogFilters[flf.Name] = flf.ReplaceWith ?? "";
                else if (!String.IsNullOrWhiteSpace(flf.Name)) _formRegexLogFilters[flf.Name] = flf.ReplaceWith ?? "";
            }
            );


            _headerLogFilters = new ConcurrentDictionary<string, string>();
            _headerRegexLogFilters = new ConcurrentDictionary<string, string>();
            _filterSettings?.HeaderFilters?.ToList().ForEach(flf =>
            {
                if (!flf.NameIsRegex) _headerLogFilters[flf.Name] = flf.ReplaceWith ?? "";
                else if (!String.IsNullOrWhiteSpace(flf.Name)) _headerRegexLogFilters[flf.Name] = flf.ReplaceWith ?? "";
            }
            );


            _serverVarLogFilters = new ConcurrentDictionary<string, string>();
            _serverVarRegexLogFilters = new ConcurrentDictionary<string, string>();
            _filterSettings?.ServerVarFilters?.ToList().ForEach(flf =>
            {
                if (!flf.NameIsRegex) _serverVarLogFilters[flf.Name] = flf.ReplaceWith ?? "";
                else if (!String.IsNullOrWhiteSpace(flf.Name)) _serverVarRegexLogFilters[flf.Name] = flf.ReplaceWith ?? "";
            }
            );

            if (!string.IsNullOrEmpty(_filterSettings?.DataIncludePattern))
            {
                _dataIncludeRegex = new Regex(_filterSettings?.DataIncludePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
        }

        private void SetExceptionProperties(Exception e, bool appendFullStackTrace)
        {
            var baseException = e;

            // if it's not a .Net core exception, usually more information is being added
            // so use the wrapper for the message, type, etc.
            // if it's a .Net core exception type, drill down and get the innermost exception
            if (e.IsBuiltInException())
                baseException = e.GetBaseException();

            ExceptionType = baseException.GetType().FullName;
            ExceptionMessage = baseException.Message;
            ExceptionSource = baseException.Source;
            ExceptionDetail = e.ToString();


            var httpException = e as HttpException;
            if (httpException != null)
            {
                StatusCode = httpException.GetHttpCode();
            }

            if (appendFullStackTrace)
            {
                var frames = new StackTrace(fNeedFileInfo: true).GetFrames();
                if (frames != null && frames.Length > 2)
                    ExceptionDetail += "\n\nFull Trace:\n\n" + string.Join("", frames.Skip(2));
            }

            var exCursor = baseException;
            while (exCursor != null)
            {
                AddFromData(exCursor);
                exCursor = exCursor.InnerException;
            }


        }

        private void SetContextProperties(HttpContextBase context)
        {
            if (context == null) return;

            var request = context.Request;

            Func<Func<HttpRequestBase, NameValueCollection>, NameValueCollection> tryGetCollection = getter =>
            {
                try
                {
                    return new NameValueCollection(getter(request));
                }
                catch (HttpRequestValidationException e)
                {
                    Trace.WriteLine("Error parsing collection: " + e.Message);
                    return new NameValueCollection { { CollectionErrorKey, e.Message } };
                }
            };

            StatusCode = context.Response?.StatusCode;
            _httpMethod = context.Request.HttpMethod;

            ServerVariables = _serverVarLogFilters?.ContainsKey("") == true
                ? new NameValueCollection()
                : tryGetCollection(r => r.ServerVariables);

            QueryString = tryGetCollection(r => r.QueryString);
            Form = _formLogFilters?.ContainsKey("") == true ? new NameValueCollection()
                : tryGetCollection(r => r.Form);

            FilterServerVariables();
            FilterFormData();
            FilterCookies(request);
            FilterRequestHeaderData(request);
        }

        private void FilterRequestHeaderData(HttpRequestBase request)
        {
            if (_headerLogFilters?.ContainsKey("") == true)
            {
                RequestHeaders = new NameValueCollection();
            }
            else
            {
                RequestHeaders = new NameValueCollection(request.Headers.Count);
                foreach (var header in request.Headers.AllKeys)
                {
                    // Cookies are handled above, no need to repeat
                    if (string.Compare(header, "Cookie", StringComparison.OrdinalIgnoreCase) == 0)
                        continue;

                    if (request.Headers[header] != null)
                    {
                        string val = null;
                        if (!(_headerLogFilters?.TryGetValue(header, out val) ?? false)) // No match by name
                        {
                            var regexMatch = _headerRegexLogFilters.Where(r => Regex.IsMatch(header, r.Key, RegexOptions.IgnoreCase | RegexOptions.Singleline))
                                .Select(r => new { Key = r.Key, Value = r.Value })
                                .FirstOrDefault();

                            val = regexMatch?.Value;
                        }

                        if (val == "") continue; // Discard header value
                        else RequestHeaders[header] = val ?? request.Headers[header];
                    }
                }
            }
        }

        private void FilterCookies(HttpRequestBase request)
        {
            try
            {
                if (_cookieLogFilters?.ContainsKey("") == true)
                {
                    Cookies = new NameValueCollection();
                }
                else
                {
                    Cookies = new NameValueCollection(request.Cookies.Count);

                    for (var i = 0; i < request.Cookies.Count; i++)
                    {
                        var name = request.Cookies[i].Name;
                        string val=null;
                        if(!(_cookieLogFilters?.TryGetValue(name, out val)??false)) // No match by name
                        {
                            var regexMatch = _cookieRegexLogFilters.Where(r => Regex.IsMatch(name, r.Key, RegexOptions.IgnoreCase | RegexOptions.Singleline))
                                .Select(r => new { Key = r.Key, Value = r.Value})
                                .FirstOrDefault();

                            val = regexMatch?.Value;
                        }

                        if (val == "" ) Cookies.Remove(name); // Discard cookie value
                        else Cookies.Add(name, val ?? request.Cookies[i].Value);
                    }
                }
            }
            catch (HttpRequestValidationException e)
            {
                Trace.WriteLine("Error parsing cookie collection: " + e.Message);
            }
        }

        private void FilterFormData()
        {
            // Filter form variables
            if (_formLogFilters?.Count > 0 && Form?.Count > 0)
            {
                foreach (var k in _formLogFilters.Keys)
                {
                    if (Form[k] != null)
                    {
                        var replaceWith = _formLogFilters[k];
                        if (replaceWith == "") Form.Remove(k); // Discard form value
                        else Form[k] = replaceWith;
                    }
                }
            }

            if (_formRegexLogFilters?.Count > 0 && Form?.Count > 0)
            {
                foreach (var key in _formRegexLogFilters.Keys)
                {
                    var matchKeys =
                        Form?.AllKeys?.Where(k => Regex.IsMatch(k, key, RegexOptions.IgnoreCase | RegexOptions.Singleline));
                    foreach (var k in matchKeys)
                    {
                        if (Form[k] != null)
                        {
                            var replaceWith = _formRegexLogFilters[key];
                            if (replaceWith == "") Form.Remove(k); // Discard form value
                            else Form[k] = replaceWith;
                        }
                    }
                }
            }
        }

        private void FilterServerVariables()
        {
            // Filter server variables 
            if (_serverVarLogFilters?.Count > 0 && ServerVariables?.Count > 0)
            {
                foreach (var k in _serverVarLogFilters.Keys)
                {
                    if (ServerVariables[k] != null)
                    {
                        var replaceWith = _serverVarLogFilters[k];
                        if (replaceWith == "") ServerVariables.Remove(k); // Discard form value
                        else ServerVariables[k] = replaceWith;
                    }
                }
            }

            if (_serverVarRegexLogFilters?.Count > 0 && ServerVariables?.Count > 0)
            {
                foreach (var key in _serverVarRegexLogFilters.Keys)
                {
                    var matchKeys =
                        ServerVariables?.AllKeys?.Where(
                            k => Regex.IsMatch(k, key, RegexOptions.IgnoreCase | RegexOptions.Singleline));
                    foreach (var k in matchKeys)
                    {
                        if (ServerVariables[k] != null)
                        {
                            var replaceWith = _serverVarRegexLogFilters[key];
                            if (replaceWith == "") ServerVariables.Remove(k); // Discard form value
                            else ServerVariables[k] = replaceWith;
                        }
                    }
                }
            }
        }

        internal void AddFromData(Exception exception)
        {
            if (exception.Data == null) return;

            // Regardless of what Resharper may be telling you, .Data can be null on things like a null ref exception.
            if (_dataIncludeRegex != null)
            {
                if (CustomData == null)
                    CustomData = new Dictionary<string, string>();

                foreach (string k in exception.Data.Keys)
                {
                    if (!_dataIncludeRegex.IsMatch(k)) continue;
                    CustomData[k] = exception.Data[k] != null ? exception.Data[k].ToString() : "";
                }
            }
        }

        public string ExceptionType { get; set; }

        public string ExceptionSource { get; set; }

        public string ExceptionMessage { get; set; }

        public string ExceptionDetail { get; set; }

        public string MachineName { get; set; }

        public int? StatusCode { get; set; }

        [ScriptIgnore]
        public NameValueCollection ServerVariables { get; set; }

        [ScriptIgnore]
        public NameValueCollection QueryString { get; set; }

        [ScriptIgnore]
        public NameValueCollection Form { get; set; }

        [ScriptIgnore]
        public NameValueCollection Cookies { get; set; }

        [ScriptIgnore]
        public NameValueCollection RequestHeaders { get; set; }

        /// <summary>
        /// Gets a collection of custom data added at log time
        /// </summary>
        public Dictionary<string, string> CustomData { get; set; }


        public string Host { get { return _host ?? (_host = ServerVariables == null ? "" : ServerVariables["HTTP_HOST"]); } set { _host = value; } }
        private string _host;

        public string Url { get { return _url ?? (_url = ServerVariables == null ? "" : ServerVariables["URL"]); } set { _url = value; } }
        private string _url;

        public string HTTPMethod { get { return _httpMethod ?? (_httpMethod = ServerVariables == null ? "" : ServerVariables["REQUEST_METHOD"]); } set { _httpMethod = value; } }
        private string _httpMethod;

        public string IPAddress { get { return _ipAddress ?? (_ipAddress = ServerVariables == null ? "" : ServerVariables.GetRemoteIP()); } set { _ipAddress = value; } }
        private string _ipAddress;


        public List<NameValuePair> ServerVariablesSerializable
        {
            get { return GetPairs(ServerVariables); }
            set { ServerVariables = GetNameValueCollection(value); }
        }

        public List<NameValuePair> QueryStringSerializable
        {
            get { return GetPairs(QueryString); }
            set { QueryString = GetNameValueCollection(value); }
        }
        public List<NameValuePair> FormSerializable
        {
            get { return GetPairs(Form); }
            set { Form = GetNameValueCollection(value); }
        }
        public List<NameValuePair> CookiesSerializable
        {
            get { return GetPairs(Cookies); }
            set { Cookies = GetNameValueCollection(value); }
        }
        public List<NameValuePair> RequestHeadersSerializable
        {
            get { return GetPairs(RequestHeaders); }
            set { RequestHeaders = GetNameValueCollection(value); }
        }



        public string ToJson()
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(this);
        }

        public string ToDetailedJson()
        {
            var serializer = new JavaScriptSerializer();
            var dto = ToDto();
            return serializer.Serialize(dto);
        }

        public object ToDto()
        {
            return new
            {
                ExceptionType,
                ExceptionSource,
                ExceptionMessage,
                ExceptionDetail,
                CustomData,
                HTTPMethod,
                Host,
                IPAddress,
                MachineName,
                StatusCode,
                Url,
                QueryString = ServerVariables?["QUERY_STRING"],
                ServerVariables = ServerVariablesSerializable.ToJsonDictionary(),
                CookieVariables = CookiesSerializable.ToJsonDictionary(),
                RequestHeaders = RequestHeadersSerializable.ToJsonDictionary(),
                QueryStringVariables = QueryStringSerializable.ToJsonDictionary(),
                FormVariables = FormSerializable.ToJsonDictionary()
            };
        }

        public override string ToString()
        {
            return !String.IsNullOrWhiteSpace(ExceptionMessage) ? ExceptionMessage : base.ToString();
        }
        /// <summary>
        /// Serialization class in place of the NameValueCollection pairs
        /// </summary>
        /// <remarks>This exists because things like a querystring can havle multiple values, they are not a dictionary</remarks>
        public class NameValuePair
        {
            /// <summary>
            /// The name for this variable
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// The value for this variable
            /// </summary>
            public string Value { get; set; }
        }

        private List<NameValuePair> GetPairs(NameValueCollection nvc)
        {
            var result = new List<NameValuePair>();
            if (nvc == null) return null;

            for (int i = 0; i < nvc.Count; i++)
            {
                result.Add(new NameValuePair { Name = nvc.GetKey(i), Value = nvc.Get(i) });
            }
            return result;
        }

        private NameValueCollection GetNameValueCollection(List<NameValuePair> pairs)
        {
            var result = new NameValueCollection();
            if (pairs == null) return null;

            foreach (var p in pairs)
            {
                result.Add(p.Name, p.Value);
            }
            return result;
        }


    }

}