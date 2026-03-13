using System;
using System.Collections.Generic;
using NUnit.Framework;
using McpUnity.Server;
using McpUnity.Protocol;

namespace McpUnity.Tests
{
    /// <summary>
    /// Edit Mode tests for McpResourceRegistry — resource registration, lookup, and reading.
    /// </summary>
    public class McpResourceRegistryTests
    {
        private McpResourceRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new McpResourceRegistry();
        }

        // ── Helper ─────────────────────────────────────────────────────────

        private McpResourceDefinition MakeDef(string uri, string name = "Test", string mime = "text/plain")
        {
            return new McpResourceDefinition
            {
                uri = uri,
                name = name,
                description = "Test resource",
                mimeType = mime
            };
        }

        private Func<McpResourceContent> MakeHandler(string text = "content")
        {
            return () => new McpResourceContent
            {
                uri = "test",
                mimeType = "text/plain",
                text = text
            };
        }

        // ── RegisterResource ───────────────────────────────────────────────

        [Test]
        public void RegisterResource_ValidResource_IncreasesCount()
        {
            _registry.RegisterResource(MakeDef("unity://test"), MakeHandler());
            Assert.AreEqual(1, _registry.Count);
        }

        [Test]
        public void RegisterResource_MultipleResources_AllRegistered()
        {
            _registry.RegisterResource(MakeDef("unity://a"), MakeHandler());
            _registry.RegisterResource(MakeDef("unity://b"), MakeHandler());
            _registry.RegisterResource(MakeDef("unity://c"), MakeHandler());
            Assert.AreEqual(3, _registry.Count);
        }

        [Test]
        public void RegisterResource_NullDefinition_DoesNotRegister()
        {
            _registry.RegisterResource(null, MakeHandler());
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void RegisterResource_EmptyUri_DoesNotRegister()
        {
            _registry.RegisterResource(MakeDef(""), MakeHandler());
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void RegisterResource_NullHandler_DoesNotRegister()
        {
            _registry.RegisterResource(MakeDef("unity://test"), null);
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void RegisterResource_DuplicateUri_OverwritesPrevious()
        {
            _registry.RegisterResource(MakeDef("unity://test"), MakeHandler("first"));
            _registry.RegisterResource(MakeDef("unity://test"), MakeHandler("second"));
            Assert.AreEqual(1, _registry.Count);

            var result = _registry.ReadResource("unity://test");
            Assert.AreEqual("second", result.contents[0].text);
        }

        // ── UnregisterResource ─────────────────────────────────────────────

        [Test]
        public void UnregisterResource_ExistingUri_ReturnsTrue()
        {
            _registry.RegisterResource(MakeDef("unity://test"), MakeHandler());
            Assert.IsTrue(_registry.UnregisterResource("unity://test"));
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void UnregisterResource_NonExistentUri_ReturnsFalse()
        {
            Assert.IsFalse(_registry.UnregisterResource("unity://missing"));
        }

        [Test]
        public void UnregisterResource_NullUri_ReturnsFalse()
        {
            Assert.IsFalse(_registry.UnregisterResource(null));
        }

        [Test]
        public void UnregisterResource_EmptyUri_ReturnsFalse()
        {
            Assert.IsFalse(_registry.UnregisterResource(""));
        }

        [Test]
        public void UnregisterResource_ThenRead_ThrowsKeyNotFound()
        {
            _registry.RegisterResource(MakeDef("unity://test"), MakeHandler());
            _registry.UnregisterResource("unity://test");
            Assert.Throws<KeyNotFoundException>(() => _registry.ReadResource("unity://test"));
        }

        // ── HasResource ────────────────────────────────────────────────────

        [Test]
        public void HasResource_ExistingUri_ReturnsTrue()
        {
            _registry.RegisterResource(MakeDef("unity://test"), MakeHandler());
            Assert.IsTrue(_registry.HasResource("unity://test"));
        }

        [Test]
        public void HasResource_NonExistentUri_ReturnsFalse()
        {
            Assert.IsFalse(_registry.HasResource("unity://missing"));
        }

        [Test]
        public void HasResource_NullUri_ReturnsFalse()
        {
            Assert.IsFalse(_registry.HasResource(null));
        }

        [Test]
        public void HasResource_EmptyUri_ReturnsFalse()
        {
            Assert.IsFalse(_registry.HasResource(""));
        }

        // ── GetAllResources ────────────────────────────────────────────────

        [Test]
        public void GetAllResources_Empty_ReturnsEmptyList()
        {
            var resources = _registry.GetAllResources();
            Assert.IsNotNull(resources);
            Assert.AreEqual(0, resources.Count);
        }

        [Test]
        public void GetAllResources_ReturnsAllRegistered()
        {
            _registry.RegisterResource(MakeDef("unity://a", "A"), MakeHandler());
            _registry.RegisterResource(MakeDef("unity://b", "B"), MakeHandler());

            var resources = _registry.GetAllResources();
            Assert.AreEqual(2, resources.Count);
        }

        [Test]
        public void GetAllResources_ReturnsCopy_NotReference()
        {
            _registry.RegisterResource(MakeDef("unity://a"), MakeHandler());
            var list1 = _registry.GetAllResources();
            var list2 = _registry.GetAllResources();
            Assert.AreNotSame(list1, list2);
        }

        // ── GetResource ────────────────────────────────────────────────────

        [Test]
        public void GetResource_ExistingUri_ReturnsDefinition()
        {
            _registry.RegisterResource(MakeDef("unity://test", "MyResource"), MakeHandler());
            var def = _registry.GetResource("unity://test");
            Assert.IsNotNull(def);
            Assert.AreEqual("MyResource", def.name);
        }

        [Test]
        public void GetResource_NonExistentUri_ReturnsNull()
        {
            var def = _registry.GetResource("unity://missing");
            Assert.IsNull(def);
        }

        [Test]
        public void GetResource_NullUri_ReturnsNull()
        {
            Assert.IsNull(_registry.GetResource(null));
        }

        [Test]
        public void GetResource_EmptyUri_ReturnsNull()
        {
            Assert.IsNull(_registry.GetResource(""));
        }

        // ── ReadResource ───────────────────────────────────────────────────

        [Test]
        public void ReadResource_ExistingUri_ReturnsContent()
        {
            _registry.RegisterResource(MakeDef("unity://test"), MakeHandler("hello"));
            var result = _registry.ReadResource("unity://test");
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.contents.Count);
            Assert.AreEqual("hello", result.contents[0].text);
        }

        [Test]
        public void ReadResource_NullUri_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _registry.ReadResource(null));
        }

        [Test]
        public void ReadResource_EmptyUri_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _registry.ReadResource(""));
        }

        [Test]
        public void ReadResource_NonExistentUri_ThrowsKeyNotFound()
        {
            Assert.Throws<KeyNotFoundException>(() => _registry.ReadResource("unity://missing"));
        }

        [Test]
        public void ReadResource_HandlerThrows_WrapsException()
        {
            _registry.RegisterResource(
                MakeDef("unity://broken"),
                () => throw new InvalidOperationException("boom"));

            Assert.Throws<Exception>(() => _registry.ReadResource("unity://broken"));
        }

        // ── Wildcard / pattern matching ────────────────────────────────────

        [Test]
        public void ReadResource_WildcardPattern_MatchesPrefixUri()
        {
            _registry.RegisterResource(MakeDef("unity://asset/*"), MakeHandler("asset content"));
            var result = _registry.ReadResource("unity://asset/Scripts/MyScript.cs");
            Assert.IsNotNull(result);
            Assert.AreEqual("asset content", result.contents[0].text);
        }

        [Test]
        public void ReadResource_WildcardPattern_DoesNotMatchDifferentPrefix()
        {
            _registry.RegisterResource(MakeDef("unity://asset/*"), MakeHandler());
            Assert.Throws<KeyNotFoundException>(() => _registry.ReadResource("unity://scene/hierarchy"));
        }

        [Test]
        public void ReadResource_ExactMatchTakesPriority_OverWildcard()
        {
            _registry.RegisterResource(MakeDef("unity://asset/*"), MakeHandler("wildcard"));
            _registry.RegisterResource(MakeDef("unity://asset/special"), MakeHandler("exact"));

            var result = _registry.ReadResource("unity://asset/special");
            Assert.AreEqual("exact", result.contents[0].text);
        }

        // ── Clear ──────────────────────────────────────────────────────────

        [Test]
        public void Clear_RemovesAllResources()
        {
            _registry.RegisterResource(MakeDef("unity://a"), MakeHandler());
            _registry.RegisterResource(MakeDef("unity://b"), MakeHandler());
            _registry.Clear();
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void Clear_ThenHasResource_ReturnsFalse()
        {
            _registry.RegisterResource(MakeDef("unity://test"), MakeHandler());
            _registry.Clear();
            Assert.IsFalse(_registry.HasResource("unity://test"));
        }

        // ── Count ──────────────────────────────────────────────────────────

        [Test]
        public void Count_InitiallyZero()
        {
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void Count_AfterRegisterAndUnregister_IsCorrect()
        {
            _registry.RegisterResource(MakeDef("unity://a"), MakeHandler());
            _registry.RegisterResource(MakeDef("unity://b"), MakeHandler());
            Assert.AreEqual(2, _registry.Count);

            _registry.UnregisterResource("unity://a");
            Assert.AreEqual(1, _registry.Count);
        }
    }
}
