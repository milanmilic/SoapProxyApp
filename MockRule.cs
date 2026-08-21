using System;

namespace SoapProxyApp
{
    public class MockRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsEnabled { get; set; } = true;
        public string UrlMatch { get; set; }
        public int StatusCode { get; set; } = 200;
        public string ContentType { get; set; } = "application/json";
        public string ResponseBody { get; set; }
    }
}
