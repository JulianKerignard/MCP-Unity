using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using McpUnity.Protocol;
using McpUnity.Helpers;

namespace McpUnity.Server
{
    /// <summary>
    /// UI Creation Tools - Canvas creation, UI element creation, UI hierarchy inspection
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register UI creation tools: create_canvas, create_ui_element, get_ui_hierarchy
        /// </summary>
        static partial void RegisterUICreationTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_canvas",
                description = "Create a UI Canvas with EventSystem. Canvas is required for all UI elements. Supports ScreenSpaceOverlay (default), ScreenSpaceCamera, and WorldSpace render modes.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["name"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Canvas name (default: 'Canvas')"
                        },
                        ["renderMode"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Render mode: 'ScreenSpaceOverlay' (default), 'ScreenSpaceCamera', or 'WorldSpace'"
                        },
                        ["createEventSystem"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Create EventSystem if none exists (default: true)"
                        },
                        ["scaleMode"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "CanvasScaler mode: 'ConstantPixelSize' (default), 'ScaleWithScreenSize', 'ConstantPhysicalSize'"
                        },
                        ["referenceResolutionX"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Reference resolution width for ScaleWithScreenSize (default: 1920)"
                        },
                        ["referenceResolutionY"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Reference resolution height for ScaleWithScreenSize (default: 1080)"
                        },
                        ["matchWidthOrHeight"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Match width (0) or height (1) for ScaleWithScreenSize (default: 0.5)"
                        }
                    },
                    required = new List<string>()
                }
            }, CreateCanvas);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_ui_element",
                description = "Create a UI element with RectTransform (Button, Text, Image, Panel, Slider, Toggle, InputField, Dropdown, ScrollView). ALWAYS use this for UI - NOT unity_create_gameobject. Auto-parents to Canvas.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["elementType"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "UI element type: Panel, Button, Text, Image, RawImage, Slider, Toggle, InputField, Dropdown, ScrollView"
                        },
                        ["name"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Element name (default: same as elementType)"
                        },
                        ["parent"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Parent path (default: first Canvas in scene)"
                        },
                        ["text"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Text content for Button, Text, Toggle, InputField elements"
                        },
                        ["posX"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "X position (anchored position)"
                        },
                        ["posY"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Y position (anchored position)"
                        },
                        ["width"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Element width"
                        },
                        ["height"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Element height"
                        },
                        ["color"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Color in hex format (#RGB, #RRGGBB, #RRGGBBAA) or named color (red, blue, white, etc.)"
                        },
                        ["sprite"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Sprite asset path for Image elements (e.g., 'Assets/Sprites/icon.png')"
                        },
                        ["fontSize"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Font size for Text, Button, InputField elements (default: 14)"
                        }
                    },
                    required = new List<string> { "elementType" }
                }
            }, CreateUIElement);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_ui_hierarchy",
                description = "Get the UI hierarchy of all Canvas elements in the scene, showing element types, positions, and sizes.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["canvasPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Optional: specific Canvas path to inspect (default: all canvases)"
                        }
                    },
                    required = new List<string>()
                }
            }, GetUIHierarchy);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_modify_ui_element",
                description = "Modify properties of an existing UI element (text, color, fontSize, interactable, value, sprite). Works on Button, Text, Image, Slider, Toggle, InputField, Dropdown.",
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
                        ["text"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Text content (for Text, Button label, InputField, Toggle label)"
                        },
                        ["color"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Color in hex (#RRGGBB) or named color. Applies to Image or Text depending on element type."
                        },
                        ["fontSize"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Font size for text elements"
                        },
                        ["interactable"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Enable/disable interaction (Button, Slider, Toggle, InputField, Dropdown)"
                        },
                        ["value"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Value for Slider (0-1), Toggle (0=off, 1=on), Dropdown (option index)"
                        },
                        ["sprite"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Sprite asset path for Image elements"
                        },
                        ["placeholder"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Placeholder text for InputField"
                        },
                        ["options"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Comma-separated options for Dropdown (e.g., 'Option A,Option B,Option C')"
                        }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, ModifyUIElement);
        }

        #region UI Creation Handlers

        private static McpToolResult CreateCanvas(Dictionary<string, object> args)
        {
            try
            {
                string canvasName = ArgumentParser.GetString(args, "name", "Canvas");
                string renderMode = ArgumentParser.GetString(args, "renderMode", "ScreenSpaceOverlay");
                bool createEventSystem = ArgumentParser.GetBool(args, "createEventSystem", true);

                var canvasGO = new GameObject(canvasName);
                Undo.RegisterCreatedObjectUndo(canvasGO, $"Create Canvas '{canvasName}'");

                var canvas = canvasGO.AddComponent<Canvas>();

                switch (renderMode.ToLowerInvariant())
                {
                    case "screenspaceoverlay":
                    case "overlay":
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        break;
                    case "screenspacecamera":
                    case "camera":
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        break;
                    case "worldspace":
                    case "world":
                        canvas.renderMode = RenderMode.WorldSpace;
                        var rectTransform = canvasGO.GetComponent<RectTransform>();
                        rectTransform.sizeDelta = new Vector2(800, 600);
                        break;
                    default:
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        break;
                }

                var scaler = canvasGO.AddComponent<CanvasScaler>();

                string scaleMode = ArgumentParser.GetString(args, "scaleMode", "ConstantPixelSize");
                switch (scaleMode.ToLowerInvariant())
                {
                    case "scalewithscreensize":
                    case "screensize":
                        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        scaler.referenceResolution = new Vector2(
                            ArgumentParser.GetFloat(args, "referenceResolutionX", 1920),
                            ArgumentParser.GetFloat(args, "referenceResolutionY", 1080)
                        );
                        scaler.matchWidthOrHeight = ArgumentParser.GetFloat(args, "matchWidthOrHeight", 0.5f);
                        break;
                    case "constantphysicalsize":
                    case "physicalsize":
                        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
                        break;
                    default:
                        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                        break;
                }

                canvasGO.AddComponent<GraphicRaycaster>();

                if (createEventSystem && UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
                {
                    var eventSystemGO = new GameObject("EventSystem");
                    Undo.RegisterCreatedObjectUndo(eventSystemGO, "Create EventSystem");
                    eventSystemGO.AddComponent<EventSystem>();
                    eventSystemGO.AddComponent<StandaloneInputModule>();
                }

                return McpResponse.Success($"Created Canvas '{canvasName}'", new
                {
                    name = canvasName,
                    renderMode = canvas.renderMode.ToString(),
                    hasEventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to create Canvas: {ex.Message}");
            }
        }

        private static McpToolResult CreateUIElement(Dictionary<string, object> args)
        {
            try
            {
                var (elementType, typeErr) = RequireArg(args, "elementType");
                if (typeErr != null) return typeErr;

                string elementName = ArgumentParser.GetString(args, "name", elementType);
                string parentPath = ArgumentParser.GetString(args, "parent", null);
                string text = ArgumentParser.GetString(args, "text", null);
                string colorStr = ArgumentParser.GetString(args, "color", null);
                string spritePath = ArgumentParser.GetString(args, "sprite", null);
                int fontSize = ArgumentParser.GetInt(args, "fontSize", 14);

                Transform parent = null;
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parentGO = GameObjectHelpers.FindGameObject(parentPath);
                    if (parentGO == null)
                        return McpToolResult.Error($"Parent not found: {parentPath}");
                    parent = parentGO.transform;
                }
                else
                {
                    var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                    if (canvas == null)
                        return McpToolResult.Error("No Canvas found in scene. Create one first with unity_create_canvas.");
                    parent = canvas.transform;
                }

                GameObject uiElement = null;
                string createdType = elementType.ToLowerInvariant();

                switch (createdType)
                {
                    case "panel":
                        uiElement = CreateUIPanel(elementName, parent);
                        break;
                    case "button":
                        uiElement = CreateUIButton(elementName, parent, text ?? "Button");
                        break;
                    case "text":
                    case "label":
                        uiElement = CreateUIText(elementName, parent, text ?? "New Text");
                        break;
                    case "image":
                        uiElement = CreateUIImage(elementName, parent);
                        break;
                    case "rawimage":
                        uiElement = CreateUIRawImage(elementName, parent);
                        break;
                    case "slider":
                        uiElement = CreateUISlider(elementName, parent);
                        break;
                    case "toggle":
                    case "checkbox":
                        uiElement = CreateUIToggle(elementName, parent, text ?? "Toggle");
                        break;
                    case "inputfield":
                    case "input":
                        uiElement = CreateUIInputField(elementName, parent, text ?? "Enter text...");
                        break;
                    case "dropdown":
                        uiElement = CreateUIDropdown(elementName, parent);
                        break;
                    case "scrollview":
                        uiElement = CreateUIScrollView(elementName, parent);
                        break;
                    default:
                        return McpToolResult.Error($"Unknown UI element type: {elementType}. Valid types: Panel, Button, Text, Image, RawImage, Slider, Toggle, InputField, Dropdown, ScrollView");
                }

                if (uiElement == null)
                    return McpToolResult.Error($"Failed to create UI element: {elementType}");

                Undo.RegisterCreatedObjectUndo(uiElement, $"Create UI {elementType}");

                var posX = ArgumentParser.GetFloat(args, "posX", 0);
                var posY = ArgumentParser.GetFloat(args, "posY", 0);
                var width = ArgumentParser.GetFloat(args, "width", 0);
                var height = ArgumentParser.GetFloat(args, "height", 0);

                var rect = uiElement.GetComponent<RectTransform>();
                if (rect != null)
                {
                    if (posX != 0 || posY != 0)
                        rect.anchoredPosition = new Vector2(posX, posY);
                    if (width > 0)
                        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                    if (height > 0)
                        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                }

                if (!string.IsNullOrEmpty(colorStr))
                {
                    var color = ColorParser.Parse(colorStr, Color.white);

                    var image = uiElement.GetComponent<Image>();
                    if (image != null)
                        image.color = color;

                    var textComp = uiElement.GetComponent<TMP_Text>() ?? uiElement.GetComponentInChildren<TMP_Text>();
                    if (textComp != null && createdType != "button")
                        textComp.color = color;
                }

                if (!string.IsNullOrEmpty(spritePath))
                {
                    var image = uiElement.GetComponent<Image>();
                    if (image != null)
                    {
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                        if (sprite != null)
                            image.sprite = sprite;
                    }
                }

                if (ArgumentParser.HasKey(args, "fontSize"))
                {
                    var textComp = uiElement.GetComponent<TMP_Text>() ?? uiElement.GetComponentInChildren<TMP_Text>();
                    if (textComp != null)
                        textComp.fontSize = fontSize;
                }

                return McpResponse.Success($"Created UI {elementType} '{elementName}'", new
                {
                    name = elementName,
                    type = createdType,
                    path = GameObjectHelpers.GetGameObjectPath(uiElement),
                    parent = parent.name,
                    color = colorStr,
                    sprite = spritePath,
                    fontSize = ArgumentParser.HasKey(args, "fontSize") ? fontSize : (int?)null
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to create UI element: {ex.Message}");
            }
        }

        private static McpToolResult GetUIHierarchy(Dictionary<string, object> args)
        {
            try
            {
                string canvasPath = ArgumentParser.GetString(args, "canvasPath", null);

                Canvas targetCanvas = null;
                if (!string.IsNullOrEmpty(canvasPath))
                {
                    var canvasGO = GameObjectHelpers.FindGameObject(canvasPath);
                    if (canvasGO != null)
                        targetCanvas = canvasGO.GetComponent<Canvas>();
                }

                var canvases = targetCanvas != null
                    ? new Canvas[] { targetCanvas }
                    : UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

                var result = new List<object>();
                foreach (var canvas in canvases)
                {
                    result.Add(new
                    {
                        name = canvas.gameObject.name,
                        path = GameObjectHelpers.GetGameObjectPath(canvas.gameObject),
                        renderMode = canvas.renderMode.ToString(),
                        sortingOrder = canvas.sortingOrder,
                        children = GetUIChildrenInfo(canvas.transform)
                    });
                }

                return McpResponse.Success("UI Hierarchy", new { canvases = result, count = canvases.Length });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to get UI hierarchy: {ex.Message}");
            }
        }

        #endregion

        #region UI Modify Handler

        private static McpToolResult ModifyUIElement(Dictionary<string, object> args)
        {
            try
            {
                var (go, gameObjectPath, goErr) = RequireGameObject(args);
                if (goErr != null) return goErr;

                var modified = new List<string>();

                // Text modification (TMP_Text on self or first child)
                if (ArgumentParser.HasKey(args, "text"))
                {
                    string text = ArgumentParser.GetString(args, "text", "");
                    var textComp = go.GetComponent<TMP_Text>() ?? go.GetComponentInChildren<TMP_Text>();
                    if (textComp != null)
                    {
                        Undo.RecordObject(textComp, "Modify UI Text");
                        textComp.text = text;
                        EditorUtility.SetDirty(textComp);
                        modified.Add("text");
                    }
                }

                // Color modification
                if (ArgumentParser.HasKey(args, "color"))
                {
                    string colorStr = ArgumentParser.GetString(args, "color", "");
                    var color = ColorParser.Parse(colorStr, Color.white);

                    // Apply to Image if present
                    var image = go.GetComponent<Image>();
                    if (image != null)
                    {
                        Undo.RecordObject(image, "Modify UI Color");
                        image.color = color;
                        EditorUtility.SetDirty(image);
                        modified.Add("imageColor");
                    }

                    // Apply to text if no Image or if it's a Text element
                    var textComp = go.GetComponent<TMP_Text>();
                    if (textComp != null && image == null)
                    {
                        Undo.RecordObject(textComp, "Modify UI Text Color");
                        textComp.color = color;
                        EditorUtility.SetDirty(textComp);
                        modified.Add("textColor");
                    }
                }

                // Font size
                if (ArgumentParser.HasKey(args, "fontSize"))
                {
                    int fontSize = ArgumentParser.GetInt(args, "fontSize", 14);
                    var textComp = go.GetComponent<TMP_Text>() ?? go.GetComponentInChildren<TMP_Text>();
                    if (textComp != null)
                    {
                        Undo.RecordObject(textComp, "Modify UI FontSize");
                        textComp.fontSize = fontSize;
                        EditorUtility.SetDirty(textComp);
                        modified.Add("fontSize");
                    }
                }

                // Interactable
                if (ArgumentParser.HasKey(args, "interactable"))
                {
                    bool interactable = ArgumentParser.GetBool(args, "interactable", true);
                    var selectable = go.GetComponent<UnityEngine.UI.Selectable>();
                    if (selectable != null)
                    {
                        Undo.RecordObject(selectable, "Modify UI Interactable");
                        selectable.interactable = interactable;
                        EditorUtility.SetDirty(selectable);
                        modified.Add("interactable");
                    }
                }

                // Value (Slider, Toggle, Dropdown)
                if (ArgumentParser.HasKey(args, "value"))
                {
                    float value = ArgumentParser.GetFloat(args, "value", 0);

                    var slider = go.GetComponent<Slider>();
                    if (slider != null)
                    {
                        Undo.RecordObject(slider, "Modify Slider Value");
                        slider.value = value;
                        EditorUtility.SetDirty(slider);
                        modified.Add("sliderValue");
                    }

                    var toggle = go.GetComponent<Toggle>();
                    if (toggle != null)
                    {
                        Undo.RecordObject(toggle, "Modify Toggle Value");
                        toggle.isOn = value > 0.5f;
                        EditorUtility.SetDirty(toggle);
                        modified.Add("toggleValue");
                    }

                    var dropdown = go.GetComponent<TMP_Dropdown>();
                    if (dropdown != null)
                    {
                        Undo.RecordObject(dropdown, "Modify Dropdown Value");
                        dropdown.value = (int)value;
                        EditorUtility.SetDirty(dropdown);
                        modified.Add("dropdownValue");
                    }
                }

                // Sprite
                if (ArgumentParser.HasKey(args, "sprite"))
                {
                    string spritePath = ArgumentParser.GetString(args, "sprite", "");
                    var image = go.GetComponent<Image>();
                    if (image != null && !string.IsNullOrEmpty(spritePath))
                    {
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                        if (sprite != null)
                        {
                            Undo.RecordObject(image, "Modify UI Sprite");
                            image.sprite = sprite;
                            EditorUtility.SetDirty(image);
                            modified.Add("sprite");
                        }
                    }
                }

                // Placeholder (InputField)
                if (ArgumentParser.HasKey(args, "placeholder"))
                {
                    string placeholder = ArgumentParser.GetString(args, "placeholder", "");
                    var inputField = go.GetComponent<TMP_InputField>();
                    if (inputField != null && inputField.placeholder is TMP_Text phText)
                    {
                        Undo.RecordObject(phText, "Modify InputField Placeholder");
                        phText.text = placeholder;
                        EditorUtility.SetDirty(phText);
                        modified.Add("placeholder");
                    }
                }

                // Dropdown options
                if (ArgumentParser.HasKey(args, "options"))
                {
                    string optionsStr = ArgumentParser.GetString(args, "options", "");
                    var dropdown = go.GetComponent<TMP_Dropdown>();
                    if (dropdown != null && !string.IsNullOrEmpty(optionsStr))
                    {
                        Undo.RecordObject(dropdown, "Modify Dropdown Options");
                        dropdown.ClearOptions();
                        var optionsList = new List<TMP_Dropdown.OptionData>();
                        foreach (var opt in optionsStr.Split(','))
                            optionsList.Add(new TMP_Dropdown.OptionData(opt.Trim()));
                        dropdown.AddOptions(optionsList);
                        dropdown.RefreshShownValue();
                        EditorUtility.SetDirty(dropdown);
                        modified.Add("options");
                    }
                }

                if (modified.Count == 0)
                    return McpToolResult.Error("No modifiable UI properties found on this element, or no valid parameters provided.");

                return McpResponse.Success($"Modified UI element '{gameObjectPath}'", new
                {
                    gameObject = gameObjectPath,
                    modifiedProperties = modified
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to modify UI element: {ex.Message}");
            }
        }

        #endregion

        #region UI Element Factory Methods

        private static GameObject CreateUIPanel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(1, 1, 1, 0.4f);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 200);
            return go;
        }

        private static GameObject CreateUIButton(string name, Transform parent, string buttonText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var bgImage = go.GetComponent<Image>();
            bgImage.color = Color.white;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 30);

            // Assign targetGraphic so Button has visual feedback (hover/press color transitions)
            var button = go.GetComponent<Button>();
            button.targetGraphic = bgImage;

            var textGO = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.GetComponent<TextMeshProUGUI>();
            text.text = buttonText;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.black;
            text.fontSize = 14;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return go;
        }

        private static GameObject CreateUIText(string name, Transform parent, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.black;
            text.fontSize = 14;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 30);
            return go;
        }

        private static GameObject CreateUIImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
            return go;
        }

        private static GameObject CreateUIRawImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
            return go;
        }

        private static GameObject CreateUISlider(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 20);

            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(go.transform, false);
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.sizeDelta = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-15, 0);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.sizeDelta = new Vector2(10, 0);
            fill.GetComponent<Image>().color = new Color(0.3f, 0.6f, 1f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 0);
            handle.GetComponent<Image>().color = Color.white;

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            // Assign targetGraphic so handle has visual feedback on drag
            slider.targetGraphic = handle.GetComponent<Image>();

            return go;
        }

        private static GameObject CreateUIToggle(string name, Transform parent, string labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 20);

            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(go.transform, false);
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 1);
            bgRect.anchorMax = new Vector2(0, 1);
            bgRect.anchoredPosition = new Vector2(10, -10);
            bgRect.sizeDelta = new Vector2(20, 20);
            background.GetComponent<Image>().color = Color.white;

            var checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            checkmark.transform.SetParent(background.transform, false);
            var checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.sizeDelta = new Vector2(-4, -4);
            checkmark.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(23, 1);
            labelRect.offsetMax = new Vector2(-5, -2);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.text = labelText;
            text.color = Color.black;
            text.fontSize = 14;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = background.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();

            return go;
        }

        private static GameObject CreateUIInputField(string name, Transform parent, string placeholder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 30);
            go.GetComponent<Image>().color = Color.white;

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(go.transform, false);
            var textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 6);
            textAreaRect.offsetMax = new Vector2(-10, -7);

            var placeholderGO = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderGO.transform.SetParent(textArea.transform, false);
            var phRect = placeholderGO.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = Vector2.zero;
            var phText = placeholderGO.GetComponent<TextMeshProUGUI>();
            phText.text = placeholder;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            phText.fontSize = 14;

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(textArea.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var text = textGO.GetComponent<TextMeshProUGUI>();
            text.color = Color.black;
            text.fontSize = 14;
            text.richText = false;

            var inputField = go.GetComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = phText;
            // Assign targetGraphic so InputField has visual feedback on focus
            inputField.targetGraphic = go.GetComponent<Image>();

            return go;
        }

        private static GameObject CreateUIDropdown(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_Dropdown));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 30);
            var bgImage = go.GetComponent<Image>();
            bgImage.color = Color.white;

            // Label (shows selected option text)
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 6);
            labelRect.offsetMax = new Vector2(-25, -7);
            var labelText = label.GetComponent<TextMeshProUGUI>();
            labelText.color = Color.black;
            labelText.fontSize = 14;
            labelText.alignment = TextAlignmentOptions.Left;

            // Arrow indicator
            var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            arrow.transform.SetParent(go.transform, false);
            var arrowRect = arrow.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.sizeDelta = new Vector2(20, 20);
            arrowRect.anchoredPosition = new Vector2(-15, 0);
            arrow.GetComponent<Image>().color = Color.black;

            // === Template (required for dropdown popup to work) ===
            var template = new GameObject("Template", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(go.transform, false);
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(0, 150);
            template.GetComponent<Image>().color = new Color(0.96f, 0.96f, 0.96f);

            // Viewport inside template
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            var vpRect = viewport.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = new Vector2(-18, 0);
            vpRect.pivot = new Vector2(0, 1);
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            // Content inside viewport
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 28);

            // Item template inside content
            var item = new GameObject("Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            var itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 28);
            var itemBg = item.GetComponent<Image>();
            itemBg.color = new Color(0.96f, 0.96f, 0.96f);

            // Item background (highlight on hover)
            var itemBackground = new GameObject("Item Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            itemBackground.transform.SetParent(item.transform, false);
            var itemBgRect = itemBackground.GetComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.sizeDelta = Vector2.zero;
            itemBackground.GetComponent<Image>().color = new Color(0.82f, 0.88f, 0.96f);

            // Item checkmark
            var itemCheckmark = new GameObject("Item Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            itemCheckmark.transform.SetParent(item.transform, false);
            var checkRect = itemCheckmark.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0, 0.5f);
            checkRect.anchorMax = new Vector2(0, 0.5f);
            checkRect.sizeDelta = new Vector2(20, 20);
            checkRect.anchoredPosition = new Vector2(10, 0);
            itemCheckmark.GetComponent<Image>().color = Color.black;

            // Item label
            var itemLabel = new GameObject("Item Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            itemLabel.transform.SetParent(item.transform, false);
            var itemLabelRect = itemLabel.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(20, 1);
            itemLabelRect.offsetMax = new Vector2(-10, -2);
            var itemLabelText = itemLabel.GetComponent<TextMeshProUGUI>();
            itemLabelText.color = Color.black;
            itemLabelText.fontSize = 14;
            itemLabelText.alignment = TextAlignmentOptions.Left;

            // Configure item Toggle
            var itemToggle = item.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBg;
            itemToggle.graphic = itemBackground.GetComponent<Image>();
            itemToggle.isOn = true;

            // Scrollbar for template
            var scrollbar = new GameObject("Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            scrollbar.transform.SetParent(template.transform, false);
            var scrollbarRect = scrollbar.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.pivot = new Vector2(1, 1);
            scrollbarRect.sizeDelta = new Vector2(20, 0);
            scrollbarRect.anchoredPosition = Vector2.zero;
            scrollbar.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f);

            var slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
            slidingArea.transform.SetParent(scrollbar.transform, false);
            var slidingRect = slidingArea.GetComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.offsetMin = new Vector2(10, 10);
            slidingRect.offsetMax = new Vector2(-10, -10);

            var scrollHandle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            scrollHandle.transform.SetParent(slidingArea.transform, false);
            var handleRect = scrollHandle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 20);
            scrollHandle.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f);

            var scrollbarComp = scrollbar.GetComponent<Scrollbar>();
            scrollbarComp.handleRect = handleRect;
            scrollbarComp.targetGraphic = scrollHandle.GetComponent<Image>();
            scrollbarComp.direction = Scrollbar.Direction.BottomToTop;

            // Wire up ScrollRect
            var scrollRect = template.GetComponent<ScrollRect>();
            scrollRect.viewport = vpRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.verticalScrollbar = scrollbarComp;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = -3;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Template starts hidden (TMP_Dropdown activates it on click)
            template.SetActive(false);

            // === Configure the TMP_Dropdown ===
            var dropdown = go.GetComponent<TMP_Dropdown>();
            dropdown.targetGraphic = bgImage;
            dropdown.template = templateRect;
            dropdown.captionText = labelText;
            dropdown.itemText = itemLabelText;
            dropdown.options.Add(new TMP_Dropdown.OptionData("Option A"));
            dropdown.options.Add(new TMP_Dropdown.OptionData("Option B"));
            dropdown.options.Add(new TMP_Dropdown.OptionData("Option C"));
            dropdown.RefreshShownValue();

            return go;
        }

        private static GameObject CreateUIScrollView(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 200);
            go.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.5f);

            // Viewport
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(go.transform, false);
            var vpRect = viewport.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = new Vector2(-17, 0);
            vpRect.pivot = new Vector2(0, 1);
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            // Content
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.sizeDelta = new Vector2(0, 300);

            // Vertical Scrollbar
            var scrollbarGO = new GameObject("Scrollbar Vertical", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            scrollbarGO.transform.SetParent(go.transform, false);
            var scrollbarRect = scrollbarGO.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = Vector2.one;
            scrollbarRect.pivot = new Vector2(1, 1);
            scrollbarRect.sizeDelta = new Vector2(17, 0);
            scrollbarRect.anchoredPosition = Vector2.zero;
            scrollbarGO.GetComponent<Image>().color = new Color(0.78f, 0.78f, 0.78f);

            var slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
            slidingArea.transform.SetParent(scrollbarGO.transform, false);
            var slidingRect = slidingArea.GetComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.offsetMin = new Vector2(10, 10);
            slidingRect.offsetMax = new Vector2(-10, -10);

            var scrollHandle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            scrollHandle.transform.SetParent(slidingArea.transform, false);
            var handleRect = scrollHandle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 20);
            scrollHandle.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f);

            var scrollbar = scrollbarGO.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = scrollHandle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            // Wire up ScrollRect
            var scrollRect = go.GetComponent<ScrollRect>();
            scrollRect.viewport = vpRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = -3;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            return go;
        }

        #endregion

        #region UI Hierarchy Helpers

        private static List<object> GetUIChildrenInfo(Transform parent, int depth = 0, int maxDepth = 10)
        {
            var children = new List<object>();
            if (depth >= maxDepth) return children;
            foreach (Transform child in parent)
            {
                var rect = child.GetComponent<RectTransform>();
                var uiComponents = new List<string>();

                if (child.GetComponent<Button>()) uiComponents.Add("Button");
                if (child.GetComponent<TMP_Text>()) uiComponents.Add("Text (TMP)");
                else if (child.GetComponent<Text>()) uiComponents.Add("Text (Legacy)");
                if (child.GetComponent<Image>()) uiComponents.Add("Image");
                if (child.GetComponent<RawImage>()) uiComponents.Add("RawImage");
                if (child.GetComponent<Slider>()) uiComponents.Add("Slider");
                if (child.GetComponent<Toggle>()) uiComponents.Add("Toggle");
                if (child.GetComponent<TMP_InputField>()) uiComponents.Add("InputField (TMP)");
                else if (child.GetComponent<InputField>()) uiComponents.Add("InputField (Legacy)");
                if (child.GetComponent<TMP_Dropdown>()) uiComponents.Add("Dropdown (TMP)");
                else if (child.GetComponent<Dropdown>()) uiComponents.Add("Dropdown (Legacy)");
                if (child.GetComponent<ScrollRect>()) uiComponents.Add("ScrollRect");

                children.Add(new
                {
                    name = child.name,
                    uiType = uiComponents.Count > 0 ? string.Join(", ", uiComponents) : "Container",
                    position = rect != null ? new { x = rect.anchoredPosition.x, y = rect.anchoredPosition.y } : null,
                    size = rect != null ? new { x = rect.sizeDelta.x, y = rect.sizeDelta.y } : null,
                    children = GetUIChildrenInfo(child, depth + 1, maxDepth)
                });
            }
            return children;
        }

        #endregion
    }
}
