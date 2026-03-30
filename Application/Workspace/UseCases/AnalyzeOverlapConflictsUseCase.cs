using System.Collections.Generic;
using System.Linq;
using JSQViewer.Core;

namespace JSQViewer.Application.Workspace.UseCases
{
    public sealed class AnalyzeOverlapConflictsUseCase
    {
        public List<string> Execute(IList<TestData> list)
        {
            var codeToSources = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            if (list == null)
            {
                return new List<string>();
            }

            for (int i = 0; i < list.Count; i++)
            {
                TestData data = list[i];
                var seenInSource = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < data.ColumnNames.Length; c++)
                {
                    string code = data.ColumnNames[c];
                    if (!seenInSource.Add(code))
                    {
                        continue;
                    }

                    int count;
                    codeToSources.TryGetValue(code, out count);
                    codeToSources[code] = count + 1;
                }
            }

            return codeToSources
                .Where(kv => kv.Value > 1)
                .Select(kv => kv.Key)
                .OrderBy(code => code, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
