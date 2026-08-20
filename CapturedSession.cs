using System;

namespace SoapProxyApp
{
    public class CapturedSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Url { get; set; }
        public string Method { get; set; }
        public int StatusCode { get; set; }
        public string RequestHeaders { get; set; }
        public string RequestBody { get; set; }
        public string ResponseHeaders { get; set; }
        public string ResponseBody { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public override string ToString()
        {
            return $"{Timestamp:HH:mm:ss} - {StatusCode} - {Url}";
        }
    }
}
