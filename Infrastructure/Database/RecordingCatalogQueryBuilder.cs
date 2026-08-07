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
                conditions.Add("r.title ILIKE @title ESCAPE '\\'");
                parameterNames.Add("title");
            }

            var sql = new StringBuilder();
            sql.AppendLine("WITH page AS (");
            sql.AppendLine("    SELECT r.id, r.post_id, r.title, r.status, r.started_at, r.stopped_at,");
            sql.AppendLine("           r.equipment_model, r.experiment_type, r.climate_mode");
            sql.AppendLine("    FROM recordings r");
            if (conditions.Count > 0)
            {
                sql.AppendLine("    WHERE " + string.Join(" AND ", conditions.ToArray()));
            }

            sql.AppendLine("    ORDER BY r.started_at DESC NULLS LAST");
            sql.AppendLine("    LIMIT @limit)");
            sql.AppendLine("SELECT p.id, p.post_id, p.title, p.status, p.started_at, p.stopped_at,");
            sql.AppendLine("       p.equipment_model, p.experiment_type, p.climate_mode,");
            sql.AppendLine("       (SELECT avg(t.v) FROM (");
            sql.AppendLine("           SELECT a.avg_value v FROM recording_aggregates a");
            sql.AppendLine("           WHERE a.recording_id = p.id AND a.channel_id = 'T-sie' AND a.avg_value IS NOT NULL");
            sql.AppendLine("           ORDER BY a.window_start LIMIT 5) t) AS t_sie_avg,");
            sql.AppendLine("       (SELECT avg(u.v) FROM (");
            sql.AppendLine("           SELECT a.avg_value v FROM recording_aggregates a");
            sql.AppendLine("           WHERE a.recording_id = p.id AND a.channel_id = 'UR-sie' AND a.avg_value IS NOT NULL");
            sql.AppendLine("           ORDER BY a.window_start LIMIT 5) u) AS ur_sie_avg");
            sql.AppendLine("FROM page p");
            sql.AppendLine("ORDER BY p.started_at DESC NULLS LAST");
            parameterNames.Add("limit");
            return sql.ToString();
        }
    }
}
