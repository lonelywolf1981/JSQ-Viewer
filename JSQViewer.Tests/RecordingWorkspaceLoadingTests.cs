using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Application.Workspace;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Application.Workspace.UseCases;
using JSQViewer.Core;
using JSQViewer.Infrastructure.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingWorkspaceLoadingTests
    {
        [TestMethod]
        public void Execute_RecordingSource_InvokesReaderWithRecordingId()
        {
            var reader = new FakeRecordingDataReader();
            LoadWorkspaceDataUseCase useCase = CreateUseCase(reader);

            useCase.Execute(new WorkspaceLoadRequest("jsqdb://recording/recording-42"));

            Assert.AreEqual("recording-42", reader.LastRecordingId);
        }

        [TestMethod]
        public void Execute_RecordingSource_PreservesNormalizedSourceString()
        {
            var reader = new FakeRecordingDataReader();
            LoadWorkspaceDataUseCase useCase = CreateUseCase(reader);

            WorkspaceLoadResult result = useCase.Execute(
                new WorkspaceLoadRequest("  \"jsqdb://recording/recording-42\"  "));

            Assert.AreEqual("jsqdb://recording/recording-42", result.NormalizedFolderSpec);
            Assert.AreEqual("jsqdb://recording/recording-42", result.Folders[0]);
            Assert.AreEqual("jsqdb://recording/recording-42", result.Data.Root);
        }

        [TestMethod]
        public void Execute_RecordingSource_WithoutReaderThrowsInvalidOperationException()
        {
            LoadWorkspaceDataUseCase useCase = CreateUseCase(null);

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => useCase.Execute(new WorkspaceLoadRequest("jsqdb://recording/recording-42")));

            Assert.AreEqual("Recording data reader is not configured.", exception.Message);
        }

        [TestMethod]
        public void IsValidSpec_AcceptsRecordingSourceAndRejectsEmptyOrMissingPath()
        {
            var service = new WorkspaceLoadOrchestrationService(
                new WorkspaceFolderSpecParser(),
                new EmptyFileSystem());

            Assert.IsTrue(service.IsValidSpec("jsqdb://recording/recording-42"));
            Assert.IsFalse(service.IsValidSpec(string.Empty));
            Assert.IsFalse(service.IsValidSpec(@"C:\missing"));
        }

        [TestMethod]
        public void ResolveSelectedFolderSource_RecordingSource_PreservesTrimmedUri()
        {
            var service = new WorkspaceLoadOrchestrationService(
                new WorkspaceFolderSpecParser(),
                new EmptyFileSystem());

            string result = service.ResolveSelectedFolderSource(
                "  \"jsqdb://recording/recording-42\"  ");

            Assert.AreEqual("jsqdb://recording/recording-42", result);
        }

        [TestMethod]
        public void PostgresReader_RowsSql_RequiresVisibleChannelConfigurationForRecordingPost()
        {
            FieldInfo field = typeof(PostgresRecordingDataSourceReader).GetField(
                "RowsSql",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field);
            string sql = (string)field.GetRawConstantValue();
            StringAssert.Contains(sql, "EXISTS");
            StringAssert.Contains(sql, "channel_config cc");
            StringAssert.Contains(sql, "cc.post_id = r.post_id");
            StringAssert.Contains(sql, "cc.channel_id = a.channel_id");
            StringAssert.Contains(sql, "NOT cc.is_hidden");
        }

        [TestMethod]
        public void PostgresReader_ReadLocalDateTime_ConvertsUtcDateTimeToLocalTime()
        {
            DateTime utc = new DateTime(2026, 8, 6, 10, 15, 0, DateTimeKind.Utc);

            DateTime actual = InvokeReadLocalDateTime(utc);

            Assert.AreEqual(utc.ToLocalTime(), actual);
            Assert.AreEqual(DateTimeKind.Local, actual.Kind);
        }

        [TestMethod]
        public void PostgresReader_ReadLocalDateTime_ConvertsDateTimeOffsetToLocalTime()
        {
            var moment = new DateTimeOffset(2026, 8, 6, 10, 15, 0, TimeSpan.FromHours(-3));

            DateTime actual = InvokeReadLocalDateTime(moment);

            Assert.AreEqual(moment.LocalDateTime, actual);
            Assert.AreEqual(DateTimeKind.Local, actual.Kind);
        }

        private static LoadWorkspaceDataUseCase CreateUseCase(IRecordingDataReader recordingDataReader)
        {
            return new LoadWorkspaceDataUseCase(
                new WorkspaceFolderSpecParser(),
                new ThrowingRootLocator(),
                new ThrowingMetadataReader(),
                new ThrowingCanaliDefinitionReader(),
                new ThrowingTestDataSourceReader(),
                new MergeLoadedSourcesUseCase(),
                null,
                recordingDataReader);
        }

        private static DateTime InvokeReadLocalDateTime(object value)
        {
            MethodInfo method = typeof(PostgresRecordingDataSourceReader).GetMethod(
                "ReadLocalDateTime",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);
            return (DateTime)method.Invoke(null, new[] { value });
        }

        private sealed class FakeRecordingDataReader : IRecordingDataReader
        {
            public string LastRecordingId { get; private set; }

            public TestData ReadRecording(string recordingId)
            {
                LastRecordingId = recordingId;
                return new TestData { Root = RecordingSourceRef.Build(recordingId) };
            }

            public TestData AppendNewWindows(TestData existing, string recordingId)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ThrowingRootLocator : ITestRootLocator
        {
            public string FindRoot(string folder)
            {
                throw new AssertFailedException("A recording source must not use the filesystem root locator.");
            }
        }

        private sealed class ThrowingMetadataReader : ITestMetadataReader
        {
            public Dictionary<string, string> Read(string root)
            {
                throw new AssertFailedException("A recording source must not read filesystem metadata.");
            }
        }

        private sealed class ThrowingCanaliDefinitionReader : ICanaliDefinitionReader
        {
            public Dictionary<string, ChannelInfo> Read(string root)
            {
                throw new AssertFailedException("A recording source must not read filesystem channels.");
            }
        }

        private sealed class ThrowingTestDataSourceReader : ITestDataSourceReader
        {
            public TestData Read(
                string root,
                Dictionary<string, ChannelInfo> channels,
                Dictionary<string, string> metadata)
            {
                throw new AssertFailedException("A recording source must not use the filesystem data reader.");
            }
        }

        private sealed class EmptyFileSystem : IFileSystem
        {
            public bool FileExists(string path) => false;

            public bool DirectoryExists(string path) => false;

            public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => new string[0];

            public DateTime GetLastWriteTime(string path) => DateTime.MinValue;

            public void WriteAllBytes(string path, byte[] contents) { }

            public void CreateDirectory(string path) { }

            public void AppendAllText(string path, string contents, Encoding encoding) { }
        }
    }
}
