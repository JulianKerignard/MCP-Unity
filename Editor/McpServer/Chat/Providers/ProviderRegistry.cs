using System;
using System.Collections.Generic;
using UnityEditor;

namespace McpUnity.Chat.Providers
{
    /// <summary>
    /// Configuration preset for a known API provider.
    /// </summary>
    [Serializable]
    public class ProviderPreset
    {
        public string id;
        public string displayName;
        public string defaultEndpoint;
        public string[] modelIds;
        public string[] modelLabels;
        public string defaultModel;
        public string apiKeyEnvVar;
        public bool supportsTools;
        public int maxContextTokens;
        public int defaultMaxTokens;
        public bool isLocal;
        public string apiKeyUrl; // URL to get an API key

        public ProviderPreset(string id, string displayName, string defaultEndpoint,
            string[] modelIds, string[] modelLabels, string defaultModel,
            string apiKeyEnvVar, bool supportsTools, int maxContextTokens,
            int defaultMaxTokens = 4096, bool isLocal = false, string apiKeyUrl = null)
        {
            this.id = id;
            this.displayName = displayName;
            this.defaultEndpoint = defaultEndpoint;
            this.modelIds = modelIds;
            this.modelLabels = modelLabels;
            this.defaultModel = defaultModel;
            this.apiKeyEnvVar = apiKeyEnvVar;
            this.supportsTools = supportsTools;
            this.maxContextTokens = maxContextTokens;
            this.defaultMaxTokens = defaultMaxTokens;
            this.isLocal = isLocal;
            this.apiKeyUrl = apiKeyUrl;
        }
    }

    /// <summary>
    /// Registry of available LLM providers with preset configurations.
    /// Manages provider instances, per-provider API keys, and active provider selection.
    /// </summary>
    public static class ProviderRegistry
    {
        // EditorPrefs keys
        private const string ActiveProviderPref = "McpUnity_ActiveProvider";
        private const string ApiKeyPrefPrefix = "McpUnity_ProviderKey_"; // + providerId
        private const string ModelPrefPrefix = "McpUnity_ProviderModel_"; // + providerId
        private const string MaxTokensPrefPrefix = "McpUnity_ProviderMaxTokens_"; // + providerId
        private const string EndpointPrefPrefix = "McpUnity_ProviderEndpoint_"; // + providerId
        private const string TemperaturePrefPrefix = "McpUnity_ProviderTemp_"; // + providerId

        // Known provider presets (updated February 2026)
        private static readonly ProviderPreset[] KnownPresets =
        {
            new ProviderPreset("anthropic", "Anthropic Claude",
                "https://api.anthropic.com/v1/messages",
                new[] { "claude-sonnet-4-6", "claude-opus-4-6", "claude-haiku-4-5", "claude-sonnet-4-5" },
                new[] { "Claude Sonnet 4.6", "Claude Opus 4.6", "Claude Haiku 4.5", "Claude Sonnet 4.5" },
                "claude-sonnet-4-6", "ANTHROPIC_API_KEY",
                true, 200000, 8192, false, "https://console.anthropic.com/settings/keys"),

            new ProviderPreset("openai", "OpenAI",
                "https://api.openai.com/v1/chat/completions",
                new[] { "gpt-4o", "gpt-4o-mini", "o3-mini", "gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano" },
                new[] { "GPT-4o", "GPT-4o Mini", "o3 Mini", "GPT-4.1", "GPT-4.1 Mini", "GPT-4.1 Nano" },
                "gpt-4o", "OPENAI_API_KEY",
                true, 128000, 4096, false, "https://platform.openai.com/api-keys"),

            new ProviderPreset("google", "Google Gemini",
                "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
                new[] { "gemini-2.5-pro", "gemini-2.5-flash", "gemini-2.0-flash" },
                new[] { "Gemini 2.5 Pro", "Gemini 2.5 Flash", "Gemini 2.0 Flash" },
                "gemini-2.5-flash", "GEMINI_API_KEY",
                true, 1048576, 8192, false, "https://aistudio.google.com/apikey"),

            new ProviderPreset("deepseek", "DeepSeek",
                "https://api.deepseek.com/v1/chat/completions",
                new[] { "deepseek-chat", "deepseek-reasoner" },
                new[] { "DeepSeek Chat (V3.2)", "DeepSeek Reasoner (R1)" },
                "deepseek-chat", "DEEPSEEK_API_KEY",
                true, 128000, 4096, false, "https://platform.deepseek.com/api_keys"),

            new ProviderPreset("groq", "Groq",
                "https://api.groq.com/openai/v1/chat/completions",
                new[] { "llama-3.3-70b-versatile", "llama-3.1-8b-instant", "meta-llama/llama-4-maverick-17b-128e-instruct", "qwen/qwen3-32b" },
                new[] { "Llama 3.3 70B", "Llama 3.1 8B", "Llama 4 Maverick 17B", "Qwen3 32B" },
                "llama-3.3-70b-versatile", "GROQ_API_KEY",
                true, 131072, 4096, false, "https://console.groq.com/keys"),

            new ProviderPreset("mistral", "Mistral AI",
                "https://api.mistral.ai/v1/chat/completions",
                new[] { "mistral-large-2512", "mistral-small-2506", "codestral-2508", "magistral-medium-2509" },
                new[] { "Mistral Large 3", "Mistral Small 3.2", "Codestral", "Magistral Medium" },
                "mistral-large-2512", "MISTRAL_API_KEY",
                true, 128000, 4096, false, "https://console.mistral.ai/api-keys"),

            new ProviderPreset("ollama", "Ollama (local)",
                "http://localhost:11434/v1/chat/completions",
                new[] { "llama3.3", "qwen3", "qwen2.5-coder", "deepseek-r1", "gemma3" },
                new[] { "Llama 3.3", "Qwen3", "Qwen 2.5 Coder", "DeepSeek R1", "Gemma 3" },
                "llama3.3", null,
                true, 131072, 4096, true),

            new ProviderPreset("lmstudio", "LM Studio (local)",
                "http://localhost:1234/v1/chat/completions",
                new[] { "local-model" },
                new[] { "Local Model" },
                "local-model", null,
                true, 131072, 4096, true),

            new ProviderPreset("custom", "Custom OpenAI-Compatible",
                "",
                new[] { "custom-model" },
                new[] { "Custom Model" },
                "custom-model", null,
                true, 128000, 4096)
        };

        // Cached provider instances
        private static readonly Dictionary<string, IChatProvider> ProviderCache = new Dictionary<string, IChatProvider>();

        // Cached arrays (avoid allocation per IMGUI frame)
        private static string[] _cachedPresetIds;
        private static string[] _cachedPresetLabels;

        /// <summary>Get all known preset IDs (cached).</summary>
        public static string[] GetPresetIds()
        {
            if (_cachedPresetIds == null)
            {
                _cachedPresetIds = new string[KnownPresets.Length];
                for (int i = 0; i < KnownPresets.Length; i++)
                    _cachedPresetIds[i] = KnownPresets[i].id;
            }
            return _cachedPresetIds;
        }

        /// <summary>Get all known preset display names (cached).</summary>
        public static string[] GetPresetLabels()
        {
            if (_cachedPresetLabels == null)
            {
                _cachedPresetLabels = new string[KnownPresets.Length];
                for (int i = 0; i < KnownPresets.Length; i++)
                    _cachedPresetLabels[i] = KnownPresets[i].displayName;
            }
            return _cachedPresetLabels;
        }

        /// <summary>Get preset by ID.</summary>
        public static ProviderPreset GetPreset(string id)
        {
            foreach (var p in KnownPresets)
                if (p.id == id) return p;
            return null;
        }

        /// <summary>Number of known presets.</summary>
        public static int PresetCount => KnownPresets.Length;

        /// <summary>Index of a provider ID in the presets array, or -1.</summary>
        public static int IndexOf(string id)
        {
            for (int i = 0; i < KnownPresets.Length; i++)
                if (KnownPresets[i].id == id) return i;
            return -1;
        }

        // ====================================================================
        // Provider Instance Management
        // ====================================================================

        /// <summary>Get or create a provider instance for the given preset ID.</summary>
        public static IChatProvider GetProvider(string id)
        {
            if (ProviderCache.TryGetValue(id, out var cached))
                return cached;

            IChatProvider provider;
            if (id == "anthropic")
            {
                var preset = GetPreset(id);
                provider = new AnthropicProvider(preset);
            }
            else
            {
                var preset = GetPreset(id);
                if (preset == null)
                {
                    // Fallback to custom with the id as label
                    preset = new ProviderPreset(id, id, "", new[] { "model" }, new[] { "Model" },
                        "model", null, true, 128000);
                }
                provider = new OpenAICompatProvider(preset);
            }

            ProviderCache[id] = provider;
            return provider;
        }

        /// <summary>Get the currently active provider.</summary>
        public static IChatProvider GetActiveProvider()
        {
            return GetProvider(ActiveProviderId);
        }

        // ====================================================================
        // Per-Provider Settings (EditorPrefs)
        // ====================================================================

        /// <summary>Active provider ID (persisted in EditorPrefs).</summary>
        public static string ActiveProviderId
        {
            get => EditorPrefs.GetString(ActiveProviderPref, "anthropic");
            set => EditorPrefs.SetString(ActiveProviderPref, value);
        }

        /// <summary>Get the stored API key for a provider.</summary>
        public static string GetApiKey(string providerId)
        {
            return EditorPrefs.GetString(ApiKeyPrefPrefix + providerId, "");
        }

        /// <summary>Set the API key for a provider.</summary>
        public static void SetApiKey(string providerId, string key)
        {
            EditorPrefs.SetString(ApiKeyPrefPrefix + providerId, key);
        }

        /// <summary>Get the selected model for a provider.</summary>
        public static string GetModel(string providerId)
        {
            var provider = GetProvider(providerId);
            return EditorPrefs.GetString(ModelPrefPrefix + providerId, provider.DefaultModel);
        }

        /// <summary>Set the selected model for a provider.</summary>
        public static void SetModel(string providerId, string model)
        {
            EditorPrefs.SetString(ModelPrefPrefix + providerId, model);
        }

        /// <summary>Get the max tokens setting for a provider.</summary>
        public static int GetMaxTokens(string providerId)
        {
            var provider = GetProvider(providerId);
            return EditorPrefs.GetInt(MaxTokensPrefPrefix + providerId, provider.DefaultMaxTokens);
        }

        /// <summary>Set the max tokens for a provider.</summary>
        public static void SetMaxTokens(string providerId, int tokens)
        {
            EditorPrefs.SetInt(MaxTokensPrefPrefix + providerId, UnityEngine.Mathf.Clamp(tokens, 256, 131072));
        }

        /// <summary>Get the temperature for a provider (0.0 - 2.0, default 1.0).</summary>
        public static float GetTemperature(string providerId)
        {
            // EditorPrefs has no float overload with default, so use string
            string stored = EditorPrefs.GetString(TemperaturePrefPrefix + providerId, "");
            if (float.TryParse(stored, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float val))
                return UnityEngine.Mathf.Clamp(val, 0f, 2f);
            return 1f;
        }

        /// <summary>Set the temperature for a provider.</summary>
        public static void SetTemperature(string providerId, float temperature)
        {
            float clamped = UnityEngine.Mathf.Clamp(temperature, 0f, 2f);
            EditorPrefs.SetString(TemperaturePrefPrefix + providerId,
                clamped.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Get custom endpoint override for a provider.</summary>
        public static string GetCustomEndpoint(string providerId)
        {
            return EditorPrefs.GetString(EndpointPrefPrefix + providerId, "");
        }

        /// <summary>Set custom endpoint for a provider. Rejects unsafe schemes/hosts (SSRF).</summary>
        public static void SetCustomEndpoint(string providerId, string endpoint)
        {
            if (!string.IsNullOrEmpty(endpoint) && !IsEndpointSafe(endpoint, out string reason))
            {
                UnityEngine.Debug.LogError($"[MCP-Unity] Rejected unsafe custom endpoint for '{providerId}': {reason}");
                return;
            }
            EditorPrefs.SetString(EndpointPrefPrefix + providerId, endpoint);
        }

        /// <summary>
        /// Validate that a custom endpoint URL is safe to use.
        /// Blocks file://, javascript:, ftp://, cloud-metadata IPs, and link-local ranges (SSRF).
        /// Allows https:// anywhere, and http:// only on loopback.
        /// </summary>
        public static bool IsEndpointSafe(string endpoint, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(endpoint)) { reason = "empty endpoint"; return false; }

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri uri))
            {
                reason = "not a well-formed absolute URI";
                return false;
            }

            string scheme = uri.Scheme.ToLowerInvariant();
            if (scheme != "http" && scheme != "https")
            {
                reason = $"scheme '{uri.Scheme}' not allowed (only http/https)";
                return false;
            }

            string host = uri.Host.ToLowerInvariant();
            bool isLoopback = host == "localhost" || host == "127.0.0.1" || host == "::1";

            if (scheme == "http" && !isLoopback)
            {
                reason = "plain http is only allowed on localhost (use https)";
                return false;
            }

            // Block well-known cloud metadata + link-local ranges regardless of scheme.
            if (host.StartsWith("169.254.") ||                   // AWS / Azure IMDS, link-local IPv4
                host == "100.100.100.200" ||                     // Alibaba Cloud
                host == "metadata.google.internal" ||            // GCP
                host.StartsWith("fe80:") ||                      // IPv6 link-local
                host.StartsWith("fd") ||                         // IPv6 ULA private
                host.StartsWith("10.") ||                        // RFC1918 (private)
                host.StartsWith("192.168.") ||
                IsPrivate172(host))
            {
                if (!isLoopback)
                {
                    reason = $"host '{host}' is in a blocked private/metadata range";
                    return false;
                }
            }

            return true;
        }

        private static bool IsPrivate172(string host)
        {
            // 172.16.0.0 – 172.31.255.255
            if (!host.StartsWith("172.")) return false;
            string[] parts = host.Split('.');
            if (parts.Length < 2) return false;
            if (!int.TryParse(parts[1], out int second)) return false;
            return second >= 16 && second <= 31;
        }

        /// <summary>Resolve auth for the active provider using its stored API key.</summary>
        public static AuthResult ResolveActiveAuth()
        {
            var provider = GetActiveProvider();
            string key = GetApiKey(provider.Id);
            return provider.ResolveAuth(key);
        }
    }
}
