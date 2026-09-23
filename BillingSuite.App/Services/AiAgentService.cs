using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BillingSuite.App.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BillingSuite.App.Services
{
    public class AiAgentService
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Reads the base URL from settings so it can be pointed at a proxy or
        /// a compatible endpoint without rebuilding the app.
        /// </summary>
        private string BaseUrl =>
            AppSettingsService.GetString(AppSettingKeys.AiBaseUrl,
                "https://generativelanguage.googleapis.com/v1beta/models/");

        /// <summary>Reads the model name from settings.</summary>
        private string Model =>
            AppSettingsService.GetString(AppSettingKeys.AiModel, "gemini-1.5-flash");

        public AiAgentService()
        {
            _httpClient = new HttpClient();
        }

        private string? _cachedKey = null;

        // Stored under %LOCALAPPDATA% rather than beside the exe: an installed app
        // under Program Files cannot write next to itself.
        private string GetConfigPath()
        {
            return AppPaths.SettingsFile;
        }

        public string? GetApiKey()
        {
            if (!string.IsNullOrEmpty(_cachedKey)) return _cachedKey;

            // Priority 1: DB-backed settings (the canonical store going forward)
            var fromDb = AppSettingsService.GetString(AppSettingKeys.AiApiKey);
            if (!string.IsNullOrWhiteSpace(fromDb))
            {
                _cachedKey = fromDb;
                return _cachedKey;
            }

            // Priority 2: Legacy JSON file (backward compat for existing installs)
            try
            {
                var path = GetConfigPath();
                if (System.IO.File.Exists(path))
                {
                    var json = System.IO.File.ReadAllText(path);
                    var obj = JObject.Parse(json);

                    var keys = obj["Global"]?["GeminiApiKeys"] as JArray;
                    if (keys != null && keys.Count > 0)
                        _cachedKey = keys[0].ToString();

                    if (string.IsNullOrEmpty(_cachedKey))
                        _cachedKey = obj["ApiKey"]?.ToString();

                    // Migrate to DB so future reads use the canonical path
                    if (!string.IsNullOrWhiteSpace(_cachedKey))
                        AppSettingsService.Set(AppSettingKeys.AiApiKey, _cachedKey);

                    return _cachedKey;
                }
            }
            catch { }
            return null;
        }

        public void SaveApiKey(string key)
        {
            _cachedKey = key;
            // Write to DB (primary canonical store)
            AppSettingsService.Set(AppSettingKeys.AiApiKey, key);

            // Also keep the JSON file in sync for any legacy code paths
            try
            {
                var path = GetConfigPath();
                JObject obj;
                if (System.IO.File.Exists(path))
                    obj = JObject.Parse(System.IO.File.ReadAllText(path));
                else
                    obj = new JObject { ["Global"] = new JObject() };

                if (obj["Global"] == null) obj["Global"] = new JObject();
                obj["Global"]!["GeminiApiKeys"] = new JArray(key);
                System.IO.File.WriteAllText(path, obj.ToString());
            }
            catch { }
        }

        public async Task<Dictionary<string, string?>> PerformProductMatchingAsync(List<string> inputNames, List<string> dbNames)
        {
            var key = GetApiKey();
            if (string.IsNullOrEmpty(key) || inputNames.Count == 0) 
            {
                Console.WriteLine("AI Matching skipped: No API key or no items.");
                return new Dictionary<string, string?>();
            }

            // Increase limit significantly for large databases
            var sampleDb = dbNames.OrderBy(x => x).Take(10000).ToList();
            
            var prompt = new StringBuilder();
            prompt.AppendLine("You are a Senior Pharmaceutical Database Auditor.");
            prompt.AppendLine("YOUR GOAL: Match 'Imported Names' from a vendor invoice to our 'Local Database' product names.");
            prompt.AppendLine();
            prompt.AppendLine("EQUIVALENCY RULES:");
            prompt.AppendLine("1. Volume/Form conversion: Vendors use '200ML', '100ML', while local DB uses 'SYP' (Syrup), 'DRP' (Drop), 'SUSP' (Suspension). IF the brand matches, assume they are the same.");
            prompt.AppendLine("2. Handle dosage forms: 'TAB' (Tablet), 'CAP' (Capsule), 'SYP', 'INJ' (Injection), 'CRM' (Cream), 'GEL'.");
            prompt.AppendLine("3. If multiple local products match the brand, pick the one with the closest strength/volume.");
            prompt.AppendLine("4. If the strength matches exactly (e.g. 500), but the name is slightly different, prioritize the match.");
            prompt.AppendLine("5. OUTPUT: A local name if likely matched, otherwise null.");
            prompt.AppendLine();
            prompt.AppendLine("MATCHING GUIDELINES:");
            prompt.AppendLine(" - 'ZIVZYME 200ML' -> 'ZIVZYME SYP' is a MATCH.");
            prompt.AppendLine(" - 'PARA 500' -> 'PARACETAMOL 500MG' is a MATCH. ");
            prompt.AppendLine();
            prompt.AppendLine("Respond ONLY with a JSON object: { \"action\": \"match_results\", \"data\": { \"Imported Name\": \"Local Database Name\" }, \"message\": \"...\" }");
            prompt.AppendLine();
            prompt.AppendLine("LOCAL DATABASE PRODUCTS:");
            prompt.AppendLine(string.Join("\n", sampleDb));
            prompt.AppendLine();
            prompt.AppendLine("IMPORTED NAMES TO MATCH:");
            prompt.AppendLine(string.Join("\n", inputNames));

            try
            {
                // Use the configured model for consistent JSON output
                var response = await CallGeminiApiAsync(Model, key, prompt.ToString());
                if (response != null && response.Data != null)
                {
                    // The matches are now inside the 'data' property of the AiActionResponse
                    return response.Data.ToObject<Dictionary<string, string?>>() ?? new Dictionary<string, string?>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Matching Error: {ex.Message}");
            }

            return new Dictionary<string, string?>();
        }

        public async Task<AiActionResponse> GetAiActionAsync(string userMessage, object? contextData = null, string? formName = null, string? systemPrompt = null)
        {
            var key = GetApiKey();
            var model = Model; // reads from AppSettingsService.AiModel
            
            if (string.IsNullOrEmpty(key))
            {
                return new AiActionResponse 
                { 
                    Action = "no_action", 
                    Message = "AI functionality is not configured. Please set your Gemini API Key in AI Settings." 
                };
            }

            string fullPrompt = ConstructPrompt(userMessage, systemPrompt ?? "You are a helpful AI assistant.", contextData, formName);

            try
            {
                var response = await CallGeminiApiAsync(model, key, fullPrompt);
                if (response != null)
                {
                    return response;
                }
            }
            catch (Exception ex)
            {
                return new AiActionResponse 
                { 
                    Action = "no_action", 
                    Message = $"AI Error: {ex.Message}" 
                };
            }

            return new AiActionResponse 
            { 
                Action = "no_action", 
                Message = "Failed to get a response from the AI." 
            };
        }

        private string ConstructPrompt(string userMessage, string systemPrompt, object? contextData, string? formName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SYSTEM INSTRUCTIONS:");
            sb.AppendLine("You are an AI Billing Assistant for a .NET WinForms business application.");
            sb.AppendLine("Your goal is to help the user manage billing and inventory.");
            sb.AppendLine("You can perform database actions ('create_purchase', 'create_bill') or direct UI actions ('active_ui_action').");
            sb.AppendLine("RESPOND ONLY IN VALID JSON FORMAT.");
            sb.AppendLine();
            
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                sb.AppendLine("ADDITIONAL GUIDELINES:");
                sb.AppendLine(systemPrompt);
                sb.AppendLine();
            }

            sb.AppendLine("LIVE CONTEXT:");
            sb.AppendLine($"Active Window: {formName ?? "Main Dashboard"}");
            if (contextData != null)
            {
                sb.AppendLine("Current Form Data:");
                sb.AppendLine(JsonConvert.SerializeObject(contextData, Formatting.Indented));
            }
            else
            {
                sb.AppendLine("No active document/form is currently being manipulated.");
            }
            sb.AppendLine();

            sb.AppendLine("USER REQUEST:");
            sb.AppendLine(userMessage);
            sb.AppendLine();
            sb.AppendLine("JSON STRUCTURE EXAMPLE:");
            sb.AppendLine("{ \"action\": \"active_ui_action\", \"data\": { \"products\": [...] }, \"message\": \"Adding items to your open bill...\" }");
            
            return sb.ToString();
        }

        private async Task<AiActionResponse> CallGeminiApiAsync(string model, string apiKey, string prompt)
        {
            var url = $"{BaseUrl}{model}:generateContent?key={apiKey}";
            
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            var jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API Error ({response.StatusCode}): {error}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var geminiData = JObject.Parse(jsonResponse);
            
            // Navigate Gemini's nested response: candidates[0].content.parts[0].text
            var text = geminiData["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
            
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new Exception("Empty response from Gemini.");
            }

            try {
                var aiResponse = JsonConvert.DeserializeObject<AiActionResponse>(text);
                return aiResponse ?? new AiActionResponse { Action = "no_action", Message = "Failed to parse AI JSON." };
            } catch (Exception ex) {
                throw new Exception($"JSON Parsing Error: {ex.Message}. Raw text: {text}");
            }
        }

        public async Task<string> GenerateContentRawAsync(string prompt, string? systemInstruction = null)
        {
            var key = GetApiKey();
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("Gemini API Key is not configured. Please set your Gemini API Key in Settings -> AI / Integrations.");

            var model = Model;
            var url = $"{BaseUrl}{model}:generateContent?key={key}";

            object requestBody;
            if (!string.IsNullOrWhiteSpace(systemInstruction))
            {
                requestBody = new
                {
                    system_instruction = new
                    {
                        parts = new[] { new { text = systemInstruction } }
                    },
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new
                    {
                        responseMimeType = "application/json"
                    }
                };
            }
            else
            {
                requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new
                    {
                        responseMimeType = "application/json"
                    }
                };
            }

            var jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API Error ({response.StatusCode}): {error}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var geminiData = JObject.Parse(jsonResponse);
            var text = geminiData["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new Exception("Empty response from Gemini.");
            }

            return text.Trim();
        }
    }
}
