using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Serilog.Enrichers.HttpContextData.Tests
{
    [TestClass]
    public class HttpContextDataTests
    {
        private Mock<HttpContextBase> _context;
        private Mock<HttpRequestBase> _request;
        private Mock<HttpResponseBase> _response;

        [TestInitialize]
        public void TestInitialize()
        {
            _context = new Mock<HttpContextBase>();
            _request = new Mock<HttpRequestBase>();
            _response = new Mock<HttpResponseBase>();

            var servervVariables = new NameValueCollection();
            servervVariables.Add("AUTH_USER", "ali");
            servervVariables.Add("AUTH_TYPE", "Forms");
            servervVariables.Add("AUTH_PASSWORD", "123");

            var cookies = new HttpCookieCollection();
            cookies.Add(new HttpCookie("COOKIE_10", "ck1"));
            cookies.Add(new HttpCookie("COOKIE_11", "ck2"));
            cookies.Add(new HttpCookie("COOKIE_2", "ck3"));
            cookies.Add(new HttpCookie("COOKIE_3", "ck4"));
            cookies.Add(new HttpCookie("COOKIE_4", "ck5"));


            var form = new NameValueCollection();
            form.Add("FORM_B_XXX_B", "F1");
            form.Add("FORM_B_YYY_B", "F2");
            form.Add("FORM_C", "F3");
            form.Add("FORM_D", "F4");
            form.Add("FORM_E", "F5");

            var headers = new NameValueCollection();
            headers.Add("HEADER_B_XXX_B", "H1");
            headers.Add("HEADER_B_YYY_B", "H2");
            headers.Add("HEADER_C", "H3");
            headers.Add("HEADER_D", "H4");
            headers.Add("HEADER_E", "H5");


            _request.SetupGet(x => x.ServerVariables).Returns(servervVariables);
            _request.SetupGet(x => x.Cookies).Returns(cookies);
            _request.SetupGet(x => x.Form).Returns(form);
            _request.SetupGet(x => x.QueryString).Returns(new NameValueCollection());
            _request.SetupGet(x => x.Headers).Returns(headers);

            _context.SetupGet(x => x.Request).Returns(_request.Object);
            _context.SetupGet(x => x.Response).Returns(_response.Object);
        }

        [TestMethod]
        public void ShouldFilter_ServerVariables_ByRegexAndName()
        {

            var logFilterSettings = new HttpContextDataLogFilterSettings
            {
                ServerVarFilters = new List<HttpContextDataLogFilter>
                {
                    new HttpContextDataLogFilter {Name = "AUTH_U.*", ReplaceWith = "", NameIsRegex = true },
                    new HttpContextDataLogFilter {Name = "AUTH_PASSWORD", ReplaceWith = "***"},
                }
            };

            var ctxData = new HttpContextData(new ApplicationException("Test exception"), _context.Object, logFilterSettings);

            Assert.IsTrue(ctxData.ServerVariables.Count == 2);
            Assert.IsTrue(ctxData.ServerVariables["AUTH_PASSWORD"] == "***");
            Assert.IsTrue(ctxData.ServerVariables["AUTH_TYPE"] == "Forms");

        }


        [TestMethod]
        public void ShouldFilter_Cookies_ByRegexAndName()
        {

            var logFilterSettings = new HttpContextDataLogFilterSettings
            {          
                CookieFilters = new List<HttpContextDataLogFilter>
                {
                    new HttpContextDataLogFilter {Name = "COOKIE_1.*", ReplaceWith = "", NameIsRegex = true },
                    new HttpContextDataLogFilter {Name = "COOKIE_2", ReplaceWith = ""},
                    new HttpContextDataLogFilter {Name = "COOKIE_3", ReplaceWith = "***"},
                },
            };

            var ctxData = new HttpContextData(new ApplicationException("Test exception"), _context.Object, logFilterSettings);

            Assert.IsTrue(ctxData.Cookies.Count == 2);
            Assert.IsTrue(ctxData.Cookies["COOKIE_3"] == "***");
            Assert.IsTrue(ctxData.Cookies["COOKIE_4"] == "ck5");

        }

        [TestMethod]
        public void ShouldFilter_FormData_ByRegexAndName()
        {

            var logFilterSettings = new HttpContextDataLogFilterSettings
            {
                FormFilters = new List<HttpContextDataLogFilter>
                {
                    new HttpContextDataLogFilter {Name = "FORM_B_.*_B", ReplaceWith = "", NameIsRegex = true },
                    new HttpContextDataLogFilter {Name = "FORM_C", ReplaceWith = ""},
                    new HttpContextDataLogFilter {Name = "FORM_D", ReplaceWith = "***"},
                },
            };

            var ctxData = new HttpContextData(new ApplicationException("Test exception"), _context.Object, logFilterSettings);

            Assert.IsTrue(ctxData.Form.Count == 2);
            Assert.IsTrue(ctxData.Form["FORM_D"] == "***");
            Assert.IsTrue(ctxData.Form["FORM_E"] == "F5");

        }

        [TestMethod]
        public void ShouldFilter_Header_ByRegexAndName()
        {

            var logFilterSettings = new HttpContextDataLogFilterSettings
            {
                HeaderFilters = new List<HttpContextDataLogFilter>
                {
                    new HttpContextDataLogFilter {Name = "HEADER_B_.*_B", ReplaceWith = "", NameIsRegex = true },
                    new HttpContextDataLogFilter {Name = "HEADER_C", ReplaceWith = ""},
                    new HttpContextDataLogFilter {Name = "HEADER_D", ReplaceWith = "***"},
                },
            };

            var ctxData = new HttpContextData(new ApplicationException("Test exception"), _context.Object, logFilterSettings);

            Assert.IsTrue(ctxData.RequestHeaders.Count == 2);
            Assert.IsTrue(ctxData.RequestHeaders["HEADER_D"] == "***");
            Assert.IsTrue(ctxData.RequestHeaders["HEADER_E"] == "H5");

        }

    
    }
}
