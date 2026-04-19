namespace McpUnity.Server
{
    /// <summary>
    /// Editor tools dispatcher — delegates to EditorWorkflowTools, EditorScreenshotTools, EditorSelectionTools
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterEditorTools()
        {
            RegisterEditorWorkflowTools();
            RegisterEditorScreenshotTools();
            RegisterEditorSelectionTools();
        }
    }
}
