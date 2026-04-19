# MCP Unity — Provider Configurations (9 Presets)

Updated February 2026. Source: `ProviderRegistry.cs`

## Provider Presets

### Anthropic Claude

| Field | Value |
|-------|-------|
| ID | `anthropic` |
| Endpoint | `https://api.anthropic.com/v1/messages` |
| Models | `claude-sonnet-4-6`, `claude-opus-4-6`, `claude-haiku-4-5`, `claude-sonnet-4-5` |
| Default | `claude-sonnet-4-6` |
| Context | 200,000 tokens |
| Max Tokens | 8,192 |
| Auth | API Key (`ANTHROPIC_API_KEY`) or OAuth PKCE |
| Key URL | https://console.anthropic.com/settings/keys |
| Provider class | `AnthropicProvider` (dedicated, not OpenAI-compat) |

### OpenAI

| Field | Value |
|-------|-------|
| ID | `openai` |
| Endpoint | `https://api.openai.com/v1/chat/completions` |
| Models | `gpt-4o`, `gpt-4o-mini`, `o3-mini`, `gpt-4.1`, `gpt-4.1-mini`, `gpt-4.1-nano` |
| Default | `gpt-4o` |
| Context | 128,000 tokens |
| Max Tokens | 4,096 |
| Auth | API Key (`OPENAI_API_KEY`) |
| Key URL | https://platform.openai.com/api-keys |

### Google Gemini

| Field | Value |
|-------|-------|
| ID | `google` |
| Endpoint | `https://generativelanguage.googleapis.com/v1beta/openai/chat/completions` |
| Models | `gemini-2.5-pro`, `gemini-2.5-flash`, `gemini-2.0-flash` |
| Default | `gemini-2.5-flash` |
| Context | 1,048,576 tokens (1M) |
| Max Tokens | 8,192 |
| Auth | API Key (`GEMINI_API_KEY`) |
| Key URL | https://aistudio.google.com/apikey |

### DeepSeek

| Field | Value |
|-------|-------|
| ID | `deepseek` |
| Endpoint | `https://api.deepseek.com/v1/chat/completions` |
| Models | `deepseek-chat` (V3.2), `deepseek-reasoner` (R1) |
| Default | `deepseek-chat` |
| Context | 128,000 tokens |
| Max Tokens | 4,096 |
| Auth | API Key (`DEEPSEEK_API_KEY`) |
| Key URL | https://platform.deepseek.com/api_keys |

### Groq

| Field | Value |
|-------|-------|
| ID | `groq` |
| Endpoint | `https://api.groq.com/openai/v1/chat/completions` |
| Models | `llama-3.3-70b-versatile`, `llama-3.1-8b-instant`, `meta-llama/llama-4-maverick-17b-128e-instruct`, `qwen/qwen3-32b` |
| Default | `llama-3.3-70b-versatile` |
| Context | 131,072 tokens |
| Max Tokens | 4,096 |
| Auth | API Key (`GROQ_API_KEY`) |
| Key URL | https://console.groq.com/keys |

### Mistral AI

| Field | Value |
|-------|-------|
| ID | `mistral` |
| Endpoint | `https://api.mistral.ai/v1/chat/completions` |
| Models | `mistral-large-2512` (Large 3), `mistral-small-2506` (Small 3.2), `codestral-2508`, `magistral-medium-2509` |
| Default | `mistral-large-2512` |
| Context | 128,000 tokens |
| Max Tokens | 4,096 |
| Auth | API Key (`MISTRAL_API_KEY`) |
| Key URL | https://console.mistral.ai/api-keys |

### Ollama (local)

| Field | Value |
|-------|-------|
| ID | `ollama` |
| Endpoint | `http://localhost:11434/v1/chat/completions` |
| Models | `llama3.3`, `qwen3`, `qwen2.5-coder`, `deepseek-r1`, `gemma3` |
| Default | `llama3.3` |
| Context | 131,072 tokens |
| Max Tokens | 4,096 |
| Auth | None (local) |

### LM Studio (local)

| Field | Value |
|-------|-------|
| ID | `lmstudio` |
| Endpoint | `http://localhost:1234/v1/chat/completions` |
| Models | `local-model` (any loaded model) |
| Default | `local-model` |
| Context | 131,072 tokens |
| Max Tokens | 4,096 |
| Auth | None (local) |

### Custom (OpenAI-Compatible)

| Field | Value |
|-------|-------|
| ID | `custom` |
| Endpoint | (user-defined) |
| Models | `custom-model` |
| Default | `custom-model` |
| Context | 128,000 tokens |
| Max Tokens | 4,096 |
| Auth | API Key (user-defined) |

---

## EditorPrefs Storage

All per-provider settings persist via Unity `EditorPrefs`:

| Key Pattern | Description |
|-------------|-------------|
| `McpUnity_ActiveProvider` | Currently active provider ID |
| `McpUnity_ProviderKey_{providerId}` | API key |
| `McpUnity_ProviderModel_{providerId}` | Selected model ID |
| `McpUnity_ProviderMaxTokens_{providerId}` | Max tokens (clamped 256-131072) |
| `McpUnity_ProviderEndpoint_{providerId}` | Custom endpoint override |
| `McpUnity_ProviderTemp_{providerId}` | Temperature (float 0.0-2.0, default 1.0) |

## Provider Architecture

- **Anthropic**: Uses dedicated `AnthropicProvider` class (Messages API format)
- **All others**: Use `OpenAICompatProvider` class (OpenAI chat completions format)
- Provider instances are cached in `ProviderCache` dictionary to avoid re-creation per IMGUI frame
