using System;
using System.Collections.Generic;
using System.Text;
using JSQViewer.Application.Database;

namespace JSQViewer.Infrastructure.Database
{
    public sealed class RecordingCatalogQueryBuilder
    {
        public string Build(RecordingCatalogFilter filter, IList<string> parameterNames)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (parameterNames == null) throw new ArgumentNullException(nameof(parameterNames));

            var conditions = new List<string>();
            if (!string.IsNullOrWhiteSpace(filter.PostId))
            {
                conditions.Add("r.post_id = @post_id");
                parameterNames.Add("post_id");
            }

            if (filter.From.HasValue)
            {
                conditions.Add("r.started_at >= @from");
                parameterNames.Add("from");
            }

            if (filter.To.HasValue)
            {
                conditions.Add("r.started_at < @to");
                parameterNames.Add("to");
            }

            if (!string.IsNullOrWhiteSpace(filter.ExperimentType))
            {
                conditions.Add("r.experiment_type = @experiment_type");
                parameterNames.Add("experiment_type");
            }

            if (!string.IsNullOrWhiteSpace(filter.TitleContains))
            {
                conditions.Add("r.title ILIKE @title");
                parameterNames.Add("title");
            }

            var sql = new StringBuilder();
            sql.AppendLine("SELECT r.id, r.post_id, r.title, r.status, r.started_at, r.stopped_at,");
            sql.AppendLine("       r.equipment_model, r.experiment_type");
            sql.AppendLine("FROM recordings r");
            if (conditions.Count > 0)
            {
                sql.AppendLine("WHERE " + string.Join(" AND ", conditions.ToArray()));
            }

            sql.AppendLine("ORDER BY r.started_at DESC NULLS LAST");
            sql.AppendLine("LIMIT @limit");
            parameterNames.Add("limit");
            return sql.ToString();
        }
    }
}
