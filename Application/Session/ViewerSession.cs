using System.Threading;
using JSQViewer.Application.Charting;
using JSQViewer.Core;

namespace JSQViewer.Application.Session
{
    public sealed class ViewerSession : IViewerSession
    {
        private readonly object _sync = new object();
        private readonly ISeriesSliceCache _seriesSliceCache;
        private int _dataVersion;
        private bool _loaded;
        private string _folder = string.Empty;
        private TestData _data;

        public ViewerSession(ISeriesSliceCache seriesSliceCache)
        {
            _seriesSliceCache = seriesSliceCache;
        }

        public int DataVersion
        {
            get { return Volatile.Read(ref _dataVersion); }
        }

        public bool IsLoaded
        {
            get
            {
                lock (_sync)
                {
                    return _loaded;
                }
            }
        }

        public string Folder
        {
            get
            {
                lock (_sync)
                {
                    return _folder;
                }
            }
        }

        public TestData Data
        {
            get
            {
                lock (_sync)
                {
                    return _data;
                }
            }
        }

        public void SetData(string folder, TestData data)
        {
            lock (_sync)
            {
                _folder = folder ?? string.Empty;
                _data = data;
                _loaded = data != null;
                _dataVersion++;
            }

            if (_seriesSliceCache != null)
            {
                _seriesSliceCache.Clear();
            }
        }
    }
}
