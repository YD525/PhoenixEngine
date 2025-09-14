using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixEngine.FileManagement
{
    public class BlockHashComparer
    {
        /// <summary>
        /// Compute block-based MD5 hashes of a file.
        /// </summary>
        /// <param name="FilePath">File path</param>
        /// <param name="TargetBlockCount">Number of blocks to divide the file into</param>
        /// <param name="BufferSize">Buffer size per read (default 4MB)</param>
        /// <returns>Array of MD5 hashes (hex string) for each block</returns>
        public static string[] GetBlockMD5(string FilePath, int TargetBlockCount = 100, int BufferSize = 4 * 1024 * 1024)
        {
            using var Stream = File.OpenRead(FilePath);
            long FileSize = Stream.Length;
            long BlockSize = Math.Max(1, FileSize / TargetBlockCount);

            byte[] Buffer = new byte[BufferSize];
            var Hashes = new string[TargetBlockCount];
            int BlockIndex = 0;

            while (BlockIndex < TargetBlockCount && Stream.Position < FileSize)
            {
                using var BlockMd5 = MD5.Create();
                long BytesToRead = BlockSize;

                while (BytesToRead > 0)
                {
                    int ReadSize = (int)Math.Min(BufferSize, BytesToRead);
                    int BytesRead = Stream.Read(Buffer, 0, ReadSize);
                    if (BytesRead == 0) break;

                    BlockMd5.TransformBlock(Buffer, 0, BytesRead, null, 0);
                    BytesToRead -= BytesRead;
                }

                BlockMd5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                Hashes[BlockIndex++] = BitConverter.ToString(BlockMd5.Hash!).Replace("-", "").ToLowerInvariant();
            }

            return Hashes;
        }

        /// <summary>
        /// Compare two block MD5 arrays and return similarity ratio (0~1).
        /// </summary>
        public static double CompareHashes(string[] H1, string[] H2)
        {
            if (H1.Length == 0 || H2.Length == 0) return 0;

            int Min = Math.Min(H1.Length, H2.Length);
            int Max = Math.Max(H1.Length, H2.Length);

            int Same = 0;
            for (int I = 0; I < Min; I++)
            {
                if (H1[I] == H2[I]) Same++;
            }

            return (double)Same / Max;
        }

        /// <summary>
        /// Join an array of hashes into a single string.
        /// </summary>
        public static string JoinHashes(string[] Hashes) => string.Join("|", Hashes);

        /// <summary>
        /// Split a joined hash string back into an array.
        /// </summary>
        public static string[] SplitHashes(string Joined) =>
            Joined.Split('|', StringSplitOptions.RemoveEmptyEntries);


        public static bool MatchFile(string Key1, string Key2)
        {
            if (CompareHashes(SplitHashes(Key1), SplitHashes(Key2))>=0.8)
            {
                return true;
            }

            return false;
        }
    }
}
