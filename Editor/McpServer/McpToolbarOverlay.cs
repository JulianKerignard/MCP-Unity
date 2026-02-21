using UnityEditor;
using UnityEngine;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace McpUnity.Editor
{
    /// <summary>
    /// Toolbar overlay for quick MCP server status and controls.
    /// Shows in the Scene view toolbar for easy access.
    /// </summary>
    [Overlay(typeof(SceneView), "MCP Status", true)]
    public class McpToolbarOverlay : ToolbarOverlay
    {
        public McpToolbarOverlay() : base(
            McpStatusElement.Id,
            McpToggleElement.Id)
        { }
    }

    /// <summary>
    /// Status indicator element for the toolbar
    /// </summary>
    [EditorToolbarElement(Id, typeof(SceneView))]
    public class McpStatusElement : EditorToolbarButton
    {
        public const string Id = "McpUnity/Status";

        public McpStatusElement()
        {
            text = "MCP";
            tooltip = "Click to open MCP Unity Server window";
            clicked += OnClick;

            UpdateStatus();
            McpServerStatus.OnServerStateChanged += _ => UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (McpServerStatus.IsRunning)
            {
                icon = SafeGetIcon("d_GreenCheckmark", "d_Valid");
                tooltip = $"MCP Server Running - {McpServerStatus.ConnectedClients} clients";
            }
            else
            {
                icon = SafeGetIcon("d_redLight", "d_console.erroricon.sml");
                tooltip = "MCP Server Stopped - Click to open settings";
            }
        }

        private static Texture2D SafeGetIcon(string primary, string fallback)
        {
            var content = EditorGUIUtility.IconContent(primary);
            if (content?.image != null) return content.image as Texture2D;
            content = EditorGUIUtility.IconContent(fallback);
            return content?.image as Texture2D;
        }

        private void OnClick()
        {
            McpEditorWindow.ShowWindow();
        }
    }

    /// <summary>
    /// Quick toggle button for starting/stopping server
    /// </summary>
    [EditorToolbarElement(Id, typeof(SceneView))]
    public class McpToggleElement : EditorToolbarButton
    {
        public const string Id = "McpUnity/Toggle";

        public McpToggleElement()
        {
            UpdateButton();
            clicked += OnClick;
            McpServerStatus.OnServerStateChanged += _ => UpdateButton();
        }

        private void UpdateButton()
        {
            if (McpServerStatus.IsRunning)
            {
                text = "Stop";
                icon = SafeGetIcon("d_PauseButton", "d_PauseButton On");
                tooltip = "Stop MCP Server";
            }
            else
            {
                text = "Start";
                icon = SafeGetIcon("d_PlayButton", "d_PlayButton On");
                tooltip = "Start MCP Server";
            }
        }

        private static Texture2D SafeGetIcon(string primary, string fallback)
        {
            var content = EditorGUIUtility.IconContent(primary);
            if (content?.image != null) return content.image as Texture2D;
            content = EditorGUIUtility.IconContent(fallback);
            return content?.image as Texture2D;
        }

        private void OnClick()
        {
            if (McpServerStatus.IsRunning)
            {
                McpServerStatus.Stop();
            }
            else
            {
                McpServerStatus.Start();
            }
        }
    }
}
