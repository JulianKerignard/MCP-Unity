using System.Collections.Generic;
using NUnit.Framework;
using McpUnity.Helpers;

namespace McpUnity.Tests
{
    /// <summary>
    /// Edit Mode tests for ArgumentParser — safe typed extraction from tool argument dictionaries.
    /// </summary>
    public class ArgumentParserTests
    {
        // ── GetString ────────────────────────────────────────────────────────

        [Test]
        public void GetString_ExistingKey_ReturnsValue()
        {
            var args = new Dictionary<string, object> { ["name"] = "Player" };
            Assert.AreEqual("Player", ArgumentParser.GetString(args, "name"));
        }

        [Test]
        public void GetString_MissingKey_ReturnsDefault()
        {
            var args = new Dictionary<string, object>();
            Assert.IsNull(ArgumentParser.GetString(args, "name"));
            Assert.AreEqual("fallback", ArgumentParser.GetString(args, "name", "fallback"));
        }

        [Test]
        public void GetString_NullArgs_ReturnsDefault()
        {
            Assert.AreEqual("x", ArgumentParser.GetString(null, "key", "x"));
        }

        [Test]
        public void GetString_NullValue_ReturnsDefault()
        {
            var args = new Dictionary<string, object> { ["key"] = null };
            Assert.AreEqual("def", ArgumentParser.GetString(args, "key", "def"));
        }

        // ── RequireString ────────────────────────────────────────────────────

        [Test]
        public void RequireString_Present_ReturnsValueAndNullError()
        {
            var args = new Dictionary<string, object> { ["path"] = "Assets/foo.cs" };
            var val = ArgumentParser.RequireString(args, "path", out var err);
            Assert.IsNull(err);
            Assert.AreEqual("Assets/foo.cs", val);
        }

        [Test]
        public void RequireString_Missing_ReturnsNullAndError()
        {
            var args = new Dictionary<string, object>();
            var val = ArgumentParser.RequireString(args, "path", out var err);
            Assert.IsNull(val);
            Assert.IsNotNull(err);
        }

        [Test]
        public void RequireString_Empty_ReturnsNullAndError()
        {
            var args = new Dictionary<string, object> { ["path"] = "   " };
            var val = ArgumentParser.RequireString(args, "path", out var err);
            Assert.IsNull(val);
            Assert.IsNotNull(err);
        }

        [Test]
        public void RequireString_NullArgs_ReturnsError()
        {
            var val = ArgumentParser.RequireString(null, "path", out var err);
            Assert.IsNull(val);
            Assert.IsNotNull(err);
        }

        // ── GetInt ───────────────────────────────────────────────────────────

        [Test]
        public void GetInt_IntValue_ReturnsCorrect()
        {
            var args = new Dictionary<string, object> { ["depth"] = 3 };
            Assert.AreEqual(3, ArgumentParser.GetInt(args, "depth", 0));
        }

        [Test]
        public void GetInt_LongValue_Clamped()
        {
            var args = new Dictionary<string, object> { ["val"] = (long)42 };
            Assert.AreEqual(42, ArgumentParser.GetInt(args, "val", 0));
        }

        [Test]
        public void GetInt_DoubleValue_Rounded()
        {
            var args = new Dictionary<string, object> { ["val"] = 2.9 };
            Assert.AreEqual(3, ArgumentParser.GetInt(args, "val", 0));
        }

        [Test]
        public void GetInt_StringValue_Parsed()
        {
            var args = new Dictionary<string, object> { ["count"] = "10" };
            Assert.AreEqual(10, ArgumentParser.GetInt(args, "count", 0));
        }

        [Test]
        public void GetInt_InvalidString_ReturnsDefault()
        {
            var args = new Dictionary<string, object> { ["count"] = "abc" };
            Assert.AreEqual(-1, ArgumentParser.GetInt(args, "count", -1));
        }

        [Test]
        public void GetInt_Missing_ReturnsDefault()
        {
            var args = new Dictionary<string, object>();
            Assert.AreEqual(7, ArgumentParser.GetInt(args, "missing", 7));
        }

        [Test]
        public void GetIntClamped_ClampsBelowMin()
        {
            var args = new Dictionary<string, object> { ["depth"] = -5 };
            Assert.AreEqual(1, ArgumentParser.GetIntClamped(args, "depth", 3, 1, 50));
        }

        [Test]
        public void GetIntClamped_ClampsAboveMax()
        {
            var args = new Dictionary<string, object> { ["depth"] = 999 };
            Assert.AreEqual(50, ArgumentParser.GetIntClamped(args, "depth", 3, 1, 50));
        }

        // ── GetFloat ─────────────────────────────────────────────────────────

        [Test]
        public void GetFloat_FloatValue_ReturnsCorrect()
        {
            var args = new Dictionary<string, object> { ["volume"] = 0.75f };
            Assert.AreEqual(0.75f, ArgumentParser.GetFloat(args, "volume"), 0.0001f);
        }

        [Test]
        public void GetFloat_DoubleValue_Converts()
        {
            var args = new Dictionary<string, object> { ["v"] = 0.5 };
            Assert.AreEqual(0.5f, ArgumentParser.GetFloat(args, "v"), 0.0001f);
        }

        [Test]
        public void GetFloat_StringValue_Parsed()
        {
            var args = new Dictionary<string, object> { ["v"] = "1.5" };
            Assert.AreEqual(1.5f, ArgumentParser.GetFloat(args, "v"), 0.0001f);
        }

        [Test]
        public void GetFloat_Missing_ReturnsDefault()
        {
            var args = new Dictionary<string, object>();
            Assert.AreEqual(0.5f, ArgumentParser.GetFloat(args, "missing", 0.5f), 0.0001f);
        }

        // ── GetBool ──────────────────────────────────────────────────────────

        [Test]
        public void GetBool_TrueValue_ReturnsTrue()
        {
            var args = new Dictionary<string, object> { ["active"] = true };
            Assert.IsTrue(ArgumentParser.GetBool(args, "active"));
        }

        [Test]
        public void GetBool_FalseValue_ReturnsFalse()
        {
            var args = new Dictionary<string, object> { ["active"] = false };
            Assert.IsFalse(ArgumentParser.GetBool(args, "active", true));
        }

        [Test]
        public void GetBool_StringTrue_ReturnsTrue()
        {
            var args = new Dictionary<string, object> { ["v"] = "true" };
            Assert.IsTrue(ArgumentParser.GetBool(args, "v"));
        }

        [Test]
        public void GetBool_StringFalse_ReturnsFalse()
        {
            var args = new Dictionary<string, object> { ["v"] = "false" };
            Assert.IsFalse(ArgumentParser.GetBool(args, "v", true));
        }

        [Test]
        public void GetBool_String1_ReturnsTrue()
        {
            var args = new Dictionary<string, object> { ["v"] = "1" };
            Assert.IsTrue(ArgumentParser.GetBool(args, "v"));
        }

        [Test]
        public void GetBool_StringYes_ReturnsTrue()
        {
            var args = new Dictionary<string, object> { ["v"] = "yes" };
            Assert.IsTrue(ArgumentParser.GetBool(args, "v"));
        }

        [Test]
        public void GetBool_IntZero_ReturnsFalse()
        {
            var args = new Dictionary<string, object> { ["v"] = 0 };
            Assert.IsFalse(ArgumentParser.GetBool(args, "v", true));
        }

        [Test]
        public void GetBool_IntNonZero_ReturnsTrue()
        {
            var args = new Dictionary<string, object> { ["v"] = 1 };
            Assert.IsTrue(ArgumentParser.GetBool(args, "v"));
        }

        [Test]
        public void GetBool_Missing_ReturnsDefault()
        {
            var args = new Dictionary<string, object>();
            Assert.IsTrue(ArgumentParser.GetBool(args, "missing", true));
        }

        // ── GetStringArray ────────────────────────────────────────────────────

        [Test]
        public void GetStringArray_ListOfStrings_ReturnsAll()
        {
            var list = new List<object> { "a", "b", "c" };
            var args = new Dictionary<string, object> { ["items"] = list };
            var result = ArgumentParser.GetStringArray(args, "items");
            Assert.AreEqual(new[] { "a", "b", "c" }, result);
        }

        [Test]
        public void GetStringArray_CommaSeparatedString_Splits()
        {
            var args = new Dictionary<string, object> { ["cats"] = "core,asset,ui" };
            var result = ArgumentParser.GetStringArray(args, "cats");
            CollectionAssert.Contains(result, "core");
            CollectionAssert.Contains(result, "asset");
            CollectionAssert.Contains(result, "ui");
        }

        [Test]
        public void GetStringArray_Missing_ReturnsEmpty()
        {
            var args = new Dictionary<string, object>();
            var result = ArgumentParser.GetStringArray(args, "missing");
            Assert.IsEmpty(result);
        }

        [Test]
        public void GetStringArray_NullArgs_ReturnsEmpty()
        {
            var result = ArgumentParser.GetStringArray(null, "key");
            Assert.IsEmpty(result);
        }

        // ── GetEnum ───────────────────────────────────────────────────────────

        [Test]
        public void GetEnum_StringValue_ParsesCaseInsensitive()
        {
            var args = new Dictionary<string, object> { ["mode"] = "single" };
            var result = ArgumentParser.GetEnum(args, "mode", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            Assert.AreEqual(UnityEngine.SceneManagement.LoadSceneMode.Single, result);
        }

        [Test]
        public void GetEnum_IntValue_MapsToEnum()
        {
            var args = new Dictionary<string, object> { ["mode"] = 0 };
            var result = ArgumentParser.GetEnum(args, "mode", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            Assert.AreEqual(UnityEngine.SceneManagement.LoadSceneMode.Single, result);
        }

        [Test]
        public void GetEnum_Missing_ReturnsDefault()
        {
            var args = new Dictionary<string, object>();
            var result = ArgumentParser.GetEnum(args, "mode", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            Assert.AreEqual(UnityEngine.SceneManagement.LoadSceneMode.Additive, result);
        }

        // ── HasKey ────────────────────────────────────────────────────────────

        [Test]
        public void HasKey_PresentNonNull_ReturnsTrue()
        {
            var args = new Dictionary<string, object> { ["x"] = 1 };
            Assert.IsTrue(ArgumentParser.HasKey(args, "x"));
        }

        [Test]
        public void HasKey_NullValue_ReturnsFalse()
        {
            var args = new Dictionary<string, object> { ["x"] = null };
            Assert.IsFalse(ArgumentParser.HasKey(args, "x"));
        }

        [Test]
        public void HasKey_Missing_ReturnsFalse()
        {
            var args = new Dictionary<string, object>();
            Assert.IsFalse(ArgumentParser.HasKey(args, "x"));
        }
    }
}
