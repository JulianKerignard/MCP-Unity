using UnityEditor;
using UnityEngine;

namespace McpUnity.Chat
{
    /// <summary>
    /// Centralizes all GUIStyle definitions and color constants for the chat panel.
    /// Styles are lazily initialized and automatically refreshed on Unity theme changes (light/dark).
    /// </summary>
    internal static class McpChatStyles
    {
        // Bubble styles
        public static GUIStyle UserBubble { get; private set; }
        public static GUIStyle AssistantBubble { get; private set; }
        public static GUIStyle ToolBubble { get; private set; }
        public static GUIStyle ErrorBubble { get; private set; }
        public static GUIStyle SystemBubble { get; private set; }

        // Input
        public static GUIStyle Input { get; private set; }
        public static GUIStyle Placeholder { get; private set; }

        // Code & Markdown
        public static GUIStyle CodeBlock { get; private set; }
        public static GUIStyle H1 { get; private set; }
        public static GUIStyle H2 { get; private set; }
        public static GUIStyle H3 { get; private set; }
        public static GUIStyle Blockquote { get; private set; }

        // Buttons & Labels
        public static GUIStyle CopyButton { get; private set; }
        public static GUIStyle LangLabel { get; private set; }
        public static GUIStyle CopiedLabel { get; private set; }
        public static GUIStyle RoleLabel { get; private set; }
        public static GUIStyle Hint { get; private set; }
        public static GUIStyle LinkButton { get; private set; }

        // Table
        public static GUIStyle TableHeader { get; private set; }
        public static GUIStyle TableCell { get; private set; }

        // Asset chips (drag & drop)
        public static GUIStyle Chip { get; private set; }
        public static GUIStyle ChipRemove { get; private set; }
        public static GUIStyle DropHint { get; private set; }

        // Timestamps & streaming
        public static GUIStyle Timestamp { get; private set; }
        public static GUIStyle Thinking { get; private set; }

        // Welcome screen (UX-03)
        public static GUIStyle WelcomeHeader { get; private set; }
        public static GUIStyle WelcomeSub { get; private set; }
        public static GUIStyle Suggestion { get; private set; }

        // Compact tool/result one-liner
        public static GUIStyle ToolCompact { get; private set; }

        // Initialization tracking
        private static bool _initialized;

        // SEC-#428: cache the dynamic Font once — Font.CreateDynamicFontFromOSFont creates a
        // native object that Unity does not GC, so re-creating it on theme change leaks memory.
        private static Font _cachedMonoFont;
        private static Font GetMonoFont()
        {
            if (_cachedMonoFont == null)
            {
                string fontName = Application.platform == RuntimePlatform.OSXEditor ? "Menlo" : "Consolas";
                _cachedMonoFont = Font.CreateDynamicFontFromOSFont(fontName, 11);
            }
            return _cachedMonoFont;
        }
        private static bool _lastProSkin;

        /// <summary>
        /// Ensures styles are initialized. Call once per OnGUI frame.
        /// Automatically re-initializes when the Unity theme changes.
        /// </summary>
        public static void EnsureInitialized()
        {
            bool isProSkin = EditorGUIUtility.isProSkin;
            if (_initialized && isProSkin == _lastProSkin) return;
            _lastProSkin = isProSkin;

            InitializeAll(isProSkin);
            _initialized = true;
        }

        private static void InitializeAll(bool isProSkin)
        {
            UserBubble = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(50, 4, 2, 2),
                wordWrap = true, richText = true, fontSize = 12
            };

            AssistantBubble = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(4, 50, 2, 2),
                wordWrap = true, richText = true, fontSize = 12
            };

            ToolBubble = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(20, 20, 1, 1),
                wordWrap = true, richText = true, fontSize = 11
            };

            ErrorBubble = new GUIStyle(ToolBubble);

            SystemBubble = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(20, 20, 4, 4),
                wordWrap = true, richText = true, fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };

            Input = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true, fontSize = 13,
                padding = new RectOffset(8, 8, 6, 6)
            };

            CodeBlock = new GUIStyle(EditorStyles.label)
            {
                font = GetMonoFont(),
                richText = true,
                wordWrap = true,
                fontSize = 11,
                padding = new RectOffset(12, 12, 8, 8),
                margin = new RectOffset(4, 4, 2, 2),
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };

            H1 = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18, richText = true, wordWrap = true,
                margin = new RectOffset(4, 4, 6, 4)
            };
            H2 = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15, richText = true, wordWrap = true,
                margin = new RectOffset(4, 4, 5, 3)
            };
            H3 = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13, richText = true, wordWrap = true,
                margin = new RectOffset(4, 4, 4, 2)
            };

            CopyButton = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                fixedWidth = 40,
                fixedHeight = 16,
                margin = new RectOffset(0, 4, 0, 0)
            };

            LangLabel = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                alignment = TextAnchor.UpperLeft // UX-06: Language label on left side of code block header
            };

            CopiedLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                richText = true
            };

            RoleLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                richText = true,
                margin = new RectOffset(4, 4, 6, 1)
            };

            TableHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                richText = true,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 4, 2, 2)
            };
            TableHeader.normal.textColor = isProSkin ? new Color(0.9f, 0.9f, 0.95f) : new Color(0.1f, 0.1f, 0.15f);

            TableCell = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                richText = true,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 4, 2, 2)
            };

            Placeholder = new GUIStyle(Input);
            Placeholder.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);

            Hint = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                richText = true,
                fontSize = 9
            };

            Chip = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                padding = new RectOffset(6, 4, 2, 2),
                margin = new RectOffset(2, 2, 2, 2),
                richText = true
            };

            ChipRemove = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 9,
                padding = new RectOffset(3, 3, 2, 2),
                margin = new RectOffset(0, 4, 2, 2),
                fixedWidth = 18,
                fixedHeight = 18
            };

            DropHint = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };

            Blockquote = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = true,
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                padding = new RectOffset(14, 10, 6, 6),
                margin = new RectOffset(12, 12, 2, 2), // UX-10: Symmetric margins for blockquotes
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            // UX-07: Timestamp style
            Timestamp = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                richText = true,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.6f) },
                margin = new RectOffset(4, 4, 0, 2)
            };

            // UX-01: Thinking indicator style
            Thinking = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(4, 50, 2, 2),
                normal = { textColor = new Color(0.7f, 0.8f, 0.95f) }
            };

            // UX-03: Welcome screen styles
            WelcomeHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                wordWrap = true,
                margin = new RectOffset(20, 20, 40, 4)
            };

            WelcomeSub = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
                margin = new RectOffset(40, 40, 0, 20)
            };

            Suggestion = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                richText = true,
                wordWrap = true,
                padding = new RectOffset(12, 12, 8, 8),
                margin = new RectOffset(40, 40, 2, 2),
                alignment = TextAnchor.MiddleLeft
            };

            // UX-05: Link button style
            LinkButton = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                richText = true,
                normal = { textColor = new Color(0.38f, 0.69f, 0.94f) },
                hover = { textColor = new Color(0.55f, 0.80f, 1f) },
                margin = new RectOffset(8, 4, 0, 2)
            };

            // Compact tool/result one-liner (clickable foldout-like)
            ToolCompact = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                fontSize = 10,
                padding = new RectOffset(6, 6, 3, 3),
                margin = new RectOffset(24, 24, 1, 1),
                wordWrap = false
            };
        }
    }
}
