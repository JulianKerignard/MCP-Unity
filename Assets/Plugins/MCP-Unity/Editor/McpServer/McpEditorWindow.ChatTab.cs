using UnityEditor;

namespace McpUnity.Editor
{
    public partial class McpEditorWindow
    {
        // ====================================================================
        // Tab 0: Chat
        // ====================================================================

        private void DrawChatTab()
        {
            _chatPanel?.Draw();
        }
    }
}
