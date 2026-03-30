using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JSQViewer.Application.Abstractions;
using JSQViewer.Settings;

namespace JSQViewer.Infrastructure.Persistence
{
    public sealed class FileOrderRepository : IOrderRepository
    {
        private readonly string _baseDirectory;
        private readonly string _legacyOrderFilePath;

        public FileOrderRepository(IAppPaths appPaths)
        {
            if (appPaths == null) throw new ArgumentNullException(nameof(appPaths));

            _baseDirectory = appPaths.ProjectRoot;
            _legacyOrderFilePath = Path.Combine(_baseDirectory, "channel_order.json");
        }

        public List<ChannelOrderModel> List()
        {
            return OrderStore.List(_baseDirectory);
        }

        public ChannelOrderModel Load(string keyOrName)
        {
            return OrderStore.Load(_baseDirectory, keyOrName);
        }

        public bool Exists(string keyOrName)
        {
            return OrderStore.Exists(_baseDirectory, keyOrName);
        }

        public ChannelOrderModel Save(string name, IList<string> order)
        {
            return OrderStore.Save(_baseDirectory, name, order);
        }

        public bool Delete(string keyOrName)
        {
            return OrderStore.Delete(_baseDirectory, keyOrName);
        }

        public List<string> LoadLegacyOrder()
        {
            if (!File.Exists(_legacyOrderFilePath))
            {
                return new List<string>();
            }

            OrderPayload payload = JsonHelper.LoadFromFile(_legacyOrderFilePath, new OrderPayload());
            return payload == null || payload.order == null
                ? new List<string>()
                : payload.order;
        }

        public bool SaveLegacyOrder(IList<string> order)
        {
            var payload = new OrderPayload
            {
                order = order == null ? new List<string>() : order.ToList()
            };

            return JsonHelper.SaveToFile(_legacyOrderFilePath, payload);
        }

        private sealed class OrderPayload
        {
            public List<string> order { get; set; }
        }
    }
}
