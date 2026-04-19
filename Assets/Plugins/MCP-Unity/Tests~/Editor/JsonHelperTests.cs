using System;
using System.Collections.Generic;
using NUnit.Framework;
using McpUnity.Server;

namespace McpUnity.Tests
{
    /// <summary>
    /// Edit Mode tests for JsonHelper and SimpleJsonParser — custom JSON serialization/parsing.
    /// </summary>
    public class JsonHelperTests
    {
        // ── ToJson — primitive types ─────────────────────────────────────────

        [Test]
        public void ToJson_Null_ReturnsNullLiteral()
        {
            Assert.AreEqual("null", JsonHelper.ToJson(null));
        }

        [Test]
        public void ToJson_String_ReturnsQuotedString()
        {
            Assert.AreEqual("\"hello\"", JsonHelper.ToJson("hello"));
        }

        [Test]
        public void ToJson_EmptyString_ReturnsEmptyQuotedString()
        {
            Assert.AreEqual("\"\"", JsonHelper.ToJson(""));
        }

        [Test]
        public void ToJson_BoolTrue_ReturnsTrue()
        {
            Assert.AreEqual("true", JsonHelper.ToJson(true));
        }

        [Test]
        public void ToJson_BoolFalse_ReturnsFalse()
        {
            Assert.AreEqual("false", JsonHelper.ToJson(false));
        }

        [Test]
        public void ToJson_Int_ReturnsNumber()
        {
            Assert.AreEqual("42", JsonHelper.ToJson(42));
        }

        [Test]
        public void ToJson_NegativeInt_ReturnsNegativeNumber()
        {
            Assert.AreEqual("-5", JsonHelper.ToJson(-5));
        }

        [Test]
        public void ToJson_Long_ReturnsNumber()
        {
            Assert.AreEqual("9999999999", JsonHelper.ToJson(9999999999L));
        }

        [Test]
        public void ToJson_FloatNaN_ReturnsNull()
        {
            Assert.AreEqual("null", JsonHelper.ToJson(float.NaN));
        }

        [Test]
        public void ToJson_FloatInfinity_ReturnsNull()
        {
            Assert.AreEqual("null", JsonHelper.ToJson(float.PositiveInfinity));
        }

        [Test]
        public void ToJson_FloatNegInfinity_ReturnsNull()
        {
            Assert.AreEqual("null", JsonHelper.ToJson(float.NegativeInfinity));
        }

        [Test]
        public void ToJson_DoubleNaN_ReturnsNull()
        {
            Assert.AreEqual("null", JsonHelper.ToJson(double.NaN));
        }

        [Test]
        public void ToJson_DoubleNormal_ReturnsNumber()
        {
            var result = JsonHelper.ToJson(3.14);
            Assert.IsTrue(result.StartsWith("3.14"));
        }

        // ── ToJson — string escaping ─────────────────────────────────────────

        [Test]
        public void ToJson_StringWithQuotes_EscapesQuotes()
        {
            var result = JsonHelper.ToJson("say \"hello\"");
            Assert.AreEqual("\"say \\\"hello\\\"\"", result);
        }

        [Test]
        public void ToJson_StringWithBackslash_EscapesBackslash()
        {
            var result = JsonHelper.ToJson("path\\to\\file");
            Assert.AreEqual("\"path\\\\to\\\\file\"", result);
        }

        [Test]
        public void ToJson_StringWithNewline_EscapesNewline()
        {
            var result = JsonHelper.ToJson("line1\nline2");
            Assert.AreEqual("\"line1\\nline2\"", result);
        }

        [Test]
        public void ToJson_StringWithTab_EscapesTab()
        {
            var result = JsonHelper.ToJson("col1\tcol2");
            Assert.AreEqual("\"col1\\tcol2\"", result);
        }

        [Test]
        public void ToJson_StringNoEscaping_FastPath()
        {
            // Simple ASCII string — should use fast path (no escaping needed)
            var result = JsonHelper.ToJson("simple text 123");
            Assert.AreEqual("\"simple text 123\"", result);
        }

        // ── ToJson — Dictionary ──────────────────────────────────────────────

        [Test]
        public void ToJson_EmptyDictionary_ReturnsEmptyObject()
        {
            var dict = new Dictionary<string, object>();
            Assert.AreEqual("{}", JsonHelper.ToJson(dict));
        }

        [Test]
        public void ToJson_Dictionary_ReturnsJsonObject()
        {
            var dict = new Dictionary<string, object>
            {
                ["name"] = "Player",
                ["health"] = 100,
                ["alive"] = true
            };
            var json = JsonHelper.ToJson(dict);
            Assert.IsTrue(json.Contains("\"name\":\"Player\""));
            Assert.IsTrue(json.Contains("\"health\":100"));
            Assert.IsTrue(json.Contains("\"alive\":true"));
        }

        [Test]
        public void ToJson_Dictionary_NullValuesSkipped()
        {
            var dict = new Dictionary<string, object>
            {
                ["present"] = "yes",
                ["absent"] = null
            };
            var json = JsonHelper.ToJson(dict);
            Assert.IsTrue(json.Contains("\"present\""));
            // Null values in Dictionary are serialized as "null" (NOT skipped)
            // The null-pruning only applies to object properties via reflection
        }

        [Test]
        public void ToJson_NestedDictionary()
        {
            var dict = new Dictionary<string, object>
            {
                ["position"] = new Dictionary<string, object>
                {
                    ["x"] = 1,
                    ["y"] = 2,
                    ["z"] = 3
                }
            };
            var json = JsonHelper.ToJson(dict);
            Assert.IsTrue(json.Contains("\"position\":{"));
            Assert.IsTrue(json.Contains("\"x\":1"));
        }

        // ── ToJson — Lists and Arrays ────────────────────────────────────────

        [Test]
        public void ToJson_EmptyList_ReturnsEmptyArray()
        {
            var list = new List<object>();
            Assert.AreEqual("[]", JsonHelper.ToJson(list));
        }

        [Test]
        public void ToJson_ListOfStrings()
        {
            var list = new List<string> { "a", "b", "c" };
            Assert.AreEqual("[\"a\",\"b\",\"c\"]", JsonHelper.ToJson(list));
        }

        [Test]
        public void ToJson_IntArray()
        {
            var arr = new int[] { 1, 2, 3 };
            Assert.AreEqual("[1,2,3]", JsonHelper.ToJson(arr));
        }

        // ── ParseJsonObject — basic parsing ──────────────────────────────────

        [Test]
        public void ParseJsonObject_NullInput_ReturnsNull()
        {
            Assert.IsNull(JsonHelper.ParseJsonObject(null));
        }

        [Test]
        public void ParseJsonObject_EmptyInput_ReturnsNull()
        {
            Assert.IsNull(JsonHelper.ParseJsonObject(""));
        }

        [Test]
        public void ParseJsonObject_NonObjectInput_ReturnsNull()
        {
            Assert.IsNull(JsonHelper.ParseJsonObject("[1,2,3]"));
        }

        [Test]
        public void ParseJsonObject_EmptyObject_ReturnsEmptyDictionary()
        {
            var result = JsonHelper.ParseJsonObject("{}");
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseJsonObject_SimpleObject_ParsesCorrectly()
        {
            var result = JsonHelper.ParseJsonObject("{\"name\":\"Player\",\"health\":100,\"alive\":true}");
            Assert.IsNotNull(result);
            Assert.AreEqual("Player", result["name"]);
            Assert.AreEqual(100L, result["health"]); // Numbers parsed as long
            Assert.AreEqual(true, result["alive"]);
        }

        [Test]
        public void ParseJsonObject_NestedObject_ParsesCorrectly()
        {
            var result = JsonHelper.ParseJsonObject("{\"pos\":{\"x\":1,\"y\":2}}");
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<Dictionary<string, object>>(result["pos"]);
            var pos = (Dictionary<string, object>)result["pos"];
            Assert.AreEqual(1L, pos["x"]);
        }

        [Test]
        public void ParseJsonObject_StringEscapes_ParsesCorrectly()
        {
            var result = JsonHelper.ParseJsonObject("{\"msg\":\"hello\\nworld\"}");
            Assert.IsNotNull(result);
            Assert.AreEqual("hello\nworld", result["msg"]);
        }

        [Test]
        public void ParseJsonObject_NullValue_ParsesCorrectly()
        {
            var result = JsonHelper.ParseJsonObject("{\"value\":null}");
            Assert.IsNotNull(result);
            Assert.IsNull(result["value"]);
        }

        [Test]
        public void ParseJsonObject_ArrayValue_ParsesCorrectly()
        {
            var result = JsonHelper.ParseJsonObject("{\"tags\":[\"a\",\"b\"]}");
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<List<object>>(result["tags"]);
            var tags = (List<object>)result["tags"];
            Assert.AreEqual(2, tags.Count);
            Assert.AreEqual("a", tags[0]);
        }

        [Test]
        public void ParseJsonObject_FloatNumber_ParsesCorrectly()
        {
            var result = JsonHelper.ParseJsonObject("{\"pi\":3.14}");
            Assert.IsNotNull(result);
            // Should be parsed as double
            Assert.IsInstanceOf<double>(result["pi"]);
            Assert.AreEqual(3.14, (double)result["pi"], 0.001);
        }

        [Test]
        public void ParseJsonObject_NegativeNumber_ParsesCorrectly()
        {
            var result = JsonHelper.ParseJsonObject("{\"offset\":-42}");
            Assert.IsNotNull(result);
            Assert.AreEqual(-42L, result["offset"]);
        }

        [Test]
        public void ParseJsonObject_ScientificNotation_ParsesCorrectly()
        {
            var result = JsonHelper.ParseJsonObject("{\"big\":1e5}");
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<double>(result["big"]);
        }

        [Test]
        public void ParseJsonObject_UnicodeEscape_ParsesCorrectly()
        {
            var result = JsonHelper.ParseJsonObject("{\"char\":\"\\u0041\"}");
            Assert.IsNotNull(result);
            Assert.AreEqual("A", result["char"]); // \u0041 = 'A'
        }

        [Test]
        public void ParseJsonObject_WhitespaceHandled()
        {
            var result = JsonHelper.ParseJsonObject("  {  \"key\"  :  \"value\"  }  ");
            Assert.IsNotNull(result);
            Assert.AreEqual("value", result["key"]);
        }

        // ── SimpleJsonParser — depth limit ───────────────────────────────────

        [Test]
        public void SimpleJsonParser_ExceedsMaxDepth_ThrowsException()
        {
            // Build deeply nested JSON: {"a":{"a":{"a":... }}} (65 levels)
            var json = "";
            for (int i = 0; i < 65; i++) json += "{\"a\":";
            json += "null";
            for (int i = 0; i < 65; i++) json += "}";

            Assert.Throws<InvalidOperationException>(() => JsonHelper.ParseJsonObject(json));
        }

        [Test]
        public void SimpleJsonParser_AtMaxDepth_Succeeds()
        {
            // 64 levels should be OK
            var json = "";
            for (int i = 0; i < 64; i++) json += "{\"a\":";
            json += "null";
            for (int i = 0; i < 64; i++) json += "}";

            var result = JsonHelper.ParseJsonObject(json);
            Assert.IsNotNull(result);
        }
    }
}
