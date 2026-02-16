using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LeMuReViewer.Settings
{
    public sealed class ChannelOrderModel
    {
        public string key { get; set; }
        public string name { get; set; }
        public string saved_at { get; set; }
        public List<string> order { get; set; }
    }

    public static class OrderStore
    {
        public static string OrdersDir(string baseDir)
        {
            return Path.Combine(baseDir, "saved_orders");
        }

        public static List<ChannelOrderModel> List(string baseDir)
        {
            string dir = OrdersDir(baseDir);
            var result = new List<ChannelOrderModel>();
            if (!Directory.Exists(dir))
            {
                return result;
            }

            foreach (string file in Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
            {
                ChannelOrderModel o = JsonHelper.LoadFromFile(file, (ChannelOrderModel)null);
                if (o == null)
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(o.key))
                {
                    o.key = Path.GetFileNameWithoutExtension(file);
                }
                if (string.IsNullOrWhiteSpace(o.name))
                {
                    o.name = o.key;
                }
                if (o.order == null)
                {
                    o.order = new List<string>();
                }
                result.Add(o);
            }

            return result.OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static ChannelOrderModel Load(string baseDir, string keyOrName)
        {
            string key = Persistence.SanitizeKey(keyOrName, "order");
            string path = Path.Combine(OrdersDir(baseDir), key + ".json");
            return JsonHelper.LoadFromFile(path, (ChannelOrderModel)null);
        }

        public static bool Exists(string baseDir, string keyOrName)
        {
            string key = Persistence.SanitizeKey(keyOrName, "order");
            string path = Path.Combine(OrdersDir(baseDir), key + ".json");
            return File.Exists(path);
        }

        public static ChannelOrderModel Save(string baseDir, string name, IList<string> order)
        {
            string key = Persistence.SanitizeKey(name, "order");
            string dir = OrdersDir(baseDir);
            Directory.CreateDirectory(dir);

            var payload = new ChannelOrderModel
            {
                key = key,
                name = name,
                saved_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                order = order == null ? new List<string>() : order.ToList()
            };

            string path = Path.Combine(dir, key + ".json");
            JsonHelper.SaveToFile(path, payload);
            return payload;
        }

        public static bool Delete(string baseDir, string keyOrName)
        {
            try
            {
                string key = Persistence.SanitizeKey(keyOrName, "order");
                string path = Path.Combine(OrdersDir(baseDir), key + ".json");
                if (!File.Exists(path))
                {
                    return false;
                }
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
