using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using McpUnity.Protocol;
using McpUnity.Helpers;

namespace McpUnity.Server
{
    /// <summary>
    /// UI Layout Tools - RectTransform configuration, layout groups, canvas scaler
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register UI layout tools: set_rect_transform, add_layout_group, set_canvas_scaler
        /// </summary>
        static partial void RegisterUILayoutTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_rect_transform",
                description = "Configure RectTransform properties for UI elements (anchors, pivot, position, size). Supports anchor presets like 'TopLeft', 'Center', 'StretchAll', etc.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to the UI GameObject"
                        },
                        ["anchorPreset"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Anchor preset: TopLeft, TopCenter, TopRight, MiddleLeft, Center, MiddleRight, BottomLeft, BottomCenter, BottomRight, StretchHorizontal, StretchVertical, StretchAll"
                        },
                        ["anchorMinX"] = new McpPropertySchema { type = "number", description = "Anchor min X (0-1)" },
                        ["anchorMinY"] = new McpPropertySchema { type = "number", description = "Anchor min Y (0-1)" },
                        ["anchorMaxX"] = new McpPropertySchema { type = "number", description = "Anchor max X (0-1)" },
                        ["anchorMaxY"] = new McpPropertySchema { type = "number", description = "Anchor max Y (0-1)" },
                        ["pivotX"] = new McpPropertySchema { type = "number", description = "Pivot X (0-1)" },
                        ["pivotY"] = new McpPropertySchema { type = "number", description = "Pivot Y (0-1)" },
                        ["posX"] = new McpPropertySchema { type = "number", description = "Anchored position X" },
                        ["posY"] = new McpPropertySchema { type = "number", description = "Anchored position Y" },
                        ["width"] = new McpPropertySchema { type = "number", description = "Width" },
                        ["height"] = new McpPropertySchema { type = "number", description = "Height" },
                        ["sizeDeltaX"] = new McpPropertySchema { type = "number", description = "Size delta X" },
                        ["sizeDeltaY"] = new McpPropertySchema { type = "number", description = "Size delta Y" },
                        ["offsetMinX"] = new McpPropertySchema { type = "number", description = "Offset min X (left)" },
                        ["offsetMinY"] = new McpPropertySchema { type = "number", description = "Offset min Y (bottom)" },
                        ["offsetMaxX"] = new McpPropertySchema { type = "number", description = "Offset max X (right)" },
                        ["offsetMaxY"] = new McpPropertySchema { type = "number", description = "Offset max Y (top)" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, SetRectTransform);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_layout_group",
                description = "Add a layout group component (Vertical, Horizontal, or Grid) to a UI element for automatic child positioning.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to the UI GameObject"
                        },
                        ["layoutType"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Layout type: 'Vertical', 'Horizontal', or 'Grid'"
                        },
                        ["spacing"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Spacing between elements"
                        },
                        ["paddingLeft"] = new McpPropertySchema { type = "integer", description = "Left padding" },
                        ["paddingRight"] = new McpPropertySchema { type = "integer", description = "Right padding" },
                        ["paddingTop"] = new McpPropertySchema { type = "integer", description = "Top padding" },
                        ["paddingBottom"] = new McpPropertySchema { type = "integer", description = "Bottom padding" },
                        ["childAlignment"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Child alignment: UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight"
                        },
                        ["cellSizeX"] = new McpPropertySchema { type = "number", description = "Grid cell width (Grid only)" },
                        ["cellSizeY"] = new McpPropertySchema { type = "number", description = "Grid cell height (Grid only)" },
                        ["constraint"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Grid constraint: 'Flexible', 'FixedColumnCount', 'FixedRowCount' (Grid only)"
                        },
                        ["constraintCount"] = new McpPropertySchema { type = "integer", description = "Number of columns/rows for constraint (Grid only)" },
                        ["controlChildWidth"] = new McpPropertySchema { type = "boolean", description = "Control child width" },
                        ["controlChildHeight"] = new McpPropertySchema { type = "boolean", description = "Control child height" },
                        ["childForceExpandWidth"] = new McpPropertySchema { type = "boolean", description = "Force expand width" },
                        ["childForceExpandHeight"] = new McpPropertySchema { type = "boolean", description = "Force expand height" }
                    },
                    required = new List<string> { "gameObjectPath", "layoutType" }
                }
            }, AddLayoutGroup);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_content_size_fitter",
                description = "Add a ContentSizeFitter to a UI element for automatic sizing based on content (text, layout children).",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to the UI GameObject"
                        },
                        ["horizontalFit"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Horizontal fit mode: 'Unconstrained', 'MinSize', 'PreferredSize'"
                        },
                        ["verticalFit"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Vertical fit mode: 'Unconstrained', 'MinSize', 'PreferredSize'"
                        }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, AddContentSizeFitter);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_layout_element",
                description = "Add a LayoutElement to control how a UI element behaves inside a LayoutGroup (min/preferred/flexible width/height).",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to the UI GameObject"
                        },
                        ["minWidth"] = new McpPropertySchema { type = "number", description = "Minimum width (-1 to disable)" },
                        ["minHeight"] = new McpPropertySchema { type = "number", description = "Minimum height (-1 to disable)" },
                        ["preferredWidth"] = new McpPropertySchema { type = "number", description = "Preferred width (-1 to disable)" },
                        ["preferredHeight"] = new McpPropertySchema { type = "number", description = "Preferred height (-1 to disable)" },
                        ["flexibleWidth"] = new McpPropertySchema { type = "number", description = "Flexible width (-1 to disable, 0+ to enable)" },
                        ["flexibleHeight"] = new McpPropertySchema { type = "number", description = "Flexible height (-1 to disable, 0+ to enable)" },
                        ["ignoreLayout"] = new McpPropertySchema { type = "boolean", description = "Ignore layout" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, AddLayoutElement);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_canvas_scaler",
                description = "Configure the CanvasScaler component on a Canvas for responsive UI scaling.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["canvasPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to the Canvas GameObject"
                        },
                        ["scaleMode"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Scale mode: 'ConstantPixelSize', 'ScaleWithScreenSize', 'ConstantPhysicalSize'"
                        },
                        ["referenceResolutionX"] = new McpPropertySchema { type = "number", description = "Reference width (for ScaleWithScreenSize)" },
                        ["referenceResolutionY"] = new McpPropertySchema { type = "number", description = "Reference height (for ScaleWithScreenSize)" },
                        ["matchWidthOrHeight"] = new McpPropertySchema { type = "number", description = "Match width (0) or height (1), 0-1 range" },
                        ["scaleFactor"] = new McpPropertySchema { type = "number", description = "Scale factor (for ConstantPixelSize)" },
                        ["referencePixelsPerUnit"] = new McpPropertySchema { type = "number", description = "Reference pixels per unit" }
                    },
                    required = new List<string> { "canvasPath" }
                }
            }, SetCanvasScaler);
        }

        #region Layout Tool Handlers

        private static McpToolResult SetRectTransform(Dictionary<string, object> args)
        {
            try
            {
                var (gameObjectPath, pathErr) = RequireArg(args, "gameObjectPath");
                if (pathErr != null) return pathErr;

                var go = GameObjectHelpers.FindGameObject(gameObjectPath);
                if (go == null)
                    return McpToolResult.Error($"GameObject not found: {gameObjectPath}");

                var rectTransform = go.GetComponent<RectTransform>();
                if (rectTransform == null)
                    return McpToolResult.Error($"GameObject '{gameObjectPath}' does not have a RectTransform component");

                Undo.RecordObject(rectTransform, "Set RectTransform");

                var modified = new List<string>();

                if (ArgumentParser.HasKey(args, "anchorPreset"))
                {
                    string preset = ArgumentParser.GetString(args, "anchorPreset", "");
                    if (!ApplyAnchorPreset(rectTransform, preset))
                        return McpToolResult.Error(
                            $"Unknown anchorPreset '{preset}'. Valid: topleft, top(center), topright, " +
                            "(middle)left, (middle)center, (middle)right, bottomleft, bottom(center), " +
                            "bottomright, stretchhorizontal, stretchvertical, stretch(all).");
                    modified.Add("anchorPreset");
                }

                if (ArgumentParser.HasKey(args, "anchorMinX") || ArgumentParser.HasKey(args, "anchorMinY"))
                {
                    rectTransform.anchorMin = new Vector2(
                        ArgumentParser.GetFloat(args, "anchorMinX", rectTransform.anchorMin.x),
                        ArgumentParser.GetFloat(args, "anchorMinY", rectTransform.anchorMin.y)
                    );
                    modified.Add("anchorMin");
                }
                if (ArgumentParser.HasKey(args, "anchorMaxX") || ArgumentParser.HasKey(args, "anchorMaxY"))
                {
                    rectTransform.anchorMax = new Vector2(
                        ArgumentParser.GetFloat(args, "anchorMaxX", rectTransform.anchorMax.x),
                        ArgumentParser.GetFloat(args, "anchorMaxY", rectTransform.anchorMax.y)
                    );
                    modified.Add("anchorMax");
                }

                if (ArgumentParser.HasKey(args, "pivotX") || ArgumentParser.HasKey(args, "pivotY"))
                {
                    rectTransform.pivot = new Vector2(
                        ArgumentParser.GetFloat(args, "pivotX", rectTransform.pivot.x),
                        ArgumentParser.GetFloat(args, "pivotY", rectTransform.pivot.y)
                    );
                    modified.Add("pivot");
                }

                if (ArgumentParser.HasKey(args, "posX") || ArgumentParser.HasKey(args, "posY"))
                {
                    rectTransform.anchoredPosition = new Vector2(
                        ArgumentParser.GetFloat(args, "posX", rectTransform.anchoredPosition.x),
                        ArgumentParser.GetFloat(args, "posY", rectTransform.anchoredPosition.y)
                    );
                    modified.Add("anchoredPosition");
                }

                if (ArgumentParser.HasKey(args, "width"))
                {
                    rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ArgumentParser.GetFloat(args, "width", 100));
                    modified.Add("width");
                }
                if (ArgumentParser.HasKey(args, "height"))
                {
                    rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ArgumentParser.GetFloat(args, "height", 100));
                    modified.Add("height");
                }

                if (ArgumentParser.HasKey(args, "sizeDeltaX") || ArgumentParser.HasKey(args, "sizeDeltaY"))
                {
                    rectTransform.sizeDelta = new Vector2(
                        ArgumentParser.GetFloat(args, "sizeDeltaX", rectTransform.sizeDelta.x),
                        ArgumentParser.GetFloat(args, "sizeDeltaY", rectTransform.sizeDelta.y)
                    );
                    modified.Add("sizeDelta");
                }

                if (ArgumentParser.HasKey(args, "offsetMinX") || ArgumentParser.HasKey(args, "offsetMinY"))
                {
                    rectTransform.offsetMin = new Vector2(
                        ArgumentParser.GetFloat(args, "offsetMinX", rectTransform.offsetMin.x),
                        ArgumentParser.GetFloat(args, "offsetMinY", rectTransform.offsetMin.y)
                    );
                    modified.Add("offsetMin");
                }
                if (ArgumentParser.HasKey(args, "offsetMaxX") || ArgumentParser.HasKey(args, "offsetMaxY"))
                {
                    rectTransform.offsetMax = new Vector2(
                        ArgumentParser.GetFloat(args, "offsetMaxX", rectTransform.offsetMax.x),
                        ArgumentParser.GetFloat(args, "offsetMaxY", rectTransform.offsetMax.y)
                    );
                    modified.Add("offsetMax");
                }

                EditorUtility.SetDirty(rectTransform);

                return McpResponse.Success($"Modified RectTransform on '{gameObjectPath}'", new
                {
                    gameObject = gameObjectPath,
                    modifiedProperties = modified,
                    current = new
                    {
                        anchorMin = new { x = rectTransform.anchorMin.x, y = rectTransform.anchorMin.y },
                        anchorMax = new { x = rectTransform.anchorMax.x, y = rectTransform.anchorMax.y },
                        pivot = new { x = rectTransform.pivot.x, y = rectTransform.pivot.y },
                        anchoredPosition = new { x = rectTransform.anchoredPosition.x, y = rectTransform.anchoredPosition.y },
                        sizeDelta = new { x = rectTransform.sizeDelta.x, y = rectTransform.sizeDelta.y }
                    }
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to set RectTransform: {ex.Message}");
            }
        }

        private static McpToolResult AddLayoutGroup(Dictionary<string, object> args)
        {
            try
            {
                var (gameObjectPath, pathErr) = RequireArg(args, "gameObjectPath");
                if (pathErr != null) return pathErr;

                var (layoutType, typeErr) = RequireArg(args, "layoutType");
                if (typeErr != null) return typeErr;

                var go = GameObjectHelpers.FindGameObject(gameObjectPath);
                if (go == null)
                    return McpToolResult.Error($"GameObject not found: {gameObjectPath}");

                float spacing = ArgumentParser.GetFloat(args, "spacing", 0);
                int paddingLeft = ArgumentParser.GetInt(args, "paddingLeft", 0);
                int paddingRight = ArgumentParser.GetInt(args, "paddingRight", 0);
                int paddingTop = ArgumentParser.GetInt(args, "paddingTop", 0);
                int paddingBottom = ArgumentParser.GetInt(args, "paddingBottom", 0);
                string alignmentStr = ArgumentParser.GetString(args, "childAlignment", "UpperLeft");
                bool controlWidth = ArgumentParser.GetBool(args, "controlChildWidth", false);
                bool controlHeight = ArgumentParser.GetBool(args, "controlChildHeight", false);
                bool expandWidth = ArgumentParser.GetBool(args, "childForceExpandWidth", false);
                bool expandHeight = ArgumentParser.GetBool(args, "childForceExpandHeight", false);

                TextAnchor alignment = ParseTextAnchor(alignmentStr);

                Component layoutComponent = null;
                string addedType = "";

                switch (layoutType.ToLowerInvariant())
                {
                    case "vertical":
                        // SEC-#441: use Undo.AddComponent for newly added components so undo
                        // properly removes the component; RecordObject only captures state
                        // changes on a pre-existing object.
                        var vLayout = go.GetComponent<VerticalLayoutGroup>()
                                      ?? Undo.AddComponent<VerticalLayoutGroup>(go);
                        Undo.RecordObject(vLayout, "Add Vertical Layout Group");
                        vLayout.spacing = spacing;
                        vLayout.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
                        vLayout.childAlignment = alignment;
                        vLayout.childControlWidth = controlWidth;
                        vLayout.childControlHeight = controlHeight;
                        vLayout.childForceExpandWidth = expandWidth;
                        vLayout.childForceExpandHeight = expandHeight;
                        layoutComponent = vLayout;
                        addedType = "VerticalLayoutGroup";
                        break;

                    case "horizontal":
                        var hLayout = go.GetComponent<HorizontalLayoutGroup>()
                                      ?? Undo.AddComponent<HorizontalLayoutGroup>(go);
                        Undo.RecordObject(hLayout, "Add Horizontal Layout Group");
                        hLayout.spacing = spacing;
                        hLayout.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
                        hLayout.childAlignment = alignment;
                        hLayout.childControlWidth = controlWidth;
                        hLayout.childControlHeight = controlHeight;
                        hLayout.childForceExpandWidth = expandWidth;
                        hLayout.childForceExpandHeight = expandHeight;
                        layoutComponent = hLayout;
                        addedType = "HorizontalLayoutGroup";
                        break;

                    case "grid":
                        var gLayout = go.GetComponent<GridLayoutGroup>()
                                      ?? Undo.AddComponent<GridLayoutGroup>(go);
                        Undo.RecordObject(gLayout, "Add Grid Layout Group");
                        gLayout.spacing = new Vector2(spacing, spacing);
                        gLayout.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
                        gLayout.childAlignment = alignment;
                        gLayout.cellSize = new Vector2(
                            ArgumentParser.GetFloat(args, "cellSizeX", 100),
                            ArgumentParser.GetFloat(args, "cellSizeY", 100)
                        );

                        string constraintStr = ArgumentParser.GetString(args, "constraint", "Flexible");
                        switch (constraintStr.ToLowerInvariant())
                        {
                            case "fixedcolumncount":
                                gLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                                gLayout.constraintCount = ArgumentParser.GetInt(args, "constraintCount", 3);
                                break;
                            case "fixedrowcount":
                                gLayout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
                                gLayout.constraintCount = ArgumentParser.GetInt(args, "constraintCount", 3);
                                break;
                            default:
                                gLayout.constraint = GridLayoutGroup.Constraint.Flexible;
                                break;
                        }
                        layoutComponent = gLayout;
                        addedType = "GridLayoutGroup";
                        break;

                    default:
                        return McpToolResult.Error($"Unknown layout type: {layoutType}. Use 'Vertical', 'Horizontal', or 'Grid'.");
                }

                EditorUtility.SetDirty(go);

                return McpResponse.Success($"Added {addedType} to '{gameObjectPath}'", new
                {
                    gameObject = gameObjectPath,
                    layoutType = addedType,
                    spacing = spacing,
                    padding = new { left = paddingLeft, right = paddingRight, top = paddingTop, bottom = paddingBottom },
                    childAlignment = alignmentStr
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to add layout group: {ex.Message}");
            }
        }

        private static McpToolResult SetCanvasScaler(Dictionary<string, object> args)
        {
            try
            {
                var (canvasPath, canvasPathErr) = RequireArg(args, "canvasPath");
                if (canvasPathErr != null) return canvasPathErr;

                var go = GameObjectHelpers.FindGameObject(canvasPath);
                if (go == null)
                    return McpToolResult.Error($"GameObject not found: {canvasPath}");

                var scaler = go.GetComponent<CanvasScaler>();
                if (scaler == null)
                    return McpToolResult.Error($"'{canvasPath}' does not have a CanvasScaler component");

                Undo.RecordObject(scaler, "Set Canvas Scaler");

                var modified = new List<string>();

                if (ArgumentParser.HasKey(args, "scaleMode"))
                {
                    string mode = ArgumentParser.GetString(args, "scaleMode", "");
                    switch (mode.ToLowerInvariant())
                    {
                        case "constantpixelsize":
                            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                            break;
                        case "scalewithscreensize":
                            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                            break;
                        case "constantphysicalsize":
                            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
                            break;
                        default:
                            // SEC-#436: surface invalid input instead of pretending we modified it.
                            return McpToolResult.Error(
                                $"Unknown scaleMode '{mode}'. Valid: constantpixelsize, scalewithscreensize, constantphysicalsize.");
                    }
                    modified.Add("scaleMode");
                }

                if (ArgumentParser.HasKey(args, "referenceResolutionX") || ArgumentParser.HasKey(args, "referenceResolutionY"))
                {
                    scaler.referenceResolution = new Vector2(
                        ArgumentParser.GetFloat(args, "referenceResolutionX", scaler.referenceResolution.x),
                        ArgumentParser.GetFloat(args, "referenceResolutionY", scaler.referenceResolution.y)
                    );
                    modified.Add("referenceResolution");
                }

                if (ArgumentParser.HasKey(args, "matchWidthOrHeight"))
                {
                    scaler.matchWidthOrHeight = ArgumentParser.GetFloatClamped(args, "matchWidthOrHeight", 0.5f, 0f, 1f);
                    modified.Add("matchWidthOrHeight");
                }

                if (ArgumentParser.HasKey(args, "scaleFactor"))
                {
                    scaler.scaleFactor = ArgumentParser.GetFloat(args, "scaleFactor", 1f);
                    modified.Add("scaleFactor");
                }

                if (ArgumentParser.HasKey(args, "referencePixelsPerUnit"))
                {
                    scaler.referencePixelsPerUnit = ArgumentParser.GetFloat(args, "referencePixelsPerUnit", 100f);
                    modified.Add("referencePixelsPerUnit");
                }

                EditorUtility.SetDirty(scaler);

                return McpResponse.Success($"Configured CanvasScaler on '{canvasPath}'", new
                {
                    canvas = canvasPath,
                    modifiedProperties = modified,
                    current = new
                    {
                        scaleMode = scaler.uiScaleMode.ToString(),
                        referenceResolution = new { x = scaler.referenceResolution.x, y = scaler.referenceResolution.y },
                        matchWidthOrHeight = scaler.matchWidthOrHeight,
                        scaleFactor = scaler.scaleFactor,
                        referencePixelsPerUnit = scaler.referencePixelsPerUnit
                    }
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to set canvas scaler: {ex.Message}");
            }
        }

        private static McpToolResult AddContentSizeFitter(Dictionary<string, object> args)
        {
            try
            {
                var (gameObjectPath, pathErr) = RequireArg(args, "gameObjectPath");
                if (pathErr != null) return pathErr;

                var go = GameObjectHelpers.FindGameObject(gameObjectPath);
                if (go == null)
                    return McpToolResult.Error($"GameObject not found: {gameObjectPath}");

                var fitter = go.GetComponent<ContentSizeFitter>() ?? go.AddComponent<ContentSizeFitter>();
                Undo.RecordObject(fitter, "Add ContentSizeFitter");

                string hFit = ArgumentParser.GetString(args, "horizontalFit", "Unconstrained");
                string vFit = ArgumentParser.GetString(args, "verticalFit", "Unconstrained");

                fitter.horizontalFit = ParseFitMode(hFit);
                fitter.verticalFit = ParseFitMode(vFit);

                EditorUtility.SetDirty(go);

                return McpResponse.Success($"Added ContentSizeFitter to '{gameObjectPath}'", new
                {
                    gameObject = gameObjectPath,
                    horizontalFit = fitter.horizontalFit.ToString(),
                    verticalFit = fitter.verticalFit.ToString()
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to add ContentSizeFitter: {ex.Message}");
            }
        }

        private static McpToolResult AddLayoutElement(Dictionary<string, object> args)
        {
            try
            {
                var (gameObjectPath, pathErr) = RequireArg(args, "gameObjectPath");
                if (pathErr != null) return pathErr;

                var go = GameObjectHelpers.FindGameObject(gameObjectPath);
                if (go == null)
                    return McpToolResult.Error($"GameObject not found: {gameObjectPath}");

                var layoutElement = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
                Undo.RecordObject(layoutElement, "Add LayoutElement");

                var modified = new List<string>();

                if (ArgumentParser.HasKey(args, "minWidth"))
                {
                    layoutElement.minWidth = ArgumentParser.GetFloat(args, "minWidth", -1);
                    modified.Add("minWidth");
                }
                if (ArgumentParser.HasKey(args, "minHeight"))
                {
                    layoutElement.minHeight = ArgumentParser.GetFloat(args, "minHeight", -1);
                    modified.Add("minHeight");
                }
                if (ArgumentParser.HasKey(args, "preferredWidth"))
                {
                    layoutElement.preferredWidth = ArgumentParser.GetFloat(args, "preferredWidth", -1);
                    modified.Add("preferredWidth");
                }
                if (ArgumentParser.HasKey(args, "preferredHeight"))
                {
                    layoutElement.preferredHeight = ArgumentParser.GetFloat(args, "preferredHeight", -1);
                    modified.Add("preferredHeight");
                }
                if (ArgumentParser.HasKey(args, "flexibleWidth"))
                {
                    layoutElement.flexibleWidth = ArgumentParser.GetFloat(args, "flexibleWidth", -1);
                    modified.Add("flexibleWidth");
                }
                if (ArgumentParser.HasKey(args, "flexibleHeight"))
                {
                    layoutElement.flexibleHeight = ArgumentParser.GetFloat(args, "flexibleHeight", -1);
                    modified.Add("flexibleHeight");
                }
                if (ArgumentParser.HasKey(args, "ignoreLayout"))
                {
                    layoutElement.ignoreLayout = ArgumentParser.GetBool(args, "ignoreLayout", false);
                    modified.Add("ignoreLayout");
                }

                EditorUtility.SetDirty(go);

                return McpResponse.Success($"Added LayoutElement to '{gameObjectPath}'", new
                {
                    gameObject = gameObjectPath,
                    modifiedProperties = modified
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to add LayoutElement: {ex.Message}");
            }
        }

        #endregion

        #region Layout Helper Methods

        private static ContentSizeFitter.FitMode ParseFitMode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return ContentSizeFitter.FitMode.Unconstrained;

            switch (value.ToLowerInvariant())
            {
                case "minsize": return ContentSizeFitter.FitMode.MinSize;
                case "preferredsize": return ContentSizeFitter.FitMode.PreferredSize;
                default: return ContentSizeFitter.FitMode.Unconstrained;
            }
        }

        // SEC-#436: returns false on unknown preset so the caller can surface a clear error
        // instead of silently succeeding without applying any anchoring.
        private static bool ApplyAnchorPreset(RectTransform rt, string preset)
        {
            switch (preset.ToLowerInvariant())
            {
                case "topleft":
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                    return true;
                case "topcenter":
                case "top":
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
                    return true;
                case "topright":
                    rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
                    return true;
                case "middleleft":
                case "left":
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
                    return true;
                case "middlecenter":
                case "center":
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    return true;
                case "middleright":
                case "right":
                    rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
                    return true;
                case "bottomleft":
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                    return true;
                case "bottomcenter":
                case "bottom":
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
                    return true;
                case "bottomright":
                    rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
                    return true;
                case "stretchhorizontal":
                    rt.anchorMin = new Vector2(0, 0.5f);
                    rt.anchorMax = new Vector2(1, 0.5f);
                    return true;
                case "stretchvertical":
                    rt.anchorMin = new Vector2(0.5f, 0);
                    rt.anchorMax = new Vector2(0.5f, 1);
                    return true;
                case "stretchall":
                case "stretch":
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    return true;
                default:
                    return false;
            }
        }

        private static TextAnchor ParseTextAnchor(string value)
        {
            if (string.IsNullOrEmpty(value))
                return TextAnchor.UpperLeft;

            switch (value.ToLowerInvariant())
            {
                case "upperleft": return TextAnchor.UpperLeft;
                case "uppercenter": return TextAnchor.UpperCenter;
                case "upperright": return TextAnchor.UpperRight;
                case "middleleft": return TextAnchor.MiddleLeft;
                case "middlecenter": return TextAnchor.MiddleCenter;
                case "middleright": return TextAnchor.MiddleRight;
                case "lowerleft": return TextAnchor.LowerLeft;
                case "lowercenter": return TextAnchor.LowerCenter;
                case "lowerright": return TextAnchor.LowerRight;
                default: return TextAnchor.UpperLeft;
            }
        }

        #endregion
    }
}
