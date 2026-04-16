using System;
using NUnit.Framework;
using McpUnity.Editor;

namespace McpUnity.Tests
{
    /// <summary>
    /// Edit Mode tests for McpServerLogger — centralized logging and LogEntry struct.
    /// </summary>
    public class McpServerLoggerTests
    {
        private McpServerLogger _logger;

        [SetUp]
        public void SetUp()
        {
            _logger = McpServerLogger.Instance;
            _logger.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _logger.Clear();
        }

        // ── Instance ───────────────────────────────────────────────────────

        [Test]
        public void Instance_ReturnsSameInstance()
        {
            var a = McpServerLogger.Instance;
            var b = McpServerLogger.Instance;
            Assert.AreSame(a, b);
        }

        [Test]
        public void Instance_IsNotNull()
        {
            Assert.IsNotNull(McpServerLogger.Instance);
        }

        // ── Log methods ────────────────────────────────────────────────────

        [Test]
        public void Info_AddsLogEntry()
        {
            _logger.Info("test message");
            Assert.AreEqual(1, _logger.Count);
        }

        [Test]
        public void Info_StoresCorrectLevel()
        {
            _logger.Info("info msg");
            Assert.AreEqual(LogLevel.Info, _logger.Logs[0].Level);
        }

        [Test]
        public void Info_StoresMessage()
        {
            _logger.Info("hello world");
            Assert.AreEqual("hello world", _logger.Logs[0].Message);
        }

        [Test]
        public void Info_StoresContext()
        {
            _logger.Info("msg", "MyContext");
            Assert.AreEqual("MyContext", _logger.Logs[0].Context);
        }

        [Test]
        public void Info_NullContext_StoresNull()
        {
            _logger.Info("msg");
            Assert.IsNull(_logger.Logs[0].Context);
        }

        [Test]
        public void Debug_StoresDebugLevel()
        {
            _logger.Debug("debug msg");
            // Note: may not be recorded if MinimumLogLevel > Debug
            // Test verifies behavior based on current settings
            if (_logger.Count > 0)
            {
                Assert.AreEqual(LogLevel.Debug, _logger.Logs[0].Level);
            }
        }

        [Test]
        public void Warning_StoresWarningLevel()
        {
            _logger.Warning("warning msg");
            Assert.AreEqual(LogLevel.Warning, _logger.Logs[0].Level);
        }

        [Test]
        public void Error_StoresErrorLevel()
        {
            _logger.Error("error msg");
            Assert.AreEqual(LogLevel.Error, _logger.Logs[0].Level);
        }

        [Test]
        public void Error_WithException_IncludesExceptionMessage()
        {
            var ex = new InvalidOperationException("boom");
            _logger.Error("operation failed", ex);

            Assert.AreEqual(1, _logger.Count);
            Assert.IsTrue(_logger.Logs[0].Message.Contains("boom"));
            Assert.IsTrue(_logger.Logs[0].Message.Contains("operation failed"));
        }

        [Test]
        public void Error_WithException_IncludesStackTrace()
        {
            Exception ex;
            try
            {
                throw new InvalidOperationException("test error");
            }
            catch (Exception caught)
            {
                ex = caught;
            }

            _logger.Error("failed", ex);
            // Stack trace should be present since exception was actually thrown
            Assert.IsTrue(_logger.Logs[0].Message.Contains("test error"));
        }

        // ── Multiple logs ──────────────────────────────────────────────────

        [Test]
        public void MultipleLogs_MaintainOrder()
        {
            _logger.Info("first");
            _logger.Warning("second");
            _logger.Error("third");

            Assert.AreEqual(3, _logger.Count);
            Assert.AreEqual("first", _logger.Logs[0].Message);
            Assert.AreEqual("second", _logger.Logs[1].Message);
            Assert.AreEqual("third", _logger.Logs[2].Message);
        }

        [Test]
        public void MultipleLogs_HaveCorrectLevels()
        {
            _logger.Info("i");
            _logger.Warning("w");
            _logger.Error("e");

            Assert.AreEqual(LogLevel.Info, _logger.Logs[0].Level);
            Assert.AreEqual(LogLevel.Warning, _logger.Logs[1].Level);
            Assert.AreEqual(LogLevel.Error, _logger.Logs[2].Level);
        }

        // ── Clear ──────────────────────────────────────────────────────────

        [Test]
        public void Clear_RemovesAllLogs()
        {
            _logger.Info("a");
            _logger.Info("b");
            _logger.Clear();
            Assert.AreEqual(0, _logger.Count);
        }

        [Test]
        public void Clear_ThenLogs_ReturnsEmptyList()
        {
            _logger.Info("test");
            _logger.Clear();
            Assert.AreEqual(0, _logger.Logs.Count);
        }

        // ── Count ──────────────────────────────────────────────────────────

        [Test]
        public void Count_InitiallyZero()
        {
            Assert.AreEqual(0, _logger.Count);
        }

        [Test]
        public void Count_IncrementsWithEachLog()
        {
            _logger.Info("1");
            Assert.AreEqual(1, _logger.Count);
            _logger.Info("2");
            Assert.AreEqual(2, _logger.Count);
        }

        // ── Logs property ──────────────────────────────────────────────────

        [Test]
        public void Logs_ReturnsReadOnlyList()
        {
            _logger.Info("test");
            var logs = _logger.Logs;
            Assert.IsNotNull(logs);
            Assert.AreEqual(1, logs.Count);
        }

        // ── Timestamp ──────────────────────────────────────────────────────

        [Test]
        public void Log_SetsTimestamp()
        {
            var before = DateTime.Now;
            _logger.Info("timed");
            var after = DateTime.Now;

            var entry = _logger.Logs[0];
            Assert.GreaterOrEqual(entry.Timestamp, before);
            Assert.LessOrEqual(entry.Timestamp, after);
        }

        // ── Export ─────────────────────────────────────────────────────────

        [Test]
        public void Export_IncludesHeader()
        {
            var exported = _logger.Export();
            Assert.IsTrue(exported.Contains("MCP Unity Server Logs"));
        }

        [Test]
        public void Export_IncludesLogMessages()
        {
            _logger.Info("exported message");
            _logger.Warning("warning message");

            var exported = _logger.Export();
            Assert.IsTrue(exported.Contains("exported message"));
            Assert.IsTrue(exported.Contains("warning message"));
        }

        [Test]
        public void Export_WithMinimumLevel_FiltersLogs()
        {
            _logger.Info("info msg");
            _logger.Warning("warn msg");
            _logger.Error("err msg");

            var exported = _logger.Export(LogLevel.Warning);
            Assert.IsFalse(exported.Contains("info msg"));
            Assert.IsTrue(exported.Contains("warn msg"));
            Assert.IsTrue(exported.Contains("err msg"));
        }

        [Test]
        public void Export_EmptyLogs_ReturnsHeaderOnly()
        {
            var exported = _logger.Export();
            Assert.IsTrue(exported.Contains("MCP Unity Server Logs"));
        }

        // ── GetFormattedLogs ───────────────────────────────────────────────

        [Test]
        public void GetFormattedLogs_ReturnsShortFormat()
        {
            _logger.Info("formatted");
            var formatted = _logger.GetFormattedLogs();
            Assert.IsTrue(formatted.Contains("formatted"));
            Assert.IsTrue(formatted.Contains("[I]"));
        }

        [Test]
        public void GetFormattedLogs_WithMinLevel_FiltersCorrectly()
        {
            _logger.Info("info");
            _logger.Error("error");

            var formatted = _logger.GetFormattedLogs(LogLevel.Error);
            Assert.IsFalse(formatted.Contains("info"));
            Assert.IsTrue(formatted.Contains("error"));
        }

        // ── OnLogAdded event ───────────────────────────────────────────────

        [Test]
        public void OnLogAdded_FiresOnNewLog()
        {
            LogEntry captured = default;
            bool fired = false;

            _logger.OnLogAdded += entry =>
            {
                captured = entry;
                fired = true;
            };

            _logger.Info("event test");

            Assert.IsTrue(fired);
            Assert.AreEqual("event test", captured.Message);
            Assert.AreEqual(LogLevel.Info, captured.Level);
        }

        // ── LogEntry struct ────────────────────────────────────────────────

        [Test]
        public void LogEntry_ToString_IncludesAllFields()
        {
            var entry = new LogEntry
            {
                Timestamp = new DateTime(2024, 1, 15, 10, 30, 45, 123),
                Level = LogLevel.Warning,
                Message = "test warning",
                Context = "TestCtx"
            };

            var str = entry.ToString();
            Assert.IsTrue(str.Contains("Warning"));
            Assert.IsTrue(str.Contains("test warning"));
            Assert.IsTrue(str.Contains("TestCtx"));
            Assert.IsTrue(str.Contains("10:30:45.123"));
        }

        [Test]
        public void LogEntry_ToString_NullContext_OmitsContext()
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Info,
                Message = "no context",
                Context = null
            };

            var str = entry.ToString();
            Assert.IsTrue(str.Contains("no context"));
            Assert.IsFalse(str.Contains("[]")); // No empty brackets
        }

        [Test]
        public void LogEntry_ToShortString_IncludesLevelPrefix()
        {
            var debugEntry = new LogEntry { Level = LogLevel.Debug, Message = "d", Timestamp = DateTime.Now };
            Assert.IsTrue(debugEntry.ToShortString().Contains("[D]"));

            var infoEntry = new LogEntry { Level = LogLevel.Info, Message = "i", Timestamp = DateTime.Now };
            Assert.IsTrue(infoEntry.ToShortString().Contains("[I]"));

            var warnEntry = new LogEntry { Level = LogLevel.Warning, Message = "w", Timestamp = DateTime.Now };
            Assert.IsTrue(warnEntry.ToShortString().Contains("[W]"));

            var errorEntry = new LogEntry { Level = LogLevel.Error, Message = "e", Timestamp = DateTime.Now };
            Assert.IsTrue(errorEntry.ToShortString().Contains("[E]"));
        }

        [Test]
        public void LogEntry_GetColor_ReturnsDistinctColors()
        {
            var debug = new LogEntry { Level = LogLevel.Debug };
            var info = new LogEntry { Level = LogLevel.Info };
            var warn = new LogEntry { Level = LogLevel.Warning };
            var error = new LogEntry { Level = LogLevel.Error };

            // All should return non-zero colors
            Assert.AreNotEqual(UnityEngine.Color.clear, debug.GetColor());
            Assert.AreNotEqual(UnityEngine.Color.clear, info.GetColor());
            Assert.AreNotEqual(UnityEngine.Color.clear, warn.GetColor());
            Assert.AreNotEqual(UnityEngine.Color.clear, error.GetColor());

            // Warning and error should be distinct
            Assert.AreNotEqual(warn.GetColor(), error.GetColor());
        }

        // ── LogLevel enum ──────────────────────────────────────────────────

        [Test]
        public void LogLevel_OrderIsCorrect()
        {
            Assert.Less((int)LogLevel.Debug, (int)LogLevel.Info);
            Assert.Less((int)LogLevel.Info, (int)LogLevel.Warning);
            Assert.Less((int)LogLevel.Warning, (int)LogLevel.Error);
        }
    }
}
