using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace KnightChronicles.Runtime.Core
{
    /// <summary>Preserves null equipment slots and optional run/report references across saves.</summary>
    public static class ProfileSerializer
    {
        public static string Serialize(Profile profile)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(Profile)).WriteObject(stream, profile);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public static Profile Deserialize(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var p = (Profile)new DataContractJsonSerializer(typeof(Profile)).ReadObject(stream);
                if (p == null || p.Version != 1 || p.Equipped == null || p.Equipped.Length != 6
                    || p.WarehouseLevel < 1 || p.WarehouseLevel > 5 || p.TrainingLevel < 1 || p.TrainingLevel > 5
                    || p.CollectionLevel < 1 || p.CollectionLevel > 5 || p.Warehouse == null || p.PackItems == null || p.RigItems == null)
                    throw new FormatException("存档版本或结构无效");
                return p;
            }
        }
    }
}
