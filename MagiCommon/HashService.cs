using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MagiCommon
{
    public interface IHashService
    {
        string GenerateContentHash(Stream stream, bool rewind);
        string GeneratePasswordHash(string password);
    }

    public class HashService : IHashService
    {
        public string GenerateContentHash(Stream stream, bool rewind)
        {
            using (HashAlgorithm alg = SHA256.Create())
            {
                var bytes = alg.ComputeHash(stream);
                if (rewind && stream.CanSeek)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }
                return Convert.ToBase64String(bytes);
            }
        }

        public string GeneratePasswordHash(string password)
        {
            using (HashAlgorithm alg = SHA256.Create())
            {
                var bytes = alg.ComputeHash(Encoding.UTF8.GetBytes("magicloud_" + password));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
