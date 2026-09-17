using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace KnightChronicles.Runtime.Core
{
    public sealed class AtomicSaveStore
    {
        private readonly string path;
        public AtomicSaveStore(string path) { this.path = Path.GetFullPath(path); }
        public void Write(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var bytes = Encoding.UTF8.GetBytes(Checksum(json) + "\n" + json);
            var temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
        public string Read(out bool backup)
        {
            backup = false;
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
            try { return Verify(path); }
            catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException)
            {
                backup = true; return Verify(path + ".bak");
            }
        }
        public static string Checksum(string value)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
        private static string Verify(string path)
        {
            var text = File.ReadAllText(path, Encoding.UTF8); var split = text.IndexOf('\n');
            if (split < 0 || text.Substring(0, split) != Checksum(text.Substring(split + 1))) throw new FormatException("存档校验失败");
            return text.Substring(split + 1);
        }
    }
}
