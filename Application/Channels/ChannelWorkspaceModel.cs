using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Core;

namespace JSQViewer.Application.Channels
{
    public sealed class ChannelWorkspaceModel
    {
        private readonly List<ChannelWorkspaceEntry> _channels = new List<ChannelWorkspaceEntry>();
        private readonly Dictionary<string, ChannelWorkspaceEntry> _channelsByCode = new Dictionary<string, ChannelWorkspaceEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _selectedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ChannelWorkspaceEntry>> _sourceOrders = new Dictionary<string, List<ChannelWorkspaceEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _sourceRoots = new List<string>();

        public int TotalCount
        {
            get { return _channels.Count; }
        }

        public int SelectedCount
        {
            get { return _selectedCodes.Count; }
        }

        public IReadOnlyList<string> SourceRoots
        {
            get { return _sourceRoots.ToArray(); }
        }

        public void Load(TestData data, IEnumerable<string> savedOrder, IEnumerable<string> selectedCodes)
        {
            var requestedSelection = selectedCodes == null
                ? new HashSet<string>(_selectedCodes, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(selectedCodes.Where(code => !string.IsNullOrWhiteSpace(code)), StringComparer.OrdinalIgnoreCase);

            _channels.Clear();
            _channelsByCode.Clear();
            _sourceOrders.Clear();
            _sourceRoots.Clear();
            _selectedCodes.Clear();

            if (data == null)
            {
                return;
            }

            string[] orderedColumns = ApplySavedOrder(data.ColumnNames, savedOrder);
            for (int i = 0; i < orderedColumns.Length; i++)
            {
                string code = orderedColumns[i];
                if (string.IsNullOrWhiteSpace(code) || _channelsByCode.ContainsKey(code))
                {
                    continue;
                }

                ChannelInfo channel;
                data.Channels.TryGetValue(code, out channel);
                var entry = new ChannelWorkspaceEntry(
                    code,
                    channel == null ? code : channel.Label,
                    BuildDisplayLabel(NormalizeChannelCodeForDisplay(code), channel),
                    channel == null ? string.Empty : (channel.Unit ?? string.Empty));

                _channels.Add(entry);
                _channelsByCode[entry.Code] = entry;
            }

            foreach (string code in requestedSelection)
            {
                if (_channelsByCode.ContainsKey(code))
                {
                    _selectedCodes.Add(code);
                }
            }

            if (data.SourceColumns == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string[]> pair in data.SourceColumns)
            {
                string sourceRoot = pair.Key ?? string.Empty;
                _sourceRoots.Add(sourceRoot);

                var sourceSet = new HashSet<string>(pair.Value ?? new string[0], StringComparer.OrdinalIgnoreCase);
                var orderedItems = new List<ChannelWorkspaceEntry>();
                for (int i = 0; i < _channels.Count; i++)
                {
                    ChannelWorkspaceEntry entry = _channels[i];
                    if (sourceSet.Contains(entry.Code))
                    {
                        orderedItems.Add(entry);
                    }
                }

                _sourceOrders[sourceRoot] = orderedItems;
            }
        }

        public IReadOnlyList<ChannelListProjectionItem> BuildMainList(string filterText, string sortMode, bool selectedOnly)
        {
            return BuildProjection(_channels, filterText, sortMode, selectedOnly, false);
        }

        public IReadOnlyList<ChannelListProjectionItem> BuildSourceList(string sourceRoot, string filterText, string sortMode, bool selectedOnly)
        {
            List<ChannelWorkspaceEntry> items;
            if (!_sourceOrders.TryGetValue(sourceRoot ?? string.Empty, out items))
            {
                return new ChannelListProjectionItem[0];
            }

            return BuildProjection(items, filterText, sortMode, selectedOnly, true);
        }

        public IReadOnlyList<string> GetSelectedCodes()
        {
            var result = new List<string>();
            for (int i = 0; i < _channels.Count; i++)
            {
                ChannelWorkspaceEntry entry = _channels[i];
                if (_selectedCodes.Contains(entry.Code))
                {
                    result.Add(entry.Code);
                }
            }

            return result;
        }

        public IReadOnlyList<string> GetCurrentOrder()
        {
            return _channels.Select(entry => entry.Code).ToArray();
        }

        public IReadOnlyList<string> GetCurrentOrderForSource(string sourceRoot)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<ChannelWorkspaceEntry> sourceItems;
            if (_sourceOrders.TryGetValue(sourceRoot ?? string.Empty, out sourceItems))
            {
                AddOrder(result, seen, sourceItems);
            }

            AddOrder(result, seen, _channels);
            return result;
        }

        public IReadOnlyList<string> GetEffectiveOrderForSource(string sourceRoot)
        {
            List<ChannelWorkspaceEntry> sourceItems;
            if (!_sourceOrders.TryGetValue(sourceRoot ?? string.Empty, out sourceItems))
            {
                return new string[0];
            }

            return sourceItems.Select(entry => entry.Code).ToArray();
        }

        public void ReplaceSelectedCodes(IEnumerable<string> checkedCodes)
        {
            _selectedCodes.Clear();
            if (checkedCodes == null)
            {
                return;
            }

            foreach (string code in checkedCodes)
            {
                if (!string.IsNullOrWhiteSpace(code) && _channelsByCode.ContainsKey(code))
                {
                    _selectedCodes.Add(code);
                }
            }
        }

        public void SetChannelSelected(string code, bool isSelected)
        {
            if (string.IsNullOrWhiteSpace(code) || !_channelsByCode.ContainsKey(code))
            {
                return;
            }

            if (isSelected)
            {
                _selectedCodes.Add(code);
            }
            else
            {
                _selectedCodes.Remove(code);
            }
        }

        public void SelectAll()
        {
            for (int i = 0; i < _channels.Count; i++)
            {
                _selectedCodes.Add(_channels[i].Code);
            }
        }

        public void ClearAll()
        {
            _selectedCodes.Clear();
        }

        public void SelectAllInSource(string sourceRoot)
        {
            List<ChannelWorkspaceEntry> sourceItems;
            if (!_sourceOrders.TryGetValue(sourceRoot ?? string.Empty, out sourceItems))
            {
                return;
            }

            for (int i = 0; i < sourceItems.Count; i++)
            {
                _selectedCodes.Add(sourceItems[i].Code);
            }
        }

        public void ClearAllInSource(string sourceRoot)
        {
            List<ChannelWorkspaceEntry> sourceItems;
            if (!_sourceOrders.TryGetValue(sourceRoot ?? string.Empty, out sourceItems))
            {
                return;
            }

            for (int i = 0; i < sourceItems.Count; i++)
            {
                _selectedCodes.Remove(sourceItems[i].Code);
            }
        }

        public bool MoveMainItem(int fromIndex, int toIndex)
        {
            if (!IsMovableIndex(_channels, fromIndex, toIndex))
            {
                return false;
            }

            ChannelWorkspaceEntry entry = _channels[fromIndex];
            _channels.RemoveAt(fromIndex);
            _channels.Insert(toIndex, entry);
            return true;
        }

        public bool MoveSourceItem(string sourceRoot, int fromIndex, int toIndex)
        {
            List<ChannelWorkspaceEntry> sourceItems;
            if (!_sourceOrders.TryGetValue(sourceRoot ?? string.Empty, out sourceItems) || !IsMovableIndex(sourceItems, fromIndex, toIndex))
            {
                return false;
            }

            ChannelWorkspaceEntry entry = sourceItems[fromIndex];
            sourceItems.RemoveAt(fromIndex);
            sourceItems.Insert(toIndex, entry);
            return true;
        }

        public void ApplyOrder(IEnumerable<string> order)
        {
            string[] requested = order == null
                ? new string[0]
                : order.Where(code => !string.IsNullOrWhiteSpace(code)).ToArray();

            if (requested.Length == 0)
            {
                return;
            }

            var map = _channels.ToDictionary(entry => entry.Code, StringComparer.OrdinalIgnoreCase);
            var reordered = new List<ChannelWorkspaceEntry>(_channels.Count);
            for (int i = 0; i < requested.Length; i++)
            {
                ChannelWorkspaceEntry entry;
                if (map.TryGetValue(requested[i], out entry))
                {
                    reordered.Add(entry);
                    map.Remove(requested[i]);
                }
            }

            for (int i = 0; i < _channels.Count; i++)
            {
                ChannelWorkspaceEntry entry = _channels[i];
                if (map.ContainsKey(entry.Code))
                {
                    reordered.Add(entry);
                    map.Remove(entry.Code);
                }
            }

            _channels.Clear();
            _channels.AddRange(reordered);

            foreach (string sourceRoot in _sourceRoots)
            {
                List<ChannelWorkspaceEntry> sourceItems = _sourceOrders[sourceRoot];
                var sourceMap = sourceItems.ToDictionary(entry => entry.Code, StringComparer.OrdinalIgnoreCase);
                var sourceReordered = new List<ChannelWorkspaceEntry>(sourceItems.Count);
                for (int i = 0; i < _channels.Count; i++)
                {
                    ChannelWorkspaceEntry entry;
                    if (sourceMap.TryGetValue(_channels[i].Code, out entry))
                    {
                        sourceReordered.Add(entry);
                    }
                }

                _sourceOrders[sourceRoot] = sourceReordered;
            }
        }

        public void ApplyOrderToSource(string sourceRoot, IEnumerable<string> order)
        {
            List<ChannelWorkspaceEntry> sourceItems;
            if (!_sourceOrders.TryGetValue(sourceRoot ?? string.Empty, out sourceItems))
            {
                return;
            }

            string[] requested = order == null
                ? new string[0]
                : order.Where(code => !string.IsNullOrWhiteSpace(code)).ToArray();
            if (requested.Length == 0)
            {
                return;
            }

            var sourceMap = sourceItems.ToDictionary(entry => entry.Code, StringComparer.OrdinalIgnoreCase);
            var reordered = new List<ChannelWorkspaceEntry>(sourceItems.Count);
            for (int i = 0; i < requested.Length; i++)
            {
                ChannelWorkspaceEntry entry;
                if (sourceMap.TryGetValue(requested[i], out entry))
                {
                    reordered.Add(entry);
                    sourceMap.Remove(requested[i]);
                }
            }

            for (int i = 0; i < sourceItems.Count; i++)
            {
                ChannelWorkspaceEntry entry = sourceItems[i];
                if (sourceMap.ContainsKey(entry.Code))
                {
                    reordered.Add(entry);
                    sourceMap.Remove(entry.Code);
                }
            }

            _sourceOrders[sourceRoot ?? string.Empty] = reordered;
        }

        public void ApplyEffectiveOrderToSource(string sourceRoot, IEnumerable<string> order)
        {
            ApplyOrderToSource(sourceRoot, order);
        }

        public void ApplyOrder(string firstCode, string secondCode, string thirdCode)
        {
            ApplyOrder(new[] { firstCode, secondCode, thirdCode });
        }

        private IReadOnlyList<ChannelListProjectionItem> BuildProjection(IEnumerable<ChannelWorkspaceEntry> source, string filterText, string sortMode, bool selectedOnly, bool useSourceLabel)
        {
            IEnumerable<ChannelWorkspaceEntry> items = source ?? Enumerable.Empty<ChannelWorkspaceEntry>();
            string filter = (filterText ?? string.Empty).Trim();
            if (filter.Length > 0)
            {
                string lowered = filter.ToLowerInvariant();
                items = items.Where(entry =>
                    (entry.Code ?? string.Empty).ToLowerInvariant().Contains(lowered)
                    || (entry.MainLabel ?? string.Empty).ToLowerInvariant().Contains(lowered)
                    || (entry.SourceLabel ?? string.Empty).ToLowerInvariant().Contains(lowered));
            }

            if (selectedOnly)
            {
                items = items.Where(entry => _selectedCodes.Contains(entry.Code));
            }

            string mode = NormalizeSortMode(sortMode);
            if (string.Equals(mode, "Code", StringComparison.Ordinal))
            {
                items = items.OrderBy(entry => entry.Code, StringComparer.OrdinalIgnoreCase);
            }
            else if (string.Equals(mode, "Natural code", StringComparison.Ordinal))
            {
                items = items.OrderBy(entry => entry.Code, NaturalStringComparer.Instance);
            }
            else if (string.Equals(mode, "Label", StringComparison.Ordinal))
            {
                items = items.OrderBy(entry => useSourceLabel ? entry.SourceLabel : entry.MainLabel, StringComparer.OrdinalIgnoreCase);
            }
            else if (string.Equals(mode, "Unit", StringComparison.Ordinal))
            {
                items = items.OrderBy(entry => entry.Unit, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.Code, StringComparer.OrdinalIgnoreCase);
            }
            else if (string.Equals(mode, "Priority A/C", StringComparison.Ordinal))
            {
                items = items.OrderBy(entry => PrefixPriority(entry.Code))
                    .ThenBy(entry => entry.Code, NaturalStringComparer.Instance);
            }
            else if (string.Equals(mode, "Selected first", StringComparison.Ordinal))
            {
                items = items.OrderByDescending(entry => _selectedCodes.Contains(entry.Code))
                    .ThenBy(entry => entry.Code, StringComparer.OrdinalIgnoreCase);
            }

            var result = new List<ChannelListProjectionItem>();
            foreach (ChannelWorkspaceEntry entry in items)
            {
                result.Add(new ChannelListProjectionItem(
                    entry.Code,
                    useSourceLabel ? entry.SourceLabel : entry.MainLabel,
                    entry.Unit,
                    _selectedCodes.Contains(entry.Code)));
            }

            return result;
        }

        private static string[] ApplySavedOrder(string[] columns, IEnumerable<string> savedOrder)
        {
            var source = new List<string>(columns ?? new string[0]);
            var saved = savedOrder == null
                ? new List<string>()
                : savedOrder.Where(code => !string.IsNullOrWhiteSpace(code)).ToList();
            if (saved.Count == 0)
            {
                return source.ToArray();
            }

            var result = new List<string>(source.Count);
            var inSource = new HashSet<string>(source, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < saved.Count; i++)
            {
                if (inSource.Contains(saved[i]) && !result.Contains(saved[i], StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(saved[i]);
                }
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (!result.Contains(source[i], StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(source[i]);
                }
            }

            return result.ToArray();
        }

        private static bool IsMovableIndex<T>(IList<T> items, int fromIndex, int toIndex)
        {
            return items != null
                   && fromIndex >= 0
                   && toIndex >= 0
                   && fromIndex < items.Count
                   && toIndex < items.Count
                   && fromIndex != toIndex;
        }

        private static void AddOrder(List<string> target, HashSet<string> seen, IEnumerable<ChannelWorkspaceEntry> items)
        {
            foreach (ChannelWorkspaceEntry entry in items)
            {
                if (seen.Add(entry.Code))
                {
                    target.Add(entry.Code);
                }
            }
        }

        private static string NormalizeSortMode(string sortMode)
        {
            return string.IsNullOrWhiteSpace(sortMode) ? "User" : sortMode.Trim();
        }

        private static int PrefixPriority(string code)
        {
            string value = code ?? string.Empty;
            if (value.StartsWith("A-", StringComparison.OrdinalIgnoreCase)) return 0;
            if (value.StartsWith("C-", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        private static string NormalizeChannelCodeForDisplay(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            int separator = code.IndexOf("::", StringComparison.Ordinal);
            string result = separator >= 0 ? code.Substring(separator + 2) : code;
            int hash = result.IndexOf('#');
            if (hash > 0)
            {
                result = result.Substring(0, hash);
            }

            return result;
        }

        private static string BuildDisplayLabel(string displayCode, ChannelInfo channel)
        {
            string unitPart = channel == null || string.IsNullOrEmpty(channel.Unit) ? string.Empty : " (" + channel.Unit + ")";
            string namePart = channel == null || string.IsNullOrEmpty(channel.Name) ? string.Empty : " - " + channel.Name;
            return (displayCode ?? string.Empty) + namePart + unitPart;
        }

        private sealed class ChannelWorkspaceEntry
        {
            public ChannelWorkspaceEntry(string code, string mainLabel, string sourceLabel, string unit)
            {
                Code = code ?? string.Empty;
                MainLabel = mainLabel ?? string.Empty;
                SourceLabel = sourceLabel ?? string.Empty;
                Unit = unit ?? string.Empty;
            }

            public string Code { get; private set; }

            public string MainLabel { get; private set; }

            public string SourceLabel { get; private set; }

            public string Unit { get; private set; }
        }

    }
}
