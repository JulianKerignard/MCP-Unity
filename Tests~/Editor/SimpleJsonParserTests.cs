using System.Collections.Generic;
using NUnit.Framework;
using McpUnity.Server;

namespace McpUnity.Tests
{
    /// <summary>
    /// Edit Mode tests for SimpleJsonParser — recursive descent JSON parser.
    /// </summary>
    public class SimpleJsonParserTests
    {
        // ── Empty / Null inputs ────────────────────────────────────────────

        [Test]
        public void ParseObject_EmptyObject_ReturnsEmptyDictionary()
        {
            var parser = new SimpleJsonParser("{}");
            var result = parser.ParseObject();
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseObject_EmptyString_ReturnsNull()
        {
            var parser = new SimpleJsonParser("");
            var result = parser.ParseObject();
            Assert.IsNull(result);
        }

        [Test]
        public void ParseObject_NotAnObject_ReturnsNull()
        {
            var parser = new SimpleJsonParser("[1,2,3]");
            var result = parser.ParseObject();
            Assert.IsNull(result);
        }

        // ── String values ──────────────────────────────────────────────────

        [Test]
        public void ParseObject_StringValue_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"name\":\"Player\"}");
            var result = parser.ParseObject();
            Assert.AreEqual("Player", result["name"]);
        }

        [Test]
        public void ParseObject_EmptyStringValue_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"key\":\"\"}");
            var result = parser.ParseObject();
            Assert.AreEqual("", result["key"]);
        }

        [Test]
        public void ParseObject_EscapedQuotes_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"msg\":\"he said \\\"hello\\\"\"}");
            var result = parser.ParseObject();
            Assert.AreEqual("he said \"hello\"", result["msg"]);
        }

        [Test]
        public void ParseObject_EscapedBackslash_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"path\":\"C:\\\\Users\\\\test\"}");
            var result = parser.ParseObject();
            Assert.AreEqual("C:\\Users\\test", result["path"]);
        }

        [Test]
        public void ParseObject_EscapedNewlineAndTab_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"text\":\"line1\\nline2\\ttab\"}");
            var result = parser.ParseObject();
            Assert.AreEqual("line1\nline2\ttab", result["text"]);
        }

        [Test]
        public void ParseObject_EscapedSlash_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"url\":\"http:\\/\\/example.com\"}");
            var result = parser.ParseObject();
            Assert.AreEqual("http://example.com", result["url"]);
        }

        [Test]
        public void ParseObject_EscapedBackspaceAndFormFeed_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"ctrl\":\"a\\bb\\fc\"}");
            var result = parser.ParseObject();
            Assert.AreEqual("a\bb\fc", result["ctrl"]);
        }

        [Test]
        public void ParseObject_UnicodeEscape_ParsesCorrectly()
        {
            // \u0041 = 'A'
            var parser = new SimpleJsonParser("{\"char\":\"\\u0041\"}");
            var result = parser.ParseObject();
            Assert.AreEqual("A", result["char"]);
        }

        [Test]
        public void ParseObject_UnicodeEscapeNonAscii_ParsesCorrectly()
        {
            // \u00E9 = 'é'
            var parser = new SimpleJsonParser("{\"char\":\"caf\\u00E9\"}");
            var result = parser.ParseObject();
            Assert.AreEqual("café", result["char"]);
        }

        // ── Number values ──────────────────────────────────────────────────

        [Test]
        public void ParseObject_IntegerValue_ReturnsInt()
        {
            var parser = new SimpleJsonParser("{\"count\":42}");
            var result = parser.ParseObject();
            Assert.AreEqual(42, result["count"]);
            Assert.IsInstanceOf<int>(result["count"]);
        }

        [Test]
        public void ParseObject_NegativeInteger_ReturnsInt()
        {
            var parser = new SimpleJsonParser("{\"val\":-7}");
            var result = parser.ParseObject();
            Assert.AreEqual(-7, result["val"]);
        }

        [Test]
        public void ParseObject_Zero_ReturnsInt()
        {
            var parser = new SimpleJsonParser("{\"val\":0}");
            var result = parser.ParseObject();
            Assert.AreEqual(0, result["val"]);
            Assert.IsInstanceOf<int>(result["val"]);
        }

        [Test]
        public void ParseObject_FloatValue_ReturnsDouble()
        {
            var parser = new SimpleJsonParser("{\"pi\":3.14}");
            var result = parser.ParseObject();
            Assert.AreEqual(3.14, (double)result["pi"], 0.001);
            Assert.IsInstanceOf<double>(result["pi"]);
        }

        [Test]
        public void ParseObject_NegativeFloat_ReturnsDouble()
        {
            var parser = new SimpleJsonParser("{\"val\":-0.5}");
            var result = parser.ParseObject();
            Assert.AreEqual(-0.5, (double)result["val"], 0.001);
        }

        [Test]
        public void ParseObject_ScientificNotation_ReturnsDouble()
        {
            var parser = new SimpleJsonParser("{\"big\":1.5e10}");
            var result = parser.ParseObject();
            Assert.AreEqual(1.5e10, (double)result["big"], 1e6);
            Assert.IsInstanceOf<double>(result["big"]);
        }

        [Test]
        public void ParseObject_ScientificNotationUpperE_ReturnsDouble()
        {
            var parser = new SimpleJsonParser("{\"val\":2E3}");
            var result = parser.ParseObject();
            Assert.AreEqual(2000.0, (double)result["val"], 0.1);
        }

        [Test]
        public void ParseObject_ScientificNotationNegativeExponent_ReturnsDouble()
        {
            var parser = new SimpleJsonParser("{\"val\":5e-2}");
            var result = parser.ParseObject();
            Assert.AreEqual(0.05, (double)result["val"], 0.001);
        }

        [Test]
        public void ParseObject_ScientificNotationPositiveSign_ReturnsDouble()
        {
            var parser = new SimpleJsonParser("{\"val\":1e+3}");
            var result = parser.ParseObject();
            Assert.AreEqual(1000.0, (double)result["val"], 0.1);
        }

        [Test]
        public void ParseObject_LargeInteger_ReturnsLong()
        {
            // Larger than int.MaxValue
            var parser = new SimpleJsonParser("{\"big\":3000000000}");
            var result = parser.ParseObject();
            Assert.AreEqual(3000000000L, result["big"]);
            Assert.IsInstanceOf<long>(result["big"]);
        }

        // ── Boolean values ─────────────────────────────────────────────────

        [Test]
        public void ParseObject_True_ReturnsBool()
        {
            var parser = new SimpleJsonParser("{\"active\":true}");
            var result = parser.ParseObject();
            Assert.AreEqual(true, result["active"]);
            Assert.IsInstanceOf<bool>(result["active"]);
        }

        [Test]
        public void ParseObject_False_ReturnsBool()
        {
            var parser = new SimpleJsonParser("{\"active\":false}");
            var result = parser.ParseObject();
            Assert.AreEqual(false, result["active"]);
        }

        // ── Null values ────────────────────────────────────────────────────

        [Test]
        public void ParseObject_NullValue_ReturnsNull()
        {
            var parser = new SimpleJsonParser("{\"data\":null}");
            var result = parser.ParseObject();
            Assert.IsTrue(result.ContainsKey("data"));
            Assert.IsNull(result["data"]);
        }

        // ── Array values ───────────────────────────────────────────────────

        [Test]
        public void ParseObject_EmptyArray_ReturnsEmptyList()
        {
            var parser = new SimpleJsonParser("{\"items\":[]}");
            var result = parser.ParseObject();
            var items = result["items"] as List<object>;
            Assert.IsNotNull(items);
            Assert.AreEqual(0, items.Count);
        }

        [Test]
        public void ParseObject_IntArray_ReturnsListOfInts()
        {
            var parser = new SimpleJsonParser("{\"nums\":[1,2,3]}");
            var result = parser.ParseObject();
            var nums = result["nums"] as List<object>;
            Assert.IsNotNull(nums);
            Assert.AreEqual(3, nums.Count);
            Assert.AreEqual(1, nums[0]);
            Assert.AreEqual(2, nums[1]);
            Assert.AreEqual(3, nums[2]);
        }

        [Test]
        public void ParseObject_StringArray_ReturnsListOfStrings()
        {
            var parser = new SimpleJsonParser("{\"tags\":[\"a\",\"b\",\"c\"]}");
            var result = parser.ParseObject();
            var tags = result["tags"] as List<object>;
            Assert.IsNotNull(tags);
            Assert.AreEqual(3, tags.Count);
            Assert.AreEqual("a", tags[0]);
            Assert.AreEqual("b", tags[1]);
            Assert.AreEqual("c", tags[2]);
        }

        [Test]
        public void ParseObject_MixedArray_ReturnsListOfMixedTypes()
        {
            var parser = new SimpleJsonParser("{\"mix\":[1,\"two\",true,null,3.14]}");
            var result = parser.ParseObject();
            var mix = result["mix"] as List<object>;
            Assert.IsNotNull(mix);
            Assert.AreEqual(5, mix.Count);
            Assert.AreEqual(1, mix[0]);
            Assert.AreEqual("two", mix[1]);
            Assert.AreEqual(true, mix[2]);
            Assert.IsNull(mix[3]);
            Assert.AreEqual(3.14, (double)mix[4], 0.001);
        }

        [Test]
        public void ParseObject_NestedArray_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"matrix\":[[1,2],[3,4]]}");
            var result = parser.ParseObject();
            var matrix = result["matrix"] as List<object>;
            Assert.IsNotNull(matrix);
            Assert.AreEqual(2, matrix.Count);

            var row1 = matrix[0] as List<object>;
            Assert.IsNotNull(row1);
            Assert.AreEqual(1, row1[0]);
            Assert.AreEqual(2, row1[1]);
        }

        // ── Nested objects ─────────────────────────────────────────────────

        [Test]
        public void ParseObject_NestedObject_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"pos\":{\"x\":1,\"y\":2,\"z\":3}}");
            var result = parser.ParseObject();
            var pos = result["pos"] as Dictionary<string, object>;
            Assert.IsNotNull(pos);
            Assert.AreEqual(1, pos["x"]);
            Assert.AreEqual(2, pos["y"]);
            Assert.AreEqual(3, pos["z"]);
        }

        [Test]
        public void ParseObject_DeeplyNested_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"a\":{\"b\":{\"c\":{\"d\":\"deep\"}}}}");
            var result = parser.ParseObject();
            var a = result["a"] as Dictionary<string, object>;
            var b = a["b"] as Dictionary<string, object>;
            var c = b["c"] as Dictionary<string, object>;
            Assert.AreEqual("deep", c["d"]);
        }

        [Test]
        public void ParseObject_EmptyNestedObject_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"empty\":{}}");
            var result = parser.ParseObject();
            var empty = result["empty"] as Dictionary<string, object>;
            Assert.IsNotNull(empty);
            Assert.AreEqual(0, empty.Count);
        }

        // ── Multiple keys ──────────────────────────────────────────────────

        [Test]
        public void ParseObject_MultipleKeys_AllParsed()
        {
            var json = "{\"name\":\"Player\",\"hp\":100,\"alive\":true,\"weapon\":null}";
            var parser = new SimpleJsonParser(json);
            var result = parser.ParseObject();
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("Player", result["name"]);
            Assert.AreEqual(100, result["hp"]);
            Assert.AreEqual(true, result["alive"]);
            Assert.IsNull(result["weapon"]);
        }

        // ── Whitespace handling ────────────────────────────────────────────

        [Test]
        public void ParseObject_WithWhitespace_ParsesCorrectly()
        {
            var json = "  {  \"key\"  :  \"value\"  }  ";
            var parser = new SimpleJsonParser(json);
            var result = parser.ParseObject();
            Assert.AreEqual("value", result["key"]);
        }

        [Test]
        public void ParseObject_WithNewlinesAndTabs_ParsesCorrectly()
        {
            var json = "{\n\t\"a\": 1,\n\t\"b\": 2\n}";
            var parser = new SimpleJsonParser(json);
            var result = parser.ParseObject();
            Assert.AreEqual(1, result["a"]);
            Assert.AreEqual(2, result["b"]);
        }

        // ── Complex / realistic JSON ───────────────────────────────────────

        [Test]
        public void ParseObject_ToolCallParams_ParsesCorrectly()
        {
            var json = "{\"name\":\"unity_create_gameobject\",\"arguments\":{\"objectName\":\"Cube\",\"position\":{\"x\":0,\"y\":1,\"z\":0}}}";
            var parser = new SimpleJsonParser(json);
            var result = parser.ParseObject();

            Assert.AreEqual("unity_create_gameobject", result["name"]);

            var args = result["arguments"] as Dictionary<string, object>;
            Assert.IsNotNull(args);
            Assert.AreEqual("Cube", args["objectName"]);

            var pos = args["position"] as Dictionary<string, object>;
            Assert.IsNotNull(pos);
            Assert.AreEqual(0, pos["x"]);
            Assert.AreEqual(1, pos["y"]);
            Assert.AreEqual(0, pos["z"]);
        }

        [Test]
        public void ParseObject_JsonRpcRequest_ParsesCorrectly()
        {
            var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"test\"}}";
            var parser = new SimpleJsonParser(json);
            var result = parser.ParseObject();

            Assert.AreEqual("2.0", result["jsonrpc"]);
            Assert.AreEqual(1, result["id"]);
            Assert.AreEqual("tools/call", result["method"]);

            var p = result["params"] as Dictionary<string, object>;
            Assert.IsNotNull(p);
            Assert.AreEqual("test", p["name"]);
        }

        [Test]
        public void ParseObject_ArrayOfObjects_ParsesCorrectly()
        {
            var json = "{\"tools\":[{\"name\":\"a\"},{\"name\":\"b\"}]}";
            var parser = new SimpleJsonParser(json);
            var result = parser.ParseObject();

            var tools = result["tools"] as List<object>;
            Assert.IsNotNull(tools);
            Assert.AreEqual(2, tools.Count);

            var tool1 = tools[0] as Dictionary<string, object>;
            Assert.AreEqual("a", tool1["name"]);

            var tool2 = tools[1] as Dictionary<string, object>;
            Assert.AreEqual("b", tool2["name"]);
        }

        // ── Edge cases ─────────────────────────────────────────────────────

        [Test]
        public void ParseObject_DuplicateKeys_LastWins()
        {
            var parser = new SimpleJsonParser("{\"key\":1,\"key\":2}");
            var result = parser.ParseObject();
            Assert.AreEqual(2, result["key"]);
        }

        [Test]
        public void ParseObject_SingleCharKey_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"x\":5}");
            var result = parser.ParseObject();
            Assert.AreEqual(5, result["x"]);
        }

        [Test]
        public void ParseObject_SpecialCharactersInValue_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"msg\":\"hello\\nworld\\t!\"}");
            var result = parser.ParseObject();
            Assert.AreEqual("hello\nworld\t!", result["msg"]);
        }

        [Test]
        public void ParseObject_IntegerZeroInArray_ParsesCorrectly()
        {
            var parser = new SimpleJsonParser("{\"arr\":[0]}");
            var result = parser.ParseObject();
            var arr = result["arr"] as List<object>;
            Assert.AreEqual(0, arr[0]);
        }

        // ── Max depth protection ───────────────────────────────────────────

        [Test]
        public void ParseObject_ExceedMaxDepth_ThrowsException()
        {
            // Build JSON nested 65 levels deep (max is 64)
            var json = "";
            for (int i = 0; i < 65; i++) json += "{\"a\":";
            json += "1";
            for (int i = 0; i < 65; i++) json += "}";

            var parser = new SimpleJsonParser(json);
            Assert.Throws<System.InvalidOperationException>(() => parser.ParseObject());
        }

        [Test]
        public void ParseObject_AtMaxDepth_DoesNotThrow()
        {
            // Build JSON nested exactly 64 levels deep
            var json = "";
            for (int i = 0; i < 64; i++) json += "{\"a\":";
            json += "1";
            for (int i = 0; i < 64; i++) json += "}";

            var parser = new SimpleJsonParser(json);
            Assert.DoesNotThrow(() => parser.ParseObject());
        }
    }
}
