using System.Collections.Generic;
using NUnit.Framework;
using McpUnity.Protocol;

namespace McpUnity.Tests
{
    /// <summary>
    /// Edit Mode tests for MCP Protocol types — JSON-RPC structures and MCP content blocks.
    /// </summary>
    public class McpProtocolTests
    {
        // ── JsonRpcRequest ─────────────────────────────────────────────────

        [Test]
        public void JsonRpcRequest_DefaultValues_AreCorrect()
        {
            var req = new JsonRpcRequest();
            Assert.AreEqual("2.0", req.jsonrpc);
            Assert.IsNull(req.id);
            Assert.IsNull(req.method);
            Assert.IsNull(req.@params);
        }

        [Test]
        public void JsonRpcRequest_GetParams_NullParams_ReturnsNull()
        {
            var req = new JsonRpcRequest { @params = null };
            var result = req.GetParams<ToolCallParams>();
            Assert.IsNull(result);
        }

        [Test]
        public void JsonRpcRequest_GetParams_CorrectType_ReturnsCast()
        {
            var toolParams = new ToolCallParams { name = "test_tool" };
            var req = new JsonRpcRequest { @params = toolParams };
            var result = req.GetParams<ToolCallParams>();
            Assert.IsNotNull(result);
            Assert.AreEqual("test_tool", result.name);
        }

        // ── JsonRpcResponse ────────────────────────────────────────────────

        [Test]
        public void JsonRpcResponse_DefaultValues_AreCorrect()
        {
            var resp = new JsonRpcResponse();
            Assert.AreEqual("2.0", resp.jsonrpc);
            Assert.IsNull(resp.id);
            Assert.IsNull(resp.result);
            Assert.IsNull(resp.error);
        }

        [Test]
        public void JsonRpcResponse_Success_SetsIdAndResult()
        {
            var resp = JsonRpcResponse.Success(42, "ok");
            Assert.AreEqual("2.0", resp.jsonrpc);
            Assert.AreEqual(42, resp.id);
            Assert.AreEqual("ok", resp.result);
            Assert.IsNull(resp.error);
        }

        [Test]
        public void JsonRpcResponse_Success_WithStringId()
        {
            var resp = JsonRpcResponse.Success("req-123", new { status = "done" });
            Assert.AreEqual("req-123", resp.id);
            Assert.IsNotNull(resp.result);
        }

        [Test]
        public void JsonRpcResponse_Success_NullResult_IsValid()
        {
            var resp = JsonRpcResponse.Success(1, null);
            Assert.AreEqual(1, resp.id);
            Assert.IsNull(resp.result);
            Assert.IsNull(resp.error);
        }

        [Test]
        public void JsonRpcResponse_Error_SetsIdAndErrorFields()
        {
            var resp = JsonRpcResponse.Error(5, JsonRpcError.MethodNotFound, "No such method");
            Assert.AreEqual("2.0", resp.jsonrpc);
            Assert.AreEqual(5, resp.id);
            Assert.IsNull(resp.result);
            Assert.IsNotNull(resp.error);
            Assert.AreEqual(JsonRpcError.MethodNotFound, resp.error.code);
            Assert.AreEqual("No such method", resp.error.message);
            Assert.IsNull(resp.error.data);
        }

        [Test]
        public void JsonRpcResponse_Error_WithData()
        {
            var resp = JsonRpcResponse.Error(1, JsonRpcError.InvalidParams, "bad param", "extra info");
            Assert.AreEqual("bad param", resp.error.message);
            Assert.AreEqual("extra info", resp.error.data);
        }

        [Test]
        public void JsonRpcResponse_Error_NullId_IsValid()
        {
            var resp = JsonRpcResponse.Error(null, JsonRpcError.ParseError, "parse failed");
            Assert.IsNull(resp.id);
            Assert.AreEqual(JsonRpcError.ParseError, resp.error.code);
        }

        // ── JsonRpcError Constants ─────────────────────────────────────────

        [Test]
        public void JsonRpcError_StandardCodes_AreCorrect()
        {
            Assert.AreEqual(-32700, JsonRpcError.ParseError);
            Assert.AreEqual(-32600, JsonRpcError.InvalidRequest);
            Assert.AreEqual(-32601, JsonRpcError.MethodNotFound);
            Assert.AreEqual(-32602, JsonRpcError.InvalidParams);
            Assert.AreEqual(-32603, JsonRpcError.InternalError);
        }

        [Test]
        public void JsonRpcError_McpCodes_AreCorrect()
        {
            Assert.AreEqual(-32000, JsonRpcError.ConnectionError);
            Assert.AreEqual(-32001, JsonRpcError.ToolNotFound);
            Assert.AreEqual(-32002, JsonRpcError.ResourceNotFound);
            Assert.AreEqual(-32003, JsonRpcError.ExecutionError);
            Assert.AreEqual(-32004, JsonRpcError.TimeoutError);
            Assert.AreEqual(-32005, JsonRpcError.UnityError);
        }

        // ── McpContent ─────────────────────────────────────────────────────

        [Test]
        public void McpContent_DefaultValues_AreCorrect()
        {
            var content = new McpContent();
            Assert.AreEqual("text", content.type);
            Assert.IsNull(content.text);
            Assert.IsNull(content.mimeType);
            Assert.IsNull(content.data);
        }

        [Test]
        public void McpContent_Text_CreatesTextContent()
        {
            var content = McpContent.Text("hello world");
            Assert.AreEqual("text", content.type);
            Assert.AreEqual("hello world", content.text);
            Assert.IsNull(content.mimeType);
            Assert.IsNull(content.data);
        }

        [Test]
        public void McpContent_Text_EmptyString_IsValid()
        {
            var content = McpContent.Text("");
            Assert.AreEqual("text", content.type);
            Assert.AreEqual("", content.text);
        }

        [Test]
        public void McpContent_Text_NullString_IsValid()
        {
            var content = McpContent.Text(null);
            Assert.AreEqual("text", content.type);
            Assert.IsNull(content.text);
        }

        [Test]
        public void McpContent_Json_CreatesJsonContent()
        {
            var obj = new Dictionary<string, object> { ["key"] = "value" };
            var content = McpContent.Json(obj);
            Assert.AreEqual("text", content.type);
            Assert.AreEqual("application/json", content.mimeType);
            Assert.IsNotNull(content.text);
            Assert.IsTrue(content.text.Contains("key"));
            Assert.IsTrue(content.text.Contains("value"));
        }

        [Test]
        public void McpContent_Image_CreatesImageContent()
        {
            var content = McpContent.Image("base64data==");
            Assert.AreEqual("image", content.type);
            Assert.AreEqual("base64data==", content.data);
            Assert.AreEqual("image/png", content.mimeType);
            Assert.IsNull(content.text);
        }

        [Test]
        public void McpContent_Image_CustomMimeType()
        {
            var content = McpContent.Image("data", "image/jpeg");
            Assert.AreEqual("image", content.type);
            Assert.AreEqual("image/jpeg", content.mimeType);
        }

        // ── McpToolResult ──────────────────────────────────────────────────

        [Test]
        public void McpToolResult_Success_String_CreatesCorrectResult()
        {
            var result = McpToolResult.Success("operation completed");
            Assert.IsFalse(result.isError);
            Assert.AreEqual(1, result.content.Count);
            Assert.AreEqual("text", result.content[0].type);
            Assert.AreEqual("operation completed", result.content[0].text);
        }

        [Test]
        public void McpToolResult_Success_ContentList_CreatesCorrectResult()
        {
            var contentList = new List<McpContent>
            {
                McpContent.Text("result 1"),
                McpContent.Text("result 2")
            };
            var result = McpToolResult.Success(contentList);
            Assert.IsFalse(result.isError);
            Assert.AreEqual(2, result.content.Count);
        }

        [Test]
        public void McpToolResult_Error_CreatesErrorResult()
        {
            var result = McpToolResult.Error("something went wrong");
            Assert.IsTrue(result.isError);
            Assert.AreEqual(1, result.content.Count);
            Assert.AreEqual("something went wrong", result.content[0].text);
        }

        [Test]
        public void McpToolResult_DefaultContent_IsEmptyList()
        {
            var result = new McpToolResult();
            Assert.IsNotNull(result.content);
            Assert.AreEqual(0, result.content.Count);
            Assert.IsFalse(result.isError);
        }

        // ── McpToolDefinition ──────────────────────────────────────────────

        [Test]
        public void McpToolDefinition_CanBeCreated()
        {
            var def = new McpToolDefinition
            {
                name = "unity_test_tool",
                description = "A test tool",
                inputSchema = new McpInputSchema
                {
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["param1"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "A parameter"
                        }
                    },
                    required = new List<string> { "param1" }
                }
            };

            Assert.AreEqual("unity_test_tool", def.name);
            Assert.AreEqual("A test tool", def.description);
            Assert.AreEqual("object", def.inputSchema.type);
            Assert.AreEqual(1, def.inputSchema.properties.Count);
            Assert.AreEqual(1, def.inputSchema.required.Count);
        }

        // ── McpInputSchema ────────────────────────────────────────────────

        [Test]
        public void McpInputSchema_DefaultType_IsObject()
        {
            var schema = new McpInputSchema();
            Assert.AreEqual("object", schema.type);
            Assert.IsNotNull(schema.properties);
            Assert.IsNotNull(schema.required);
        }

        // ── McpPropertySchema ──────────────────────────────────────────────

        [Test]
        public void McpPropertySchema_WithEnum_StoresValues()
        {
            var schema = new McpPropertySchema
            {
                type = "string",
                description = "Platform",
                @enum = new List<string> { "windows", "mac", "linux" },
                @default = "windows"
            };

            Assert.AreEqual("string", schema.type);
            Assert.AreEqual(3, schema.@enum.Count);
            Assert.AreEqual("windows", schema.@default);
        }

        // ── McpResourceDefinition ──────────────────────────────────────────

        [Test]
        public void McpResourceDefinition_CanBeCreated()
        {
            var def = new McpResourceDefinition
            {
                uri = "unity://project/settings",
                name = "Project Settings",
                description = "Current project settings",
                mimeType = "application/json"
            };

            Assert.AreEqual("unity://project/settings", def.uri);
            Assert.AreEqual("Project Settings", def.name);
            Assert.AreEqual("application/json", def.mimeType);
        }

        // ── McpResourceContent ─────────────────────────────────────────────

        [Test]
        public void McpResourceContent_TextResource_CanBeCreated()
        {
            var content = new McpResourceContent
            {
                uri = "unity://test",
                mimeType = "text/plain",
                text = "hello"
            };

            Assert.AreEqual("unity://test", content.uri);
            Assert.AreEqual("hello", content.text);
            Assert.IsNull(content.blob);
        }

        [Test]
        public void McpResourceContent_BlobResource_CanBeCreated()
        {
            var content = new McpResourceContent
            {
                uri = "unity://binary",
                mimeType = "image/png",
                blob = "base64..."
            };

            Assert.AreEqual("base64...", content.blob);
            Assert.IsNull(content.text);
        }

        // ── McpResourceResult ──────────────────────────────────────────────

        [Test]
        public void McpResourceResult_DefaultContents_IsEmptyList()
        {
            var result = new McpResourceResult();
            Assert.IsNotNull(result.contents);
            Assert.AreEqual(0, result.contents.Count);
        }

        // ── McpServerCapabilities ──────────────────────────────────────────

        [Test]
        public void McpToolsCapability_DefaultListChanged_IsTrue()
        {
            var cap = new McpToolsCapability();
            Assert.IsTrue(cap.listChanged);
        }

        [Test]
        public void McpResourcesCapability_DefaultValues()
        {
            var cap = new McpResourcesCapability();
            Assert.IsFalse(cap.subscribe);
            Assert.IsTrue(cap.listChanged);
        }

        [Test]
        public void McpPromptsCapability_DefaultListChanged_IsFalse()
        {
            var cap = new McpPromptsCapability();
            Assert.IsFalse(cap.listChanged);
        }

        // ── McpInitializeResult ────────────────────────────────────────────

        [Test]
        public void McpInitializeResult_DefaultProtocolVersion()
        {
            var result = new McpInitializeResult();
            Assert.AreEqual("2024-11-05", result.protocolVersion);
        }

        [Test]
        public void McpInitializeResult_FullSetup()
        {
            var result = new McpInitializeResult
            {
                capabilities = new McpServerCapabilities
                {
                    tools = new McpToolsCapability(),
                    resources = new McpResourcesCapability(),
                    prompts = new McpPromptsCapability()
                },
                serverInfo = new McpServerInfo
                {
                    name = "MCP Unity Server",
                    version = "1.0.0"
                }
            };

            Assert.IsNotNull(result.capabilities);
            Assert.IsNotNull(result.capabilities.tools);
            Assert.AreEqual("MCP Unity Server", result.serverInfo.name);
        }

        // ── ToolCallParams ─────────────────────────────────────────────────

        [Test]
        public void ToolCallParams_CanBeCreated()
        {
            var p = new ToolCallParams
            {
                name = "unity_test",
                arguments = new Dictionary<string, object>
                {
                    ["arg1"] = "value1",
                    ["arg2"] = 42
                }
            };

            Assert.AreEqual("unity_test", p.name);
            Assert.AreEqual(2, p.arguments.Count);
        }

        // ── ResourceReadParams ─────────────────────────────────────────────

        [Test]
        public void ResourceReadParams_CanBeCreated()
        {
            var p = new ResourceReadParams { uri = "unity://scene/hierarchy" };
            Assert.AreEqual("unity://scene/hierarchy", p.uri);
        }
    }
}
