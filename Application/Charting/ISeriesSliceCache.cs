using JSQViewer.Core;

namespace JSQViewer.Application.Charting
{
    public interface ISeriesSliceCache
    {
        bool TryGet(string key, out SeriesSlice slice);
        void Set(string key, SeriesSlice slice);
        void Clear();
    }
}
