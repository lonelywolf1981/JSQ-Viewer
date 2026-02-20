using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace JSQViewer.Settings
{
    public static class JsonHelper
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static T LoadFromFile<T>(string path, T fallback)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return fallback;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return fallback;
                }

                T value = Serializer.Deserialize<T>(json);
                return value == null ? fallback : value;
            }
            catch
            {
                return fallback;
            }
        }

        public static bool SaveToFile<T>(string path, T value)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = Serializer.Serialize(value);
                File.WriteAllText(path, json, Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
