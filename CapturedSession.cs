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
        public long ContentLength { get; set; }

        public string FormattedLength
        {
            get
            {
                if (ContentLength > 1024 * 1024) return $"{ContentLength / (1024 * 1024.0):F2} MB";
                if (ContentLength > 1024) return $"{ContentLength / 1024.0:F1} KB";
                return $"{ContentLength} B";
            }
        }

        public string StatusCategory
        {
            get
            {
                if (StatusCode >= 200 && StatusCode < 300) return "Success";
                if (StatusCode >= 500) return "Error";
                if (StatusCode >= 400 && StatusCode < 500) return "Warning";
                return "Neutral";
            }
        }

        public override string ToString()
        {
            return $"{Timestamp:HH:mm:ss} - {StatusCode} - {Url}";
        }
    }
}
