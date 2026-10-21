using System;
using Newtonsoft.Json;

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
        public byte[] RequestBodyBytes { get; set; }
        public string ResponseHeaders { get; set; }
        public string ResponseBody { get; set; }
        public byte[] ResponseBodyBytes { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string SoapAction { get; set; }
        public string ProcessName { get; set; }
        public long DurationMs { get; set; }

        [JsonIgnore]
        public string FormattedDuration
        {
            get
            {
                if (DurationMs >= 1000) return $"{DurationMs / 1000.0:F2} s";
                return $"{DurationMs} ms";
            }
        }

        [JsonIgnore]
        public string this[string headerName] => GetHeaderValue(headerName);

        public string GetHeaderValue(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName)) return "";
            string val = ExtractHeader(RequestHeaders, headerName);
            if (string.IsNullOrEmpty(val))
                val = ExtractHeader(ResponseHeaders, headerName);

            if (string.IsNullOrEmpty(val)) return "";

            val = val.Trim().Trim('"');
            if (headerName.Equals("SOAPAction", StringComparison.OrdinalIgnoreCase))
            {
                int lastSlash = val.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < val.Length - 1)
                {
                    string actionName = val.Substring(lastSlash + 1);
                    return $"{actionName}  ({val})";
                }
            }
            return val;
        }

        private static string ExtractHeader(string headersBlock, string headerName)
        {
            if (string.IsNullOrWhiteSpace(headersBlock)) return "";
            string prefix = headerName.Trim() + ":";
            var lines = headersBlock.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(prefix.Length).Trim();
                }
            }
            return "";
        }
        
        [JsonIgnore]
        public long ContentLength
        {
            get
            {
                if (ResponseBodyBytes != null) return ResponseBodyBytes.LongLength;
                if (!string.IsNullOrEmpty(ResponseBody)) return System.Text.Encoding.UTF8.GetByteCount(ResponseBody);
                return 0;
            }
        }

        [JsonIgnore]
        public string FormattedLength
        {
            get
            {
                if (ContentLength > 1024 * 1024) return $"{ContentLength / (1024 * 1024.0):F2} MB";
                if (ContentLength > 1024) return $"{ContentLength / 1024.0:F1} KB";
                return $"{ContentLength} B";
            }
        }

        [JsonIgnore]
        public string StatusCategory
        {
            get
            {
                if (StatusCode >= 200 && StatusCode < 300) return "Success";
                if (StatusCode >= 400 || StatusCode == 0) return "Error";
                if (StatusCode >= 300 && StatusCode < 400) return "Warning";
                return "Neutral";
            }
        }

        public override string ToString()
        {
            return $"{Timestamp:HH:mm:ss} - {StatusCode} - {Url}";
        }
    }
}
