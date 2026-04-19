using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Chat
{
    /// <summary>
    /// Handles the chat input area: text field, send/stop buttons, keyboard shortcuts,
    /// drag-and-drop of assets/GameObjects, and asset reference chip display.
    /// Communicates with the host panel via callbacks.
    /// </summary>
    internal class McpChatInput
    {
        // ====================================================================
        // Callbacks
        // ====================================================================

        /// <summary>Fired when the user submits a message (Enter key or Send button).</summary>
        public event Action<string, List<AssetReference>> OnSendRequested;

        /// <summary>Fired when the user clicks Stop to cancel streaming.</summary>
        public event Action OnStopRequested;

        // ====================================================================
        // State
        // ====================================================================

        private string _inputText = "";
        private readonly List<AssetReference> _referencedAssets = new List<AssetReference>();
        private const int MaxReferencedAssets = 8;
        private bool _isDragHovering;
        private EditorWindow _hostWindow;

        // ====================================================================
        // Lifecycle
        // ====================================================================

        public void Initialize(EditorWindow host)
        {
            _hostWindow = host;
        }

        /// <summary>
        /// Set the input text programmatically (e.g. from welcome screen suggestions).
        /// </summary>
        public void SetInputText(string text)
        {
            _inputText = text;
        }

        /// <summary>
        /// Clear input text and referenced assets after sending.
        /// Returns the captured list of asset references before clearing.
        /// </summary>
        public List<AssetReference> ConsumeReferencedAssets()
        {
            var copy = new List<AssetReference>(_referencedAssets);
            _referencedAssets.Clear();
            return copy;
        }

        // ====================================================================
        // Draw
        // ====================================================================

        /// <summary>
        /// Draw the input area. Call from the host panel's OnGUI.
        /// </summary>
        /// <param name="isProcessing">Whether the API client is currently streaming.</param>
        /// <param name="hasAuth">Whether authentication is configured.</param>
        public void Draw(bool isProcessing, bool hasAuth)
        {
            McpChatStyles.EnsureInitialized();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool shouldSend = false;
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return && !e.shift
                && GUI.GetNameOfFocusedControl() == "ChatInput")
            {
                shouldSend = true;
                e.Use();
            }

            // Asset reference chips bar (above input)
            DrawAssetChips();

            EditorGUILayout.BeginHorizontal();

            GUI.SetNextControlName("ChatInput");
            _inputText = EditorGUILayout.TextArea(_inputText, McpChatStyles.Input, GUILayout.MinHeight(36), GUILayout.MaxHeight(80));

            // Drag & drop overlay on the TextArea
            Rect inputRect = GUILayoutUtility.GetLastRect();
            HandleDragAndDrop(inputRect);

            // Placeholder text overlay when input is empty and not focused
            if (string.IsNullOrEmpty(_inputText) && _referencedAssets.Count == 0
                && GUI.GetNameOfFocusedControl() != "ChatInput")
            {
                GUI.Label(inputRect, "  Ask about your project... (drag assets here)", McpChatStyles.Placeholder);
            }

            // Drop hint overlay when dragging
            if (_isDragHovering)
            {
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f, 0.15f);
                GUI.Box(inputRect, "", EditorStyles.helpBox);
                GUI.backgroundColor = oldBg;
                GUI.Label(inputRect, "Drop to reference asset", McpChatStyles.DropHint);
            }

            EditorGUILayout.BeginVertical(GUILayout.Width(60));

            if (isProcessing)
            {
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.9f, 0.35f, 0.3f);
                if (GUILayout.Button("Stop", GUILayout.Height(36)))
                {
                    OnStopRequested?.Invoke();
                }
                GUI.backgroundColor = oldBg;
            }
            else
            {
                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_inputText) || !hasAuth);
                if (GUILayout.Button("Send", GUILayout.Height(36)) || shouldSend)
                {
                    string text = _inputText.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        _inputText = "";
                        GUI.FocusControl(null);
                        OnSendRequested?.Invoke(text, new List<AssetReference>(_referencedAssets));
                    }
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            // Keyboard hints
            if (isProcessing)
                GUILayout.Label("Esc to cancel", McpChatStyles.Hint);
            else if (!string.IsNullOrEmpty(_inputText))
                GUILayout.Label("Enter = send  |  Shift+Enter = newline", McpChatStyles.Hint);

            if (!hasAuth)
                EditorGUILayout.HelpBox("Authentication required. Click Settings to configure.", MessageType.Warning);

            EditorGUILayout.EndVertical();
        }

        // ====================================================================
        // Drag & Drop
        // ====================================================================

        private void HandleDragAndDrop(Rect dropArea)
        {
            _isDragHovering = false;

            if (!dropArea.Contains(Event.current.mousePosition))
                return;

            switch (Event.current.type)
            {
                case EventType.DragUpdated:
                    if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                        _isDragHovering = true;
                        Event.current.Use();
                        _hostWindow?.Repaint();
                    }
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();

                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj == null) continue;
                        if (_referencedAssets.Count >= MaxReferencedAssets) break;

                        string assetPath = AssetDatabase.GetAssetPath(obj);
                        bool isSceneObject = false;
                        if (obj is GameObject goCheck)
                            isSceneObject = string.IsNullOrEmpty(assetPath) || goCheck.scene.IsValid();

                        var assetRef = new AssetReference();

                        if (isSceneObject && obj is GameObject sceneGo)
                        {
                            assetRef.displayName = sceneGo.name;
                            assetRef.gameObjectPath = McpUnity.Helpers.GameObjectHelpers.GetGameObjectPath(sceneGo);
                            assetRef.isSceneObject = true;
                            assetRef.typeName = "GameObject";
                        }
                        else if (!string.IsNullOrEmpty(assetPath))
                        {
                            var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                            assetRef.displayName = obj.name;
                            assetRef.assetPath = assetPath;
                            assetRef.isSceneObject = false;
                            assetRef.typeName = assetType?.Name ?? "Object";
                        }
                        else
                        {
                            continue;
                        }

                        // Avoid duplicates
                        bool exists = false;
                        for (int i = 0; i < _referencedAssets.Count; i++)
                        {
                            var existing = _referencedAssets[i];
                            if (existing.isSceneObject == assetRef.isSceneObject
                                && existing.displayName == assetRef.displayName
                                && existing.assetPath == assetRef.assetPath
                                && existing.gameObjectPath == assetRef.gameObjectPath)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            _referencedAssets.Add(assetRef);
                            string mention = "@" + assetRef.displayName;
                            if (string.IsNullOrEmpty(_inputText))
                                _inputText = mention + " ";
                            else if (!_inputText.Contains(mention))
                                _inputText = _inputText.TrimEnd() + " " + mention + " ";
                        }
                    }

                    Event.current.Use();
                    _hostWindow?.Repaint();
                    break;

                case EventType.DragExited:
                    _isDragHovering = false;
                    _hostWindow?.Repaint();
                    break;
            }
        }

        // ====================================================================
        // Asset Chips
        // ====================================================================

        private void DrawAssetChips()
        {
            if (_referencedAssets.Count == 0) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Refs:", EditorStyles.miniLabel, GUILayout.Width(30));

            int removeIndex = -1;
            for (int i = 0; i < _referencedAssets.Count; i++)
            {
                var assetRef = _referencedAssets[i];
                string icon = assetRef.isSceneObject ? "\u25CB " : "\u25A0 ";
                string label = icon + assetRef.displayName;
                string tooltip = assetRef.isSceneObject
                    ? $"Scene: {assetRef.gameObjectPath}"
                    : $"{assetRef.typeName}: {assetRef.assetPath}";

                GUILayout.Label(new GUIContent(label, tooltip), McpChatStyles.Chip);
                if (GUILayout.Button("x", McpChatStyles.ChipRemove))
                {
                    removeIndex = i;
                }
            }

            if (removeIndex >= 0)
            {
                string mention = "@" + _referencedAssets[removeIndex].displayName;
                _inputText = _inputText.Replace(mention, "").Replace("  ", " ").Trim();
                _referencedAssets.RemoveAt(removeIndex);
                _hostWindow?.Repaint();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
}
