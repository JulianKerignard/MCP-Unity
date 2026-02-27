namespace McpUnity.Server
{
    /// <summary>
    /// UI Tools dispatcher - delegates to UICreationTools and UILayoutTools
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all UI-related tools by delegating to sub-registrations
        /// </summary>
        static partial void RegisterUITools()
        {
            RegisterUICreationTools();
            RegisterUILayoutTools();
        }
    }
}
