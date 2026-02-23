using System;
using NUnit.Framework;
using McpUnity.Utils;

namespace McpUnity.Tests
{
    /// <summary>
    /// Edit Mode tests for PathValidator — security-critical path sanitization and validation.
    /// </summary>
    public class PathValidatorTests
    {
        // ── SanitizePath — basic validation ──────────────────────────────────

        [Test]
        public void SanitizePath_NullPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PathValidator.SanitizePath(null));
        }

        [Test]
        public void SanitizePath_EmptyPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PathValidator.SanitizePath(""));
        }

        [Test]
        public void SanitizePath_ValidAssetsPath_ReturnsSamePath()
        {
            var result = PathValidator.SanitizePath("Assets/Scripts/Player.cs");
            Assert.AreEqual("Assets/Scripts/Player.cs", result);
        }

        [Test]
        public void SanitizePath_NormalizesBackslashes()
        {
            var result = PathValidator.SanitizePath("Assets\\Materials\\Wood.mat");
            Assert.AreEqual("Assets/Materials/Wood.mat", result);
        }

        // ── SanitizePath — path traversal blocking ──────────────────────────

        [Test]
        public void SanitizePath_PathTraversal_DoubleDot_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                PathValidator.SanitizePath("Assets/../../../etc/passwd"));
        }

        [Test]
        public void SanitizePath_PathTraversal_MidPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                PathValidator.SanitizePath("Assets/Scripts/../../secrets.txt"));
        }

        [Test]
        public void SanitizePath_PathTraversal_EncodedDots_ThrowsArgumentException()
        {
            // Even with extra text around ".."
            Assert.Throws<ArgumentException>(() =>
                PathValidator.SanitizePath("Assets/..hidden/file.txt"));
        }

        // ── SanitizePath — prefix validation ────────────────────────────────

        [Test]
        public void SanitizePath_MissingAssetsPrefix_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                PathValidator.SanitizePath("Scripts/Player.cs"));
        }

        [Test]
        public void SanitizePath_AbsolutePathOutsideProject_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                PathValidator.SanitizePath("/usr/local/bin/evil"));
        }

        [Test]
        public void SanitizePath_CustomPrefix_Accepted()
        {
            var result = PathValidator.SanitizePath("Packages/com.unity.render-pipelines/shaders/test.shader", "Packages/");
            Assert.AreEqual("Packages/com.unity.render-pipelines/shaders/test.shader", result);
        }

        [Test]
        public void SanitizePath_CustomPrefix_WrongPrefix_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                PathValidator.SanitizePath("Assets/file.txt", "Packages/"));
        }

        [Test]
        public void SanitizePath_CaseInsensitivePrefix()
        {
            // "assets/" should match "Assets/" prefix (case-insensitive)
            var result = PathValidator.SanitizePath("assets/Scripts/test.cs");
            Assert.AreEqual("assets/Scripts/test.cs", result);
        }

        // ── TryValidatePath ─────────────────────────────────────────────────

        [Test]
        public void TryValidatePath_ValidPath_ReturnsTrue()
        {
            bool ok = PathValidator.TryValidatePath("Assets/test.txt", out var sanitized, out var error);
            Assert.IsTrue(ok);
            Assert.AreEqual("Assets/test.txt", sanitized);
            Assert.IsNull(error);
        }

        [Test]
        public void TryValidatePath_InvalidPath_ReturnsFalseWithMessage()
        {
            bool ok = PathValidator.TryValidatePath("../evil.txt", out var sanitized, out var error);
            Assert.IsFalse(ok);
            Assert.IsNull(sanitized);
            Assert.IsNotNull(error);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryValidatePath_NullPath_ReturnsFalse()
        {
            bool ok = PathValidator.TryValidatePath(null, out _, out var error);
            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        // ── HasExtension ────────────────────────────────────────────────────

        [Test]
        public void HasExtension_NullPath_ReturnsFalse()
        {
            Assert.IsFalse(PathValidator.HasExtension(null, ".cs"));
        }

        [Test]
        public void HasExtension_EmptyPath_ReturnsFalse()
        {
            Assert.IsFalse(PathValidator.HasExtension("", ".cs"));
        }

        [Test]
        public void HasExtension_CorrectExtension_ReturnsTrue()
        {
            Assert.IsTrue(PathValidator.HasExtension("Assets/Scripts/Player.cs", ".cs"));
        }

        [Test]
        public void HasExtension_WrongExtension_ReturnsFalse()
        {
            Assert.IsFalse(PathValidator.HasExtension("Assets/Scripts/Player.cs", ".js"));
        }

        [Test]
        public void HasExtension_CaseInsensitive()
        {
            Assert.IsTrue(PathValidator.HasExtension("Assets/Scenes/Main.Unity", ".unity"));
        }

        // ── EnsureDirectorySeparator ────────────────────────────────────────

        [Test]
        public void EnsureDirectorySeparator_NullPath_ReturnsNull()
        {
            Assert.IsNull(PathValidator.EnsureDirectorySeparator(null));
        }

        [Test]
        public void EnsureDirectorySeparator_EmptyPath_ReturnsEmpty()
        {
            Assert.AreEqual("", PathValidator.EnsureDirectorySeparator(""));
        }

        [Test]
        public void EnsureDirectorySeparator_AlreadyHasSlash_NoChange()
        {
            Assert.AreEqual("Assets/Scripts/", PathValidator.EnsureDirectorySeparator("Assets/Scripts/"));
        }

        [Test]
        public void EnsureDirectorySeparator_MissingSlash_AddsSlash()
        {
            Assert.AreEqual("Assets/Scripts/", PathValidator.EnsureDirectorySeparator("Assets/Scripts"));
        }

        // ── GetDirectory ────────────────────────────────────────────────────

        [Test]
        public void GetDirectory_NullPath_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, PathValidator.GetDirectory(null));
        }

        [Test]
        public void GetDirectory_NoSeparator_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, PathValidator.GetDirectory("file.txt"));
        }

        [Test]
        public void GetDirectory_WithSeparator_ReturnsDirectory()
        {
            Assert.AreEqual("Assets/Scripts", PathValidator.GetDirectory("Assets/Scripts/Player.cs"));
        }

        // ── CombinePaths ────────────────────────────────────────────────────

        [Test]
        public void CombinePaths_NormalCombination()
        {
            Assert.AreEqual("Assets/Scripts/Player.cs", PathValidator.CombinePaths("Assets/Scripts", "Player.cs"));
        }

        [Test]
        public void CombinePaths_RelativeWithLeadingSlash_StripsSlash()
        {
            Assert.AreEqual("Assets/Scripts/Player.cs", PathValidator.CombinePaths("Assets/Scripts", "/Player.cs"));
        }

        [Test]
        public void CombinePaths_BaseAlreadyHasSlash()
        {
            Assert.AreEqual("Assets/Scripts/Player.cs", PathValidator.CombinePaths("Assets/Scripts/", "Player.cs"));
        }
    }
}
