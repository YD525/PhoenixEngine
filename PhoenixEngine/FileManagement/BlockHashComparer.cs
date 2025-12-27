using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixEngine.FileManagement
{
    internal class BlockHashComparer
    {
        private static readonly uint[] Crc32Table = GenerateCrc32Table();

        private static uint[] GenerateCrc32Table()
        {
            uint[] Table = new uint[256];
            const uint Polynomial = 0xEDB88320;

            for (uint i = 0; i < 256; i++)
            {
                uint CRC = i;
                for (int j = 0; j < 8; j++)
                    CRC = (CRC & 1) != 0 ? (CRC >> 1) ^ Polynomial : (CRC >> 1);
                Table[i] = CRC;
            }

            return Table;
        }

        private static uint ComputeCrc32(byte[] Buffer, int Offset, int Count, uint Previous = 0xFFFFFFFF)
        {
            uint CRC = Previous;
            for (int i = Offset; i < Offset + Count; i++)
                CRC = (CRC >> 8) ^ Crc32Table[(CRC & 0xFF) ^ Buffer[i]];
            return CRC;
        }

        /// <summary>
        /// Compare two block MD5 arrays and return similarity ratio (0~1).
        /// </summary>
        public static double CompareBlockHashesFlexible(string[] H1, string[] H2, int Window = 2)
        {
            int Matches = 0;
            int Total = Math.Max(H1.Length, H2.Length);

            for (int i = 0; i < H1.Length; i++)
            {
                bool Found = false;

                int Start = Math.Max(0, i - Window);
                int End = Math.Min(H2.Length - 1, i + Window);

                for (int j = Start; j <= End; j++)
                {
                    if (H1[i] == H2[j])
                    {
                        Found = true;
                        break;
                    }
                }

                if (Found) Matches++;
            }

            return (double)Matches / Total;
        }

        /// <summary>
        /// Join an array of hashes into a single string.
        /// </summary>
        public static string JoinHashes(string[] Hashes) => string.Join("|", Hashes);

        /// <summary>
        /// Split a joined hash string back into an array.
        /// </summary>
        public static string[] SplitHashes(string joined)
        {
            return joined.Split(
                new[] { '|' },
                StringSplitOptions.RemoveEmptyEntries
            );
        }


        public static bool MatchFile(string Key1, string Key2)
        {
            if (CompareBlockHashesFlexible(SplitHashes(Key1), SplitHashes(Key2))>=0.5)
            {
                return true;
            }

            return false;
        }
    }
}
