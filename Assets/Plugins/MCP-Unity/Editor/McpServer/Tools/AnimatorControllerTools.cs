namespace McpUnity.Server
{
    /// <summary>
    /// Animator Controller tools dispatcher.
    /// Delegates to AnimatorControllerInfoTools, AnimatorValidationTools, and AnimatorFlowTools.
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterAnimatorControllerTools()
        {
            RegisterAnimatorControllerInfoTools();
            RegisterAnimatorValidationTools();
            RegisterAnimatorFlowTools();
        }
    }
}
