using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using McpUnity.Editor;
using McpUnity.Server;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace McpUnity.Chat
{
    /// <summary>
    /// OAuth PKCE flow for Anthropic Claude Pro/Max/Team subscriptions.
    /// Mirrors the opencode-anthropic-auth flow exactly:
    ///
    ///   1. Generate PKCE (code_verifier + code_challenge)
    ///   2. Open browser to claude.ai/oauth/authorize?code=true&amp;...
    ///   3. User authorizes → browser lands on console.anthropic.com/oauth/code/callback
    ///      which displays the authorization code on screen
    ///   4. User copies the code and pastes it into Unity
    ///   5. Exchange code → access_token + refresh_token
    ///   6. Auto-refresh when token expires
    /// </summary>
    public static class McpChatOAuth
    {
        // OAuth constants — identical to opencode-anthropic-auth
        private const string ClientId    = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
        private const string RedirectUri = "https://console.anthropic.com/oauth/code/callback";
        private const string TokenEndpoint = "https://console.anthropic.com/v1/oauth/token";
        private const string Scope       = "org:create_api_key user:profile user:inference";
        public  const string OAuthBetaHeader = "oauth-2025-04-20,interleaved-thinking-2025-05-14";

        // EditorPrefs keys — persisted across sessions
        private const string AccessTokenPref  = "McpUnity_OAuthAccessToken";
        private const string RefreshTokenPref = "McpUnity_OAuthRefreshToken";
        private const string TokenExpiryPref  = "McpUnity_OAuthTokenExpiry";

        // SessionState keys — in-memory only, cleared on domain reload (SEC-07)
        private const string CodeVerifierKey = "McpUnity_OAuthCodeVerifier";
        private const string OAuthStateKey   = "McpUnity_OAuthState";

        // Runtime state
        private static UnityWebRequest _pendingRequest;
        private static bool            _isExchanging;

        /// <summary>Last authorization URL built by StartLogin() — shown in UI for debug.</summary>
        public static string LastAuthorizationUrl { get; private set; } = "";

        // ── Token Storage ──────────────────────────────────────────────────────────
        #region Token Storage

        public static string AccessToken
        {
            get => EditorPrefs.GetString(AccessTokenPref, "");
            private set => EditorPrefs.SetString(AccessTokenPref, value);
        }

        public static string RefreshToken
        {
            get => EditorPrefs.GetString(RefreshTokenPref, "");
            private set => EditorPrefs.SetString(RefreshTokenPref, value);
        }

        /// <summary>Token expiry as Unix timestamp (milliseconds).</summary>
        public static long TokenExpiry
        {
            get => long.TryParse(EditorPrefs.GetString(TokenExpiryPref, "0"), out var v) ? v : 0;
            private set => EditorPrefs.SetString(TokenExpiryPref, value.ToString());
        }

        public static bool HasValidToken =>
            !string.IsNullOrEmpty(AccessToken) &&
            TokenExpiry > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static bool HasRefreshToken => !string.IsNullOrEmpty(RefreshToken);

        public static bool IsExchanging => _isExchanging;

        #endregion

        // ── PKCE ──────────────────────────────────────────────────────────────────
        #region PKCE Generation

        private static string GenerateCodeVerifier()
        {
            byte[] bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string ComputeCodeChallenge(string verifier)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier));
                return Base64UrlEncode(hash);
            }
        }

        private static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        #endregion

        // ── Step 1: Open Browser ──────────────────────────────────────────────────
        #region Step 1: Open Browser

        /// <summary>
        /// Start the OAuth flow: generate PKCE, open browser to Anthropic login.
        ///
        /// After the user clicks "Authorize", the browser is redirected to
        /// console.anthropic.com/oauth/code/callback which displays the code on screen.
        /// The user copies it and pastes it into the "Paste code" field in Unity.
        /// </summary>
        /// <param name="mode">"max" = claude.ai, "console" = console.anthropic.com</param>
        public static void StartLogin(string mode = "max")
        {
            string verifier = GenerateCodeVerifier();
            string challenge = ComputeCodeChallenge(verifier);

            // opencode-anthropic-auth uses state = verifier (same value).
            // Anthropic's token endpoint validates that state == code_verifier.
            string oauthState = verifier;

            // SEC-07: Store secrets in SessionState only (in-memory, never persisted to disk)
            SessionState.SetString(CodeVerifierKey, verifier);
            SessionState.SetString(OAuthStateKey, oauthState);

            string host = mode == "console" ? "console.anthropic.com" : "claude.ai";
            string url = $"https://{host}/oauth/authorize" +
                         $"?code=true" +
                         $"&client_id={ClientId}" +
                         $"&response_type=code" +
                         $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                         $"&scope={Uri.EscapeDataString(Scope)}" +
                         $"&code_challenge={challenge}" +
                         $"&code_challenge_method=S256" +
                         $"&state={oauthState}";

            LastAuthorizationUrl = url;
            Application.OpenURL(url);
            McpDebug.Log($"[Chat OAuth] Browser opened (host={host}, mode={mode}). Auth params redacted.");
        }

        #endregion

        // ── Step 2: Exchange Code ─────────────────────────────────────────────────
        #region Step 2: Exchange Code for Token

        /// <summary>
        /// Exchange the authorization code for access + refresh tokens.
        /// The code is pasted by the user from the console.anthropic.com redirect page.
        /// Supports optional "code#state" format.
        /// </summary>
        public static void ExchangeCode(string code, Action<string> onSuccess, Action<string> onError)
        {
            if (_isExchanging)
            {
                onError?.Invoke("Token exchange already in progress.");
                return;
            }

            string verifier = SessionState.GetString(CodeVerifierKey, "");
            if (string.IsNullOrEmpty(verifier))
            {
                onError?.Invoke("No PKCE verifier found. Click 'Login' first (or restart the Editor).");
                return;
            }

            // Support "code#state" format (if user copies both from the page)
            string[] splits      = code.Split('#');
            string   authCode    = splits[0].Trim();
            string   returnedState = splits.Length > 1 ? splits[1].Trim() : "";

            // SEC-08: CSRF state validation
            string expectedState = SessionState.GetString(OAuthStateKey, "");
            if (!string.IsNullOrEmpty(expectedState) && !string.IsNullOrEmpty(returnedState))
            {
                if (returnedState != expectedState)
                {
                    onError?.Invoke("OAuth state mismatch — possible CSRF. Please try logging in again.");
                    return;
                }
            }

            // opencode-anthropic-auth includes state in the exchange body.
            // Anthropic's token endpoint requires it (non-standard but enforced server-side).
            // Since state = verifier, both fields carry the same value.
            var body = new Dictionary<string, object>
            {
                ["code"]          = authCode,
                ["state"]         = returnedState,
                ["grant_type"]    = "authorization_code",
                ["client_id"]     = ClientId,
                ["redirect_uri"]  = RedirectUri,
                ["code_verifier"] = verifier
            };

            _isExchanging = true;
            SendTokenRequest(body, json =>
            {
                if (json.TryGetValue("access_token", out var at) &&
                    json.TryGetValue("expires_in",   out var ei))
                {
                    string accessToken = at.ToString();
                    int    expiresIn   = Convert.ToInt32(ei);

                    AccessToken = accessToken;
                    TokenExpiry = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)expiresIn * 1000;

                    if (json.TryGetValue("refresh_token", out var rt))
                        RefreshToken = rt.ToString();

                    // Clear ephemeral secrets
                    SessionState.EraseString(CodeVerifierKey);
                    SessionState.EraseString(OAuthStateKey);

                    McpDebug.Log($"[Chat OAuth] Login successful. Token expires in {expiresIn}s.");
                    onSuccess?.Invoke(accessToken);
                }
                else
                {
                    onError?.Invoke("Invalid token response from Anthropic.");
                }
            }, onError);
        }

        #endregion

        // ── Token Refresh ─────────────────────────────────────────────────────────
        #region Token Refresh

        public static void RefreshAccessToken(Action<string> onSuccess, Action<string> onError)
        {
            if (!HasRefreshToken)
            {
                onError?.Invoke("No refresh token available. Please login again.");
                return;
            }
            if (_isExchanging)
            {
                onError?.Invoke("Token refresh already in progress.");
                return;
            }

            var body = new Dictionary<string, object>
            {
                ["grant_type"]    = "refresh_token",
                ["refresh_token"] = RefreshToken,
                ["client_id"]     = ClientId
            };

            _isExchanging = true;
            SendTokenRequest(body, json =>
            {
                if (json.TryGetValue("access_token", out var at) &&
                    json.TryGetValue("expires_in",   out var ei))
                {
                    AccessToken = at.ToString();
                    TokenExpiry = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)Convert.ToInt32(ei) * 1000;
                    if (json.TryGetValue("refresh_token", out var rt))
                        RefreshToken = rt.ToString();
                    McpDebug.Log("[Chat OAuth] Token refreshed.");
                    onSuccess?.Invoke(AccessToken);
                }
                else
                {
                    onError?.Invoke("Failed to refresh token. Please login again.");
                }
            }, onError);
        }

        #endregion

        // ── Manual Token ──────────────────────────────────────────────────────────
        #region Manual Token

        /// <summary>
        /// Inject a bearer token manually (e.g. from browser devtools).
        /// Sets a 24-hour expiry.
        /// </summary>
        public static void SetManualToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;
            AccessToken  = token.Trim();
            RefreshToken = "";
            TokenExpiry  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 24L * 3600 * 1000;
            McpDebug.Log("[Chat OAuth] Manual bearer token set (expires in 24h).");
        }

        #endregion

        // ── Logout ────────────────────────────────────────────────────────────────
        #region Logout

        public static void Logout()
        {
            EditorPrefs.DeleteKey(AccessTokenPref);
            EditorPrefs.DeleteKey(RefreshTokenPref);
            EditorPrefs.DeleteKey(TokenExpiryPref);

            SessionState.EraseString(CodeVerifierKey);
            SessionState.EraseString(OAuthStateKey);

            // Legacy cleanup
            EditorPrefs.DeleteKey("McpUnity_OAuthCodeVerifier");
            EditorPrefs.DeleteKey("McpUnity_OAuthState");

            McpDebug.Log("[Chat OAuth] Logged out.");
        }

        #endregion

        // ── HTTP Token Request ────────────────────────────────────────────────────
        #region HTTP Helpers

        private static void SendTokenRequest(Dictionary<string, object> body,
                                              Action<Dictionary<string, object>> onSuccess,
                                              Action<string> onError)
        {
            string jsonBody  = JsonHelper.ToJson(body);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

            _pendingRequest = new UnityWebRequest(TokenEndpoint, "POST");
            _pendingRequest.uploadHandler             = new UploadHandlerRaw(bodyBytes);
            _pendingRequest.uploadHandler.contentType = "application/json";
            _pendingRequest.downloadHandler           = new DownloadHandlerBuffer();
            _pendingRequest.SetRequestHeader("Content-Type", "application/json");
            _pendingRequest.timeout = 30;
            _pendingRequest.SendWebRequest();

            void Poll()
            {
                if (_pendingRequest == null) { EditorApplication.update -= Poll; return; }
                if (!_pendingRequest.isDone) return;

                EditorApplication.update -= Poll;
                _isExchanging = false;

                if (_pendingRequest.result == UnityWebRequest.Result.ConnectionError ||
                    _pendingRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    string error        = _pendingRequest.error;
                    string responseBody = _pendingRequest.downloadHandler?.text ?? "";
                    if (!string.IsNullOrEmpty(responseBody))
                    {
                        try
                        {
                            var parsed = JsonHelper.ParseJsonObject(responseBody);
                            if (parsed != null && parsed.TryGetValue("error_description", out var desc))
                                error = desc.ToString();
                            else if (parsed != null && parsed.TryGetValue("error", out var err))
                                error = err.ToString();
                        }
                        catch (Exception parseEx) { McpDebug.LogWarning($"[Chat OAuth] Failed to parse error response: {parseEx.Message}"); }
                    }
                    McpDebug.LogError($"[Chat OAuth] Token request failed: {error}");
                    _pendingRequest.Dispose();
                    _pendingRequest = null;
                    onError?.Invoke(error);
                    return;
                }

                string text = _pendingRequest.downloadHandler.text;
                _pendingRequest.Dispose();
                _pendingRequest = null;

                try
                {
                    var json = JsonHelper.ParseJsonObject(text);
                    if (json != null)
                        onSuccess?.Invoke(json);
                    else
                        onError?.Invoke("Failed to parse token response.");
                }
                catch (Exception ex)
                {
                    onError?.Invoke($"Token parse error: {ex.Message}");
                }
            }

            EditorApplication.update += Poll;
        }

        #endregion
    }
}
