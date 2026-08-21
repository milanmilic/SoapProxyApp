using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

namespace SoapProxyApp
{
    public static class MockRulesManager
    {
        private static readonly string filePath = "mock_rules.json";
        public static List<MockRule> Rules { get; set; } = new List<MockRule>();

        public static void LoadRules()
        {
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var loaded = JsonConvert.DeserializeObject<List<MockRule>>(json);
                    if (loaded != null) Rules = loaded;
                }
                catch { }
            }
        }

        public static void SaveRules()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Rules, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch { }
        }
        
        public static void AddRule(MockRule rule)
        {
            Rules.Add(rule);
            SaveRules();
        }
        
        public static void UpdateRule(MockRule rule)
        {
            var existing = Rules.FirstOrDefault(r => r.Id == rule.Id);
            if (existing != null)
            {
                existing.IsEnabled = rule.IsEnabled;
                existing.UrlMatch = rule.UrlMatch;
                existing.StatusCode = rule.StatusCode;
                existing.ContentType = rule.ContentType;
                existing.ResponseBody = rule.ResponseBody;
                SaveRules();
            }
        }
        
        public static void RemoveRule(Guid id)
        {
            Rules.RemoveAll(r => r.Id == id);
            SaveRules();
        }
    }
}
