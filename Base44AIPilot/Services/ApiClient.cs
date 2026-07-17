using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Base44AIPilot.Models;
using Base44AIPilot.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Base44AIPilot.Services
{
    public class ApiClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly Base44OptionsPage _options;

        private const string AgentApiBase = "https://app.base44.com";
        private const string AgentId      = "6a57fda9caabceffcbd70384";

        public ApiClient(Base44OptionsPage options)
        {
            if (options == null) throw new ArgumentNullException("options");
            _options = options;
            _http    = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        }

        // ------------------------------------------------------------------ //

        public async Task<ChatResponse> ChatAsync(ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                throw new InvalidOperationException(
                    "Base44 API key not configured. Go to Tools → Options → Base44 AI Pilot.");

            var conversationId = !string.IsNullOrEmpty(request.ConversationId)
                ? request.ConversationId
                : await GetOrCreateConversationAsync();

            var messageContent = BuildMessageContent(request);

            var body = new { role = "user", content = messageContent };
            var jsonBody = JsonConvert.SerializeObject(body);

            var url = string.Format("{0}/api/agents/{1}/conversations/{2}/messages",
                AgentApiBase, AgentId, conversationId);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response     = await _http.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(string.Format("Base44 API error {0}: {1}",
                    (int)response.StatusCode, responseBody));

            var json = JObject.Parse(responseBody);

            var result = new ChatResponse();
            result.ConversationId  = conversationId;
            result.MessageId       = json["id"] != null ? json["id"].ToString() : null;
            result.Response        = json["content"] != null ? json["content"].ToString()
                                   : json["message"] != null ? json["message"].ToString()
                                   : responseBody;

            int full = 0, summary = 0, tree = 0;
            foreach (var f in request.Files)
            {
                if (f.IsIncludedFull)            full++;
                else if (f.ContentSummary != null) summary++;
                else                              tree++;
            }
            result.FilesSentFull    = full;
            result.FilesSentSummary = summary;
            result.FilesTreeOnly    = tree;

            return result;
        }

        /// <summary>Local chunker — no HTTP round-trip needed.</summary>
        public ChunkResponse ChunkContext(ChunkRequest request)
        {
            var result = new ChunkResponse();
            int budget = request.TokenBudget > 0 ? request.TokenBudget : 12000;
            int used   = 0;

            var sorted = new List<SolutionFile>(request.Files);
            sorted.Sort(delegate(SolutionFile a, SolutionFile b)
            {
                if (a.FilePath == request.ActiveFilePath) return -1;
                if (b.FilePath == request.ActiveFilePath) return  1;
                int pa = TypePriority(a.FileType), pb = TypePriority(b.FileType);
                if (pa != pb) return pa.CompareTo(pb);
                return a.TokenEstimate.CompareTo(b.TokenEstimate);
            });

            foreach (var f in sorted)
            {
                var chunk = new ChunkedFile();
                chunk.FilePath      = f.FilePath;
                chunk.FileType      = f.FileType;
                chunk.TokenEstimate = f.TokenEstimate;

                if (used + f.TokenEstimate <= budget)
                {
                    chunk.IsIncludedFull = true;
                    chunk.Content        = f.Content;
                    used += f.TokenEstimate;
                    result.IncludedFull++;
                }
                else if (used + 60 <= budget)
                {
                    chunk.IsIncludedFull  = false;
                    chunk.ContentSummary  = BuildSummary(f);
                    used += 60;
                    result.IncludedSummary++;
                }
                else
                {
                    chunk.IsIncludedFull = false;
                    result.TreeOnly++;
                }

                result.ChunkedFiles.Add(chunk);
                result.TokensUsed = used;
                result.TotalFiles++;
            }

            return result;
        }

        // ------------------------------------------------------------------ //

        private async Task<string> GetOrCreateConversationAsync()
        {
            var url = string.Format("{0}/api/agents/{1}/conversations/default", AgentApiBase, AgentId);
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            try
            {
                var response = await _http.SendAsync(req);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(body);
                    if (json["id"] != null) return json["id"].ToString();
                }
            }
            catch { /* fall through to default */ }

            return "default";
        }

        private static string BuildMessageContent(ChatRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## SOLUTION CONTEXT");
            sb.AppendLine("Solution : " + request.SolutionName);
            if (!string.IsNullOrEmpty(request.SlnPath))
                sb.AppendLine("Path     : " + request.SlnPath);
            if (!string.IsNullOrEmpty(request.ActiveFilePath))
                sb.AppendLine("Active   : " + request.ActiveFilePath);
            sb.AppendLine("Intent   : " + request.Intent);
            sb.AppendLine();

            if (request.Files != null && request.Files.Count > 0)
            {
                sb.AppendLine("## FILE TREE");
                foreach (var f in request.Files)
                    sb.AppendLine("  [" + f.FileType + "] " + f.FilePath);
                sb.AppendLine();

                sb.AppendLine("## FILE CONTENTS");
                foreach (var f in request.Files)
                {
                    sb.AppendLine("### " + f.FilePath);
                    if (f.IsIncludedFull && f.Content != null)
                    {
                        var ext = System.IO.Path.GetExtension(f.FilePath).TrimStart('.');
                        sb.AppendLine("```" + ext);
                        sb.AppendLine(f.Content);
                        sb.AppendLine("```");
                    }
                    else if (f.ContentSummary != null)
                    {
                        sb.AppendLine("_(summary)_ " + f.ContentSummary);
                    }
                    else
                    {
                        sb.AppendLine("_(tree only)_");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine("## USER PROMPT");
            sb.AppendLine(request.Prompt);
            return sb.ToString();
        }

        private static string BuildSummary(SolutionFile f)
        {
            var lines = f.Content.Split('\n');
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (t.Length > 4 && !t.StartsWith("//") && !t.StartsWith("*") && !t.StartsWith("using "))
                    return t.Length > 120 ? t.Substring(0, 120) + "…" : t;
            }
            return f.FileType + " file (" + f.TokenEstimate + " tokens)";
        }

        private static int TypePriority(string type)
        {
            switch (type)
            {
                case "Controller":  return 1;
                case "Service":     return 2;
                case "Repository":  return 3;
                case "Model":       return 4;
                case "ViewModel":   return 5;
                case "DbContext":   return 6;
                case "Interface":   return 7;
                case "View":        return 8;
                case "Migration":   return 9;
                case "Config":      return 10;
                default:            return 11;
            }
        }

        public void Dispose() { _http.Dispose(); }
    }
}
