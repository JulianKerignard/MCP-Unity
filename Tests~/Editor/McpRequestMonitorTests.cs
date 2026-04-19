using System;
using NUnit.Framework;
using McpUnity.Editor;

namespace McpUnity.Tests
{
    /// <summary>
    /// Edit Mode tests for McpRequestMonitor — lightweight request tracking and metrics.
    /// </summary>
    public class McpRequestMonitorTests
    {
        [SetUp]
        public void SetUp()
        {
            McpRequestMonitor.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            McpRequestMonitor.Clear();
        }

        // ── Record ─────────────────────────────────────────────────────────

        [Test]
        public void Record_SingleRequest_IncreasesCount()
        {
            McpRequestMonitor.Record("tools/list", null, 5.0, true);
            Assert.AreEqual(1, McpRequestMonitor.Count);
        }

        [Test]
        public void Record_MultipleRequests_CountIsCorrect()
        {
            McpRequestMonitor.Record("tools/list", null, 1.0, true);
            McpRequestMonitor.Record("tools/call", "unity_test", 2.0, true);
            McpRequestMonitor.Record("resources/read", null, 3.0, true);
            Assert.AreEqual(3, McpRequestMonitor.Count);
        }

        [Test]
        public void Record_SuccessRequest_IncreasesTotalRequests()
        {
            McpRequestMonitor.Record("tools/list", null, 1.0, true);
            Assert.AreEqual(1, McpRequestMonitor.TotalRequests);
            Assert.AreEqual(0, McpRequestMonitor.TotalErrors);
        }

        [Test]
        public void Record_ErrorRequest_IncreasesTotalErrors()
        {
            McpRequestMonitor.Record("tools/call", "bad_tool", 1.0, false, "Tool not found");
            Assert.AreEqual(1, McpRequestMonitor.TotalRequests);
            Assert.AreEqual(1, McpRequestMonitor.TotalErrors);
        }

        [Test]
        public void Record_MixedRequests_CountersAreCorrect()
        {
            McpRequestMonitor.Record("tools/list", null, 1.0, true);
            McpRequestMonitor.Record("tools/call", "tool1", 2.0, true);
            McpRequestMonitor.Record("tools/call", "bad", 3.0, false, "error");
            McpRequestMonitor.Record("tools/call", "tool2", 4.0, true);
            McpRequestMonitor.Record("tools/call", "bad2", 5.0, false, "error");

            Assert.AreEqual(5, McpRequestMonitor.TotalRequests);
            Assert.AreEqual(2, McpRequestMonitor.TotalErrors);
        }

        // ── Entry data ────────────────────────────────────────────────────

        [Test]
        public void Record_StoresCorrectEntryData()
        {
            McpRequestMonitor.Record("tools/call", "unity_test_tool", 42.5, true);

            var entries = McpRequestMonitor.Entries;
            Assert.AreEqual(1, entries.Count);

            var entry = entries[0];
            Assert.AreEqual("tools/call", entry.Method);
            Assert.AreEqual("unity_test_tool", entry.ToolName);
            Assert.AreEqual(42.5, entry.DurationMs, 0.001);
            Assert.IsTrue(entry.Success);
            Assert.IsNull(entry.Error);
        }

        [Test]
        public void Record_ErrorEntry_StoresErrorMessage()
        {
            McpRequestMonitor.Record("tools/call", "broken", 10.0, false, "Something failed");

            var entry = McpRequestMonitor.Entries[0];
            Assert.IsFalse(entry.Success);
            Assert.AreEqual("Something failed", entry.Error);
        }

        [Test]
        public void Record_SetsTimestamp()
        {
            var before = DateTime.Now;
            McpRequestMonitor.Record("tools/list", null, 1.0, true);
            var after = DateTime.Now;

            var entry = McpRequestMonitor.Entries[0];
            Assert.GreaterOrEqual(entry.Timestamp, before);
            Assert.LessOrEqual(entry.Timestamp, after);
        }

        // ── DisplayName ────────────────────────────────────────────────────

        [Test]
        public void DisplayName_WithToolName_ReturnsToolName()
        {
            McpRequestMonitor.Record("tools/call", "unity_test", 1.0, true);
            Assert.AreEqual("unity_test", McpRequestMonitor.Entries[0].DisplayName);
        }

        [Test]
        public void DisplayName_WithoutToolName_ReturnsMethod()
        {
            McpRequestMonitor.Record("tools/list", null, 1.0, true);
            Assert.AreEqual("tools/list", McpRequestMonitor.Entries[0].DisplayName);
        }

        [Test]
        public void DisplayName_EmptyToolName_ReturnsMethod()
        {
            McpRequestMonitor.Record("resources/read", "", 1.0, true);
            Assert.AreEqual("resources/read", McpRequestMonitor.Entries[0].DisplayName);
        }

        // ── Max entries trimming ───────────────────────────────────────────

        [Test]
        public void Record_ExceedsMaxEntries_TrimsOldest()
        {
            // MaxEntries is 100 (const in the class)
            for (int i = 0; i < 110; i++)
            {
                McpRequestMonitor.Record("tools/call", $"tool_{i}", i, true);
            }

            Assert.AreEqual(100, McpRequestMonitor.Count);

            // Oldest entries should be trimmed — first entry should be tool_10
            var firstEntry = McpRequestMonitor.Entries[0];
            Assert.AreEqual("tool_10", firstEntry.ToolName);
        }

        [Test]
        public void Record_ExceedsMaxEntries_TotalRequestsStillAccurate()
        {
            for (int i = 0; i < 110; i++)
            {
                McpRequestMonitor.Record("tools/call", $"tool_{i}", i, true);
            }

            // TotalRequests should reflect all recorded, not just kept
            Assert.AreEqual(110, McpRequestMonitor.TotalRequests);
        }

        // ── Clear ──────────────────────────────────────────────────────────

        [Test]
        public void Clear_ResetsAllCounters()
        {
            McpRequestMonitor.Record("tools/call", "t1", 1.0, true);
            McpRequestMonitor.Record("tools/call", "t2", 2.0, false, "err");
            McpRequestMonitor.Clear();

            Assert.AreEqual(0, McpRequestMonitor.Count);
            Assert.AreEqual(0, McpRequestMonitor.TotalRequests);
            Assert.AreEqual(0, McpRequestMonitor.TotalErrors);
        }

        [Test]
        public void Clear_ThenEntries_ReturnsEmptyList()
        {
            McpRequestMonitor.Record("tools/list", null, 1.0, true);
            McpRequestMonitor.Clear();
            Assert.AreEqual(0, McpRequestMonitor.Entries.Count);
        }

        // ── Event ──────────────────────────────────────────────────────────

        [Test]
        public void OnRequestRecorded_FiresWhenRecording()
        {
            RequestEntry captured = default;
            bool fired = false;

            // SEC-#437: capture the handler in a named delegate so we can actually unsubscribe.
            // Previously `-= null` removed nothing and the lambda leaked into the static event,
            // contaminating subsequent test runs.
            Action<RequestEntry> handler = entry =>
            {
                captured = entry;
                fired = true;
            };
            McpRequestMonitor.OnRequestRecorded += handler;

            try
            {
                McpRequestMonitor.Record("tools/call", "my_tool", 99.0, true);
                Assert.IsTrue(fired);
                Assert.AreEqual("my_tool", captured.ToolName);
                Assert.AreEqual(99.0, captured.DurationMs, 0.001);
            }
            finally
            {
                McpRequestMonitor.OnRequestRecorded -= handler;
            }
        }

        // ── Entries returns readonly ───────────────────────────────────────

        [Test]
        public void Entries_ReturnsReadOnlyList()
        {
            McpRequestMonitor.Record("tools/list", null, 1.0, true);
            var entries = McpRequestMonitor.Entries;
            Assert.IsNotNull(entries);
            Assert.AreEqual(1, entries.Count);
        }

        // ── Order preservation ─────────────────────────────────────────────

        [Test]
        public void Entries_MaintainsInsertionOrder()
        {
            McpRequestMonitor.Record("tools/call", "first", 1.0, true);
            McpRequestMonitor.Record("tools/call", "second", 2.0, true);
            McpRequestMonitor.Record("tools/call", "third", 3.0, true);

            var entries = McpRequestMonitor.Entries;
            Assert.AreEqual("first", entries[0].ToolName);
            Assert.AreEqual("second", entries[1].ToolName);
            Assert.AreEqual("third", entries[2].ToolName);
        }
    }
}
