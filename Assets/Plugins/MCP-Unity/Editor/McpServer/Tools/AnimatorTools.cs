namespace McpUnity.Server
{
    /// <summary>
    /// Animator tools dispatcher. Delegates to sub-files:
    /// - AnimatorControllerTools.cs (6 tools: get controller, parameters, set parameter, add parameter, validate, flow)
    /// - AnimatorStateTools.cs (5 tools: add/delete/modify state, create blend tree, add blend motion)
    /// - AnimatorTransitionTools.cs (3 tools: add/delete/modify transition)
    /// - AnimatorClipTools.cs (2 tools: list clips, get clip info)
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterAnimatorTools()
        {
            RegisterAnimatorControllerTools();
            RegisterAnimatorStateTools();
            RegisterAnimatorTransitionTools();
            RegisterAnimatorClipTools();
        }
    }
}
