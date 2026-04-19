namespace McpUnity.Server
{
    /// <summary>
    /// Terrain Tools dispatcher - delegates to core, paint, detail and advanced registration methods.
    /// Contains 15 tools total (4 core + 3 paint + 3 detail + 5 advanced).
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterTerrainTools()
        {
            RegisterTerrainCoreTools();
            RegisterTerrainPaintTools();
            RegisterTerrainDetailTools();
            RegisterTerrainAdvancedTools();
        }
    }
}
