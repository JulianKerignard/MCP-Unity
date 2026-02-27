using System.Collections.Generic;
using NUnit.Framework;
using McpUnity.Helpers;

namespace McpUnity.Tests
{
    /// <summary>
    /// Extended Edit Mode tests for ArgumentParser — covering methods missing from the original test file.
    /// </summary>
    public class ArgumentParserExtendedTests
    {
        // ── GetFloatClamped ──────────────────────────────────────────────────

        [Test]
        public void GetFloatClamped_BelowMin_ClampsToMin()
        {
            var args = new Dictionary<string, object> { ["val"] = -5.0f };
            Assert.AreEqual(0f, ArgumentParser.GetFloatClamped(args, "val", 0.5f, 0f, 1f));
        }

        [Test]
        public void GetFloatClamped_AboveMax_ClampsToMax()
        {
            var args = new Dictionary<string, object> { ["val"] = 99.9f };
            Assert.AreEqual(1f, ArgumentParser.GetFloatClamped(args, "val", 0.5f, 0f, 1f));
        }

        [Test]
        public void GetFloatClamped_InRange_ReturnsValue()
        {
            var args = new Dictionary<string, object> { ["val"] = 0.75 };
            Assert.AreEqual(0.75f, ArgumentParser.GetFloatClamped(args, "val", 0.5f, 0f, 1f), 0.001f);
        }

        [Test]
        public void GetFloatClamped_MissingKey_ReturnsDefault()
        {
            var args = new Dictionary<string, object>();
            Assert.AreEqual(0.5f, ArgumentParser.GetFloatClamped(args, "val", 0.5f, 0f, 1f));
        }

        // ── TryGetValue<T> ──────────────────────────────────────────────────

        [Test]
        public void TryGetValue_ExistingString_ReturnsTrue()
        {
            var args = new Dictionary<string, object> { ["name"] = "Player" };
            bool ok = ArgumentParser.TryGetValue<string>(args, "name", out var result);
            Assert.IsTrue(ok);
            Assert.AreEqual("Player", result);
        }

        [Test]
        public void TryGetValue_MissingKey_ReturnsFalse()
        {
            var args = new Dictionary<string, object>();
            bool ok = ArgumentParser.TryGetValue<string>(args, "name", out var result);
            Assert.IsFalse(ok);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetValue_WrongType_ReturnsFalse()
        {
            var args = new Dictionary<string, object> { ["count"] = "not-an-int" };
            bool ok = ArgumentParser.TryGetValue<int>(args, "count", out var result);
            Assert.IsFalse(ok);
            Assert.AreEqual(default(int), result);
        }

        [Test]
        public void TryGetValue_NullArgs_ReturnsFalse()
        {
            bool ok = ArgumentParser.TryGetValue<string>(null, "key", out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryGetValue_Dictionary_ReturnsTrue()
        {
            var inner = new Dictionary<string, object> { ["x"] = 1 };
            var args = new Dictionary<string, object> { ["pos"] = inner };
            bool ok = ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "pos", out var result);
            Assert.IsTrue(ok);
            Assert.AreEqual(1, ((Dictionary<string, object>)result).Count);
        }

        // ── GetEnum edge cases ──────────────────────────────────────────────

        [Test]
        public void GetEnum_InvalidString_ReturnsDefault()
        {
            var args = new Dictionary<string, object> { ["mode"] = "NonExistent" };
            var result = ArgumentParser.GetEnum(args, "mode", System.DayOfWeek.Monday);
            Assert.AreEqual(System.DayOfWeek.Monday, result);
        }

        // ── GetBool edge cases ──────────────────────────────────────────────

        [Test]
        public void GetBool_StringYes_ReturnsTrue()
        {
            var args = new Dictionary<string, object> { ["flag"] = "yes" };
            Assert.IsTrue(ArgumentParser.GetBool(args, "flag"));
        }

        [Test]
        public void GetBool_StringNo_ReturnsFalse()
        {
            var args = new Dictionary<string, object> { ["flag"] = "no" };
            Assert.IsFalse(ArgumentParser.GetBool(args, "flag"));
        }

        [Test]
        public void GetBool_DoubleZero_ReturnsFalse()
        {
            var args = new Dictionary<string, object> { ["flag"] = 0.0 };
            Assert.IsFalse(ArgumentParser.GetBool(args, "flag"));
        }

        [Test]
        public void GetBool_DoubleNonZero_ReturnsTrue()
        {
            var args = new Dictionary<string, object> { ["flag"] = 1.5 };
            Assert.IsTrue(ArgumentParser.GetBool(args, "flag"));
        }

        [Test]
        public void GetBool_LongZero_ReturnsFalse()
        {
            var args = new Dictionary<string, object> { ["flag"] = 0L };
            Assert.IsFalse(ArgumentParser.GetBool(args, "flag"));
        }

        [Test]
        public void GetBool_LongNonZero_ReturnsTrue()
        {
            var args = new Dictionary<string, object> { ["flag"] = 42L };
            Assert.IsTrue(ArgumentParser.GetBool(args, "flag"));
        }

        // ── GetString with non-string object ────────────────────────────────

        [Test]
        public void GetString_IntValue_ReturnsToString()
        {
            var args = new Dictionary<string, object> { ["val"] = 42 };
            // GetString should return the ToString() of the value
            var result = ArgumentParser.GetString(args, "val");
            Assert.AreEqual("42", result);
        }

        // ── HasKey edge cases ───────────────────────────────────────────────

        [Test]
        public void HasKey_EmptyKey_ReturnsFalse()
        {
            var args = new Dictionary<string, object> { ["a"] = 1 };
            Assert.IsFalse(ArgumentParser.HasKey(args, ""));
        }

        [Test]
        public void HasKey_NullKey_ReturnsFalse()
        {
            var args = new Dictionary<string, object> { ["a"] = 1 };
            Assert.IsFalse(ArgumentParser.HasKey(args, null));
        }

        // ── RequireString with null value ───────────────────────────────────

        [Test]
        public void RequireString_NullValue_ReturnsNull()
        {
            var args = new Dictionary<string, object> { ["name"] = null };
            var (value, error) = ArgumentParser.RequireString(args, "name");
            // Should return error since value is null
            Assert.IsNull(value);
            Assert.IsNotNull(error);
        }
    }
}
