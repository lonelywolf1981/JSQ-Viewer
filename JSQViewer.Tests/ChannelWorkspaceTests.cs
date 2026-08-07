using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ChannelWorkspaceModelTests
    {
        [TestMethod]
        public void Load_WhenRefreshingWithoutExplicitSelection_PreservesExistingCheckedCodes()
        {
            object workspace = ChannelWorkspaceTestHarness.CreateWorkspaceModel();
            TestData initial = ChannelWorkspaceTestData.CreateMultiSourceData();

            ChannelWorkspaceTestHarness.Invoke(
                workspace,
                "Load",
                initial,
                new[] { "C:\\srcB::B-01", "C:\\srcA::A-02", "C:\\srcA::A-01" },
                new[] { "C:\\srcA::A-01", "C:\\srcB::B-01" });

            TestData refreshed = ChannelWorkspaceTestData.CreateMultiSourceData(
                new Dictionary<string, string[]>
                {
                    ["C:\\srcA"] = new[] { "C:\\srcA::A-01", "C:\\srcA::A-02", "C:\\srcA::A-03" },
                    ["C:\\srcB"] = new[] { "C:\\srcB::B-01" }
                });

            ChannelWorkspaceTestHarness.Invoke(
                workspace,
                "Load",
                refreshed,
                new[] { "C:\\srcA::A-03", "C:\\srcB::B-01", "C:\\srcA::A-02", "C:\\srcA::A-01" },
                null);

            CollectionAssert.AreEqual(
                new[] { "C:\\srcB::B-01", "C:\\srcA::A-01" },
                ChannelWorkspaceTestHarness.ToCodeList(ChannelWorkspaceTestHarness.Invoke(workspace, "GetSelectedCodes")));

            IList sourceItems = ChannelWorkspaceTestHarness.ToList(ChannelWorkspaceTestHarness.Invoke(
                workspace,
                "BuildSourceList",
                "C:\\srcB",
                string.Empty,
                "User",
                false));

            Assert.AreEqual(1, sourceItems.Count);
            Assert.AreEqual("C:\\srcB::B-01", ChannelWorkspaceTestHarness.GetString(sourceItems[0], "Code"));
            Assert.IsTrue(ChannelWorkspaceTestHarness.GetBoolean(sourceItems[0], "IsSelected"));
        }

        [TestMethod]
        public void BuildSourceList_AppliesSourceProjectionSelectedOnlyAndSelectedFirstSorting()
        {
            object workspace = ChannelWorkspaceTestHarness.CreateWorkspaceModel();

            ChannelWorkspaceTestHarness.Invoke(
                workspace,
                "Load",
                ChannelWorkspaceTestData.CreateMultiSourceData(),
                null,
                new[] { "C:\\srcA::A-01", "C:\\srcB::B-01" });

            IList sorted = ChannelWorkspaceTestHarness.ToList(ChannelWorkspaceTestHarness.Invoke(
                workspace,
                "BuildSourceList",
                "C:\\srcA",
                string.Empty,
                "Selected first",
                false));

            CollectionAssert.AreEqual(
                new[] { "C:\\srcA::A-01", "C:\\srcA::A-02" },
                ChannelWorkspaceTestHarness.ToCodeList(sorted));
            Assert.IsTrue(ChannelWorkspaceTestHarness.GetBoolean(sorted[0], "IsSelected"));
            Assert.IsFalse(ChannelWorkspaceTestHarness.GetBoolean(sorted[1], "IsSelected"));

            IList selectedOnly = ChannelWorkspaceTestHarness.ToList(ChannelWorkspaceTestHarness.Invoke(
                workspace,
                "BuildSourceList",
                "C:\\srcA",
                string.Empty,
                "User",
                true));

            CollectionAssert.AreEqual(
                new[] { "C:\\srcA::A-01" },
                ChannelWorkspaceTestHarness.ToCodeList(selectedOnly));
        }

        [TestMethod]
        public void MoveSourceItem_KeepsSourceLocalOrderAfterGlobalOrderIsApplied()
        {
            object workspace = ChannelWorkspaceTestHarness.CreateWorkspaceModel();

            ChannelWorkspaceTestHarness.Invoke(
                workspace,
                "Load",
                ChannelWorkspaceTestData.CreateMultiSourceData(),
                null,
                null);

            Assert.IsTrue((bool)ChannelWorkspaceTestHarness.Invoke(workspace, "MoveSourceItem", "C:\\srcA", 0, 1));

            CollectionAssert.AreEqual(
                new[] { "C:\\srcA::A-02", "C:\\srcA::A-01", "C:\\srcB::B-01" },
                ChannelWorkspaceTestHarness.ToCodeList(ChannelWorkspaceTestHarness.Invoke(workspace, "GetCurrentOrderForSource", "C:\\srcA")));
            CollectionAssert.AreEqual(
                new[] { "C:\\srcA::A-01", "C:\\srcA::A-02", "C:\\srcB::B-01" },
                ChannelWorkspaceTestHarness.ToCodeList(ChannelWorkspaceTestHarness.Invoke(workspace, "GetCurrentOrder")));

            ChannelWorkspaceTestHarness.Invoke(
                workspace,
                "ApplyOrder",
                new[] { "C:\\srcB::B-01", "C:\\srcA::A-01", "C:\\srcA::A-02" });

            CollectionAssert.AreEqual(
                new[] { "C:\\srcB::B-01", "C:\\srcA::A-01", "C:\\srcA::A-02" },
                ChannelWorkspaceTestHarness.ToCodeList(ChannelWorkspaceTestHarness.Invoke(workspace, "GetCurrentOrder")));
            CollectionAssert.AreEqual(
                new[] { "C:\\srcA::A-02", "C:\\srcA::A-01" },
                ChannelWorkspaceTestHarness.ToCodeList(ChannelWorkspaceTestHarness.Invoke(
                    workspace,
                    "BuildSourceList",
                    "C:\\srcA",
                    string.Empty,
                    "User",
                    false)));
        }
    }

    [TestClass]
    public class ChannelWorkspacePresenterTests
    {
        [TestMethod]
        public void BindData_WhenRootsAreStable_AllowsInPlaceRefreshAndPreservesSelection()
        {
            object presenter = ChannelWorkspaceTestHarness.CreatePresenter();
            TestData initial = ChannelWorkspaceTestData.CreateMultiSourceData();

            ChannelWorkspaceTestHarness.Invoke(presenter, "Initialize", string.Empty, "User", false);

            object initialPlan = ChannelWorkspaceTestHarness.Invoke(
                presenter,
                "BindData",
                initial,
                null,
                new[] { "C:\\srcB::B-01" },
                false);

            Assert.IsFalse(ChannelWorkspaceTestHarness.GetBoolean(initialPlan, "CanRefreshInPlace"));

            TestData refreshed = ChannelWorkspaceTestData.CreateMultiSourceData(
                new Dictionary<string, string[]>
                {
                    ["C:\\srcA"] = new[] { "C:\\srcA::A-01", "C:\\srcA::A-02", "C:\\srcA::A-03" },
                    ["C:\\srcB"] = new[] { "C:\\srcB::B-01" }
                });

            object refreshPlan = ChannelWorkspaceTestHarness.Invoke(
                presenter,
                "BindData",
                refreshed,
                null,
                null,
                true);

            Assert.IsTrue(ChannelWorkspaceTestHarness.GetBoolean(refreshPlan, "CanRefreshInPlace"));
            CollectionAssert.AreEqual(
                new[] { "C:\\srcB::B-01" },
                ChannelWorkspaceTestHarness.ToCodeList(ChannelWorkspaceTestHarness.Invoke(presenter, "GetSelectedCodes")));

            object sourceWindow = ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceWindow", "C:\\srcB");
            IList items = ChannelWorkspaceTestHarness.ToList(ChannelWorkspaceTestHarness.GetValue(sourceWindow, "Items"));
            Assert.AreEqual("C:\\srcB::B-01", ChannelWorkspaceTestHarness.GetString(items[0], "Code"));
            Assert.IsTrue(ChannelWorkspaceTestHarness.GetBoolean(items[0], "IsSelected"));
        }

        [TestMethod]
        public void UpdateSourceWindowOptions_SharesFilterAndSelectedOnlyWhileKeepingPerSourceSort()
        {
            object presenter = ChannelWorkspaceTestHarness.CreatePresenter();

            ChannelWorkspaceTestHarness.Invoke(presenter, "Initialize", string.Empty, "Code", false);
            ChannelWorkspaceTestHarness.Invoke(
                presenter,
                "BindData",
                ChannelWorkspaceTestData.CreateMultiSourceData(),
                null,
                null,
                false);

            ChannelWorkspaceTestHarness.Invoke(
                presenter,
                "UpdateSourceWindowOptions",
                "C:\\srcA",
                "A-0",
                "Label",
                true);

            object mainList = ChannelWorkspaceTestHarness.Invoke(presenter, "GetMainChannelList");
            object sourceA = ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceWindow", "C:\\srcA");
            object sourceB = ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceWindow", "C:\\srcB");

            Assert.AreEqual("A-0", ChannelWorkspaceTestHarness.GetString(mainList, "FilterText"));
            Assert.IsTrue(ChannelWorkspaceTestHarness.GetBoolean(mainList, "SelectedOnly"));
            Assert.AreEqual("Label", ChannelWorkspaceTestHarness.GetString(sourceA, "SortMode"));
            Assert.AreEqual("A-0", ChannelWorkspaceTestHarness.GetString(sourceB, "FilterText"));
            Assert.IsTrue(ChannelWorkspaceTestHarness.GetBoolean(sourceB, "SelectedOnly"));
            Assert.AreEqual("Code", ChannelWorkspaceTestHarness.GetString(sourceB, "SortMode"));
        }

        [TestMethod]
        public void BindData_RestoresPerSourceSelectedOrderKeysIndependently()
        {
            object presenter = ChannelWorkspaceTestHarness.CreatePresenter();
            object layout = ChannelWorkspaceTestHarness.CreateWorkspaceLayoutState(
                "main-order",
                new[] { "C:\\srcB::B-01", "C:\\srcA::A-01", "C:\\srcA::A-02" },
                Tuple.Create("C:\\srcA", "src-a-order", new[] { "C:\\srcA::A-02", "C:\\srcA::A-01" }),
                Tuple.Create("C:\\srcB", "src-b-order", new[] { "C:\\srcB::B-01" }));

            ChannelWorkspaceTestHarness.Invoke(presenter, "Initialize", string.Empty, "User", false);
            ChannelWorkspaceTestHarness.Invoke(
                presenter,
                "BindData",
                ChannelWorkspaceTestData.CreateMultiSourceData(),
                null,
                null,
                false,
                layout);

            Assert.AreEqual("main-order", (string)ChannelWorkspaceTestHarness.Invoke(presenter, "GetMainSelectedOrderKey"));
            Assert.AreEqual("src-a-order", (string)ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceSelectedOrderKey", "C:\\srcA"));
            Assert.AreEqual("src-b-order", (string)ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceSelectedOrderKey", "C:\\srcB"));

            object sourceA = ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceWindow", "C:\\srcA");
            Assert.AreEqual("src-a-order", ChannelWorkspaceTestHarness.GetString(sourceA, "SelectedOrderKey"));
            CollectionAssert.AreEqual(
                new[] { "C:\\srcA::A-02", "C:\\srcA::A-01" },
                ChannelWorkspaceTestHarness.ToCodeList(ChannelWorkspaceTestHarness.GetValue(sourceA, "Items")));
        }

        [TestMethod]
        public void BindData_WhenAddingData_PreservesExistingSourceOrderSelectionsAndLayouts()
        {
            object presenter = ChannelWorkspaceTestHarness.CreatePresenter();
            object initialLayout = ChannelWorkspaceTestHarness.CreateWorkspaceLayoutState(
                "main-order",
                new[] { "C:\\srcB::B-01", "C:\\srcA::A-01", "C:\\srcA::A-02" },
                Tuple.Create("C:\\srcA", "src-a-order", new[] { "C:\\srcA::A-02", "C:\\srcA::A-01" }),
                Tuple.Create("C:\\srcB", "src-b-order", new[] { "C:\\srcB::B-01" }));

            ChannelWorkspaceTestHarness.Invoke(presenter, "Initialize", string.Empty, "User", false);
            ChannelWorkspaceTestHarness.Invoke(
                presenter,
                "BindData",
                ChannelWorkspaceTestData.CreateMultiSourceData(),
                null,
                null,
                false,
                initialLayout);

            TestData expanded = ChannelWorkspaceTestData.CreateMultiSourceData(
                new Dictionary<string, string[]>
                {
                    ["C:\\srcA"] = new[] { "C:\\srcA::A-01", "C:\\srcA::A-02", "C:\\srcA::A-03" },
                    ["C:\\srcB"] = new[] { "C:\\srcB::B-01" },
                    ["C:\\srcC"] = new[] { "C:\\srcC::C-01" }
                });

            ChannelWorkspaceTestHarness.Invoke(
                presenter,
                "BindData",
                expanded,
                null,
                null,
                true,
                ChannelWorkspaceTestHarness.CreateWorkspaceLayoutState(string.Empty, Array.Empty<string>()));

            Assert.AreEqual("src-a-order", (string)ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceSelectedOrderKey", "C:\\srcA"));
            Assert.AreEqual("src-b-order", (string)ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceSelectedOrderKey", "C:\\srcB"));
            Assert.AreEqual(string.Empty, (string)ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceSelectedOrderKey", "C:\\srcC"));

            object sourceA = ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceWindow", "C:\\srcA");
            CollectionAssert.AreEqual(
                new[] { "C:\\srcA::A-02", "C:\\srcA::A-01", "C:\\srcA::A-03" },
                ChannelWorkspaceTestHarness.ToCodeList(ChannelWorkspaceTestHarness.GetValue(sourceA, "Items")));
        }

        [TestMethod]
        public void SetMainSelectedOrderKey_DoesNotOverwriteSourceSelections()
        {
            object presenter = ChannelWorkspaceTestHarness.CreatePresenter();
            object layout = ChannelWorkspaceTestHarness.CreateWorkspaceLayoutState(
                "main-order",
                new[] { "C:\\srcA::A-01", "C:\\srcA::A-02", "C:\\srcB::B-01" },
                Tuple.Create("C:\\srcA", "src-a-order", new[] { "C:\\srcA::A-02", "C:\\srcA::A-01" }),
                Tuple.Create("C:\\srcB", "src-b-order", new[] { "C:\\srcB::B-01" }));

            ChannelWorkspaceTestHarness.Invoke(presenter, "Initialize", string.Empty, "User", false);
            ChannelWorkspaceTestHarness.Invoke(
                presenter,
                "BindData",
                ChannelWorkspaceTestData.CreateMultiSourceData(),
                null,
                null,
                false,
                layout);

            ChannelWorkspaceTestHarness.Invoke(presenter, "SetMainSelectedOrderKey", "main-updated");

            Assert.AreEqual("main-updated", (string)ChannelWorkspaceTestHarness.Invoke(presenter, "GetMainSelectedOrderKey"));
            Assert.AreEqual("src-a-order", (string)ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceSelectedOrderKey", "C:\\srcA"));
            Assert.AreEqual("src-b-order", (string)ChannelWorkspaceTestHarness.Invoke(presenter, "GetSourceSelectedOrderKey", "C:\\srcB"));
        }
    }

    internal static class ChannelWorkspaceTestHarness
    {
        public static object CreateWorkspaceModel()
        {
            return CreateInstance("JSQViewer.Application.Channels.ChannelWorkspaceModel");
        }

        public static object CreatePresenter()
        {
            return CreateInstance("JSQViewer.Presentation.WinForms.Presenters.ChannelWorkspacePresenter");
        }

        public static object CreateWorkspaceLayoutState(string mainSelectedOrderKey, IEnumerable<string> mainOrder, params Tuple<string, string, string[]>[] sourceStates)
        {
            object state = CreateInstance("JSQViewer.Application.Channels.WorkspaceLayoutState");
            SetValue(state, "MainSelectedOrderKey", mainSelectedOrderKey);
            SetValue(state, "MainOrder", (mainOrder ?? Array.Empty<string>()).ToList());

            IDictionary sources = GetValue(state, "Sources") as IDictionary;
            Assert.IsNotNull(sources, "Workspace layout Sources must implement IDictionary.");
            foreach (Tuple<string, string, string[]> sourceState in sourceStates ?? Array.Empty<Tuple<string, string, string[]>>())
            {
                object item = CreateInstance("JSQViewer.Application.Channels.WorkspaceSourceLayoutState");
                SetValue(item, "SelectedOrderKey", sourceState.Item2);
                SetValue(item, "Order", (sourceState.Item3 ?? Array.Empty<string>()).ToList());
                sources[sourceState.Item1] = item;
            }

            return state;
        }

        public static object CreateInstance(string fullTypeName, params object[] args)
        {
            Type type = Type.GetType(fullTypeName + ", JSQViewer", throwOnError: false);
            Assert.IsNotNull(type, "Type not found: " + fullTypeName);
            return Activator.CreateInstance(type, args);
        }

        public static object Invoke(object target, string methodName, params object[] args)
        {
            Assert.IsNotNull(target);
            MethodInfo[] candidates = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.Name == methodName && method.GetParameters().Length == args.Length)
                .ToArray();

            Assert.AreEqual(1, candidates.Length, "Expected exactly one overload for " + methodName + " on " + target.GetType().FullName);
            return candidates[0].Invoke(target, args);
        }

        public static object GetValue(object target, string propertyName)
        {
            Assert.IsNotNull(target);
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "Property not found: " + propertyName + " on " + target.GetType().FullName);
            return property.GetValue(target, null);
        }

        public static void SetValue(object target, string propertyName, object value)
        {
            Assert.IsNotNull(target);
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "Property not found: " + propertyName + " on " + target.GetType().FullName);
            property.SetValue(target, value, null);
        }

        public static string GetString(object target, string propertyName)
        {
            return (string)GetValue(target, propertyName);
        }

        public static bool GetBoolean(object target, string propertyName)
        {
            return (bool)GetValue(target, propertyName);
        }

        public static IList ToList(object sequence)
        {
            if (sequence == null)
            {
                return new object[0];
            }

            if (sequence is IList list)
            {
                return list;
            }

            var result = new List<object>();
            foreach (object item in (IEnumerable)sequence)
            {
                result.Add(item);
            }

            return result;
        }

        public static string[] ToCodeList(object sequence)
        {
            return ToList(sequence)
                .Cast<object>()
                .Select(item =>
                {
                    if (item is string value)
                    {
                        return value;
                    }

                    return GetString(item, "Code");
                })
                .ToArray();
        }
    }

    internal static class ChannelWorkspaceTestData
    {
        public static TestData CreateMultiSourceData(Dictionary<string, string[]> sourceColumns = null)
        {
            var layout = sourceColumns ?? new Dictionary<string, string[]>
            {
                ["C:\\srcA"] = new[] { "C:\\srcA::A-01", "C:\\srcA::A-02" },
                ["C:\\srcB"] = new[] { "C:\\srcB::B-01" }
            };

            var data = new TestData
            {
                Root = "C:\\workspace",
                RowCount = 2,
                TimestampsMs = new[] { 1000L, 2000L },
                ColumnNames = layout.Values.SelectMany(value => value).ToArray(),
                SourceColumns = new Dictionary<string, string[]>(layout, StringComparer.OrdinalIgnoreCase)
            };

            foreach (KeyValuePair<string, string[]> pair in layout)
            {
                data.SourceStartMs[pair.Key] = 1000L;
                data.SourceEndMs[pair.Key] = 2000L;

                foreach (string code in pair.Value)
                {
                    data.Columns[code] = new double?[] { 1d, 2d };
                    data.CodeSources[code] = pair.Key;
                    data.Channels[code] = new ChannelInfo
                    {
                        Code = code,
                        Name = code.Substring(code.LastIndexOf(':') + 1),
                        Unit = "u"
                    };
                }
            }

            return data;
        }
    }
}
