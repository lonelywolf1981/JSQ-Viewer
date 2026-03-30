using System.Collections.Generic;
using JSQViewer.Settings;

namespace JSQViewer.Application.Abstractions
{
    public interface IOrderRepository
    {
        List<ChannelOrderModel> List();

        ChannelOrderModel Load(string keyOrName);

        bool Exists(string keyOrName);

        ChannelOrderModel Save(string name, IList<string> order);

        bool Delete(string keyOrName);

        List<string> LoadLegacyOrder();

        bool SaveLegacyOrder(IList<string> order);
    }
}
