using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Security.RightsManagement;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Controls;

namespace WpfResxTranslator
{
    public sealed class TranslatorService
    {
        public static readonly Dictionary<string, string> DefaultLanguageMap = new Dictionary<string, string>
        {
            { "Indonesian (Bahasa)", "id" },
            { "Danish", "da" },
            { "Croatian", "hr" },
            { "Lithuanian", "lt" },
            { "Hungarian", "hu" },
            { "Norwegian", "no" },
            { "Polish", "pl" },
            { "Romanian", "ro" },
            { "Slovak", "sk" },
            { "Serbian", "sr" },
            { "Finnish", "fi" },
            { "Swedish", "sv" },
            { "Vietnamese", "vi" },
            { "Turkish", "tr" },
            { "Czech", "cs" },
            { "Greek", "el" },
            { "Bulgarian", "bg" },
            { "Russian", "ru" },
            { "Ukrainian", "uk" }
        };

        public static readonly string[] DefaultExcludeList = new[] {
            "RPC", "RemotePC", "RemotePC Viewer", "Performance", "Classic",
            "Performance Viewer", "Classic Viewer", "Google", "Microsoft",
            "Apple", "RPCNote", "SSO", "Google Authenticator",
            "\n", "\n\n", "%@", "%d", "%s", "%1$@", "%1$d", "%1$s", "%2$@", "%2$d", "%2$s",
            "(a-z, A-Z, 0-9)", "Chromebook", "Android", "Windows", "attended.remotepc.com", "https://www.google.com", "WOL", "© IDrive Inc", "All Rights Reserved", "LAN", "CancelDragOperation"
        };

        private string _apiKey = "";
        private string _model = "gpt-5-mini";
        public List<string> TargetLanguages { get; private set; }
        private string[] _exclude = new string[0];

        private readonly HttpClient _http;

        public TranslatorService()
        {
            TargetLanguages = new List<string>();
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public void Configure(string apiKey, string model, IEnumerable<string> targetLangs, string[] excludeList)
        {
            if (string.IsNullOrEmpty(apiKey)) throw new ArgumentNullException("apiKey");
            _apiKey = apiKey;
            if (!string.IsNullOrEmpty(model)) _model = model;
            TargetLanguages = (targetLangs != null) ? new List<string>(targetLangs) : new List<string>();
            _exclude = (excludeList != null && excludeList.Length > 0) ? excludeList : DefaultExcludeList;
        }

        public async Task<Dictionary<string, string>> TranslateBatchAsync((string key, string text)[] rows, string targetLang, CancellationToken ct)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (rows == null || rows.Length == 0) return result;

            var build = BuildPreservedPayload(rows);
            var payloadJson = build.Item1;
            var meta = build.Item2;

            var json = await CallOpenAiAsync(payloadJson, targetLang, ct);
            var parsed = ParseResponseJsonToDictionary(json);
            foreach (var kv in parsed)
            {
                if (!meta.ContainsKey(kv.Key)) continue;
                var mm = meta[kv.Key];
                var restored = RestorePreservedValue(kv.Value ?? string.Empty, mm);
                result[kv.Key] = restored;
            }
            return result;
        }

        private static Dictionary<string, string> ParseResponseJsonToDictionary(string json)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in root.EnumerateObject())
                        {
                            dict[prop.Name] = prop.Value.GetString() ?? string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse model JSON response: " + ex.Message);
            }
            return dict;
        }

        private static Tuple<string, Dictionary<string, Tuple<string, string, Dictionary<string, string>>>> BuildPreservedPayload((string key, string text)[] rows)
        {
            var payloadObj = new Dictionary<string, string>();
            var meta = new Dictionary<string, Tuple<string, string, Dictionary<string, string>>>();
            for (int i = 0; i < rows.Length; i++)
            {
                var key = rows[i].key;
                var original = rows[i].text ?? string.Empty;
                var lead = System.Text.RegularExpressions.Regex.Match(original, "^(\n*)").Value;
                var trail = System.Text.RegularExpressions.Regex.Match(original, "(\n*)$").Value;
                var work = original;
                var map = new Dictionary<string, string>();
                for (int j = 0; j < DefaultExcludeList.Length; j++)
                {
                    var w = DefaultExcludeList[j];
                    var ph = "__PLACEHOLDER_" + j + "__";
                    if (work.Contains(w))
                    {
                        map[ph] = w;
                        work = work.Replace(w, ph);
                    }
                }
                var trimmed = work.Trim();
                payloadObj[key] = trimmed;
                meta[key] = new Tuple<string, string, Dictionary<string, string>>(lead, trail, map);
            }
            var payloadJson = JsonSerializer.Serialize(payloadObj);
            return new Tuple<string, Dictionary<string, Tuple<string, string, Dictionary<string, string>>>>(payloadJson, meta);
        }

        private static string RestorePreservedValue(string translated, Tuple<string, string, Dictionary<string, string>> meta)
        {
            var outStr = translated ?? string.Empty;
            var map = meta.Item3;
            foreach (var kv in map)
            {
                outStr = outStr.Replace(kv.Key, kv.Value);
            }
            return meta.Item1 + outStr + meta.Item2;
        }

        private async Task<string> CallOpenAiAsync(string payloadJson, string targetLang, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_apiKey)) throw new InvalidOperationException("API key not configured.");
            var url = "https://api.openai.com/v1/chat/completions";
            var system = string.Join("", new[] {
                "You are a professional software localization translator.",
                "Translate ONLY the JSON values. Never change, add, remove, or reorder keys.",
                "Return VALID JSON ONLY (no markdown, no comments).",
                "Keep placeholders like __PLACEHOLDER_#__ exactly as-is.",
                "Do not translate brand names or placeholders.",
                "Preserve punctuation and casing."
            });
            var user = "Translate this JSON from English to " + targetLang + ". Return JSON only.";

        var body = new Dictionary<string, object>
            {
                { "model", _model },
                { "messages", new object[] {
                    new Dictionary<string,string>{{"role","system"},{"content", system}},
                    new Dictionary<string,string>{{"role","user"},{"content", user}},
                    new Dictionary<string,string>{{"role","user"},{"content", payloadJson}}
                } }
            };

        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Authorization", "Bearer " + _apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception("OpenAI error " + resp.StatusCode + ": " + text);

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(text))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var first = choices[0];
                        if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
                        {
                            var contentStr = content.GetString();
                            if (string.IsNullOrWhiteSpace(contentStr)) throw new Exception("No content from model");
        var firstIdx = contentStr.IndexOf('{');
        var lastIdx = contentStr.LastIndexOf('}');
                            if (firstIdx >= 0 && lastIdx >= firstIdx) return contentStr.Substring(firstIdx, lastIdx - firstIdx + 1);
                            return contentStr;
                        }
}
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse OpenAI response: " + ex.Message + " Raw: " + text);
            }
            throw new Exception("Unexpected OpenAI response structure. Raw: " + text);
        }
    }
}