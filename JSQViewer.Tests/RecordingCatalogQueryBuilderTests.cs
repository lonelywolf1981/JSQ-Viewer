using System;
using System.Collections.Generic;
using JSQViewer.Application.Database;
using JSQViewer.Infrastructure.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingCatalogQueryBuilderTests
    {
        [TestMethod]
        public void Build_WithoutFilters_SelectsAllOrderedByStartDescending()
        {
            var parameters = new List<string>();

            string sql = new RecordingCatalogQueryBuilder().Build(new RecordingCatalogFilter(), parameters);

            StringAssert.Contains(sql, "FROM recordings r");
            StringAssert.Contains(sql, "ORDER BY r.started_at DESC NULLS LAST");
            StringAssert.Contains(sql, "LIMIT @limit");
            CollectionAssert.AreEqual(new[] { "limit" }, parameters);
        }

        [TestMethod]
        public void Build_WithEveryFilter_AddsOneConditionPerParameter()
        {
            var parameters = new List<string>();
            var filter = new RecordingCatalogFilter
            {
                PostId = "B",
                From = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Local),
                To = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Local),
                ExperimentType = "FUNC",
                TitleContains = "LIDER"
            };

            string sql = new RecordingCatalogQueryBuilder().Build(filter, parameters);

            StringAssert.Contains(sql, "r.post_id = @post_id");
            StringAssert.Contains(sql, "r.started_at >= @from");
            StringAssert.Contains(sql, "r.started_at < @to");
            StringAssert.Contains(sql, "r.experiment_type = @experiment_type");
            StringAssert.Contains(sql, "r.title ILIKE @title");
            CollectionAssert.AreEquivalent(
                new[] { "post_id", "from", "to", "experiment_type", "title", "limit" }, parameters);
        }

        [TestMethod]
        public void Build_WithTitleFilter_SpecifiesEscapeCharacter()
        {
            var parameters = new List<string>();
            var filter = new RecordingCatalogFilter { TitleContains = "LIDER" };

            string sql = new RecordingCatalogQueryBuilder().Build(filter, parameters);

            StringAssert.Contains(sql, "r.title ILIKE @title ESCAPE '\\'");
        }

        [TestMethod]
        public void Build_IgnoresBlankFilterValues()
        {
            var parameters = new List<string>();
            var filter = new RecordingCatalogFilter { PostId = "   ", ExperimentType = "", TitleContains = null };

            string sql = new RecordingCatalogQueryBuilder().Build(filter, parameters);

            Assert.IsFalse(sql.Contains("@post_id"));
            Assert.IsFalse(sql.Contains("@experiment_type"));
            Assert.IsFalse(sql.Contains("@title"));
            CollectionAssert.AreEqual(new[] { "limit" }, parameters);
        }

        [TestMethod]
        public void Build_NeverEmitsWriteStatements()
        {
            string sql = new RecordingCatalogQueryBuilder().Build(new RecordingCatalogFilter(), new List<string>());

            foreach (string forbidden in new[] { "INSERT", "UPDATE", "DELETE", "CREATE", "DROP" })
            {
                Assert.IsFalse(sql.ToUpperInvariant().Contains(forbidden), "SQL не должен содержать " + forbidden);
            }
        }
    }
}
