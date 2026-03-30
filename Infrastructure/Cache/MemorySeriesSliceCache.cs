using System.Collections.Generic;
using JSQViewer.Application.Charting;
using JSQViewer.Core;

namespace JSQViewer.Infrastructure.Cache
{
    public sealed class MemorySeriesSliceCache : ISeriesSliceCache
    {
        private const int MaxEntries = 8;
        private readonly object _sync = new object();
        private readonly Dictionary<string, LinkedListNode<CacheItem>> _byKey = new Dictionary<string, LinkedListNode<CacheItem>>();
        private readonly LinkedList<CacheItem> _lru = new LinkedList<CacheItem>();

        public bool TryGet(string key, out SeriesSlice slice)
        {
            lock (_sync)
            {
                LinkedListNode<CacheItem> existingNode;
                if (_byKey.TryGetValue(key, out existingNode))
                {
                    _lru.Remove(existingNode);
                    _lru.AddFirst(existingNode);
                    slice = existingNode.Value.Value;
                    return true;
                }
            }

            slice = null;
            return false;
        }

        public void Set(string key, SeriesSlice slice)
        {
            lock (_sync)
            {
                LinkedListNode<CacheItem> existingNode;
                if (_byKey.TryGetValue(key, out existingNode))
                {
                    existingNode.Value.Value = slice;
                    _lru.Remove(existingNode);
                    _lru.AddFirst(existingNode);
                    return;
                }

                var node = new LinkedListNode<CacheItem>(new CacheItem { Key = key, Value = slice });
                _lru.AddFirst(node);
                _byKey[key] = node;

                while (_lru.Count > MaxEntries)
                {
                    LinkedListNode<CacheItem> last = _lru.Last;
                    if (last == null)
                    {
                        break;
                    }

                    _lru.RemoveLast();
                    _byKey.Remove(last.Value.Key);
                }
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _byKey.Clear();
                _lru.Clear();
            }
        }

        private sealed class CacheItem
        {
            public string Key { get; set; }
            public SeriesSlice Value { get; set; }
        }
    }
}
