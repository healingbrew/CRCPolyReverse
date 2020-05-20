using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CRCReverse
{
    public class Crc32
    {
        private readonly uint[] Table;

        private uint ComputeChecksum(IEnumerable<byte> bytes, uint iv, bool xorout)
        {
            var crc = iv;
            foreach (var t in bytes)
            {
                var index = (byte) ((crc & 0xff) ^ t);
                crc = (crc >> 8) ^ Table[index];
            }

            if (xorout) crc = ~crc;
            return crc;
        }

        public bool Matches(uint check, IEnumerable<byte> bytes, uint iv, bool xorout)
        {
            var hash = ComputeChecksum(bytes, iv, xorout);
            var rev = BitConverter.ToUInt32(BitConverter.GetBytes(hash).Reverse().ToArray(), 0);
            return hash == check || rev == check;
        }

        public Crc32(uint poly = 0xedb88320)
        {
            Table = new uint[256];
            for (uint i = 0; i < Table.Length; ++i)
            {
                var temp = i;
                for (var j = 8; j > 0; --j)
                {
                    if ((temp & 1) == 1)
                    {
                        temp = (temp >> 1) ^ poly;
                    }
                    else
                    {
                        temp >>= 1;
                    }
                }

                Table[i] = temp;
            }
        }
    }

    internal static class Program
    {
        public static void Main()
        {
            var knownValues = new Dictionary<uint, string>
            {
                // these should all work
                {0x56B6D12E, "STULootbox".ToLowerInvariant()},
                {0x6760479E, "STUUnlock".ToLowerInvariant()},
                {0xB48F1D22, "m_name"},
                {0x3446F580, "m_description"}
            };

            var bytes = new Dictionary<string, byte[]>(); // precalc for lil bit of speed
            foreach (var keyValuePair in knownValues)
            {
                bytes[keyValuePair.Value] = Encoding.ASCII.GetBytes(keyValuePair.Value);
            }

            const string outputFile = "crc.txt";
            // after running for so long, stop, and set this value to where you ended
            const uint start = 0;
            var results = new Dictionary<string, int>();

            for (var i = start; i < uint.MaxValue; i++)
            {
                var crc = new Crc32(i);
                Console.WriteLine($"{i:X8}");
                // not a joke, lets go
                for (var j = 0; j < 4; ++j)
                {
                    // 0 = true, iv 0
                    // 1 = false, iv -1
                    // 2 = false, iv 0
                    // 3 = true, iv -1
                    var xorout = j == 0 || j == 3;
                    var iv = (j == 1 || j == 3) ? uint.MaxValue : 0;
                    var goodnessScale = knownValues.Count(knownValue =>
                        crc.Matches(knownValue.Key, bytes[knownValue.Value], iv, xorout));
                    if (goodnessScale > 0) results[$"{i:X} {xorout} {iv}"] = goodnessScale;
                }
            }

            using (Stream stream = File.OpenWrite(outputFile))
            {
                using (var writer = new StreamWriter(stream))
                {
                    foreach (var keyValuePair in results)
                    {
                        writer.WriteLine($"{keyValuePair.Key}: {keyValuePair.Value}");
                    }
                }
            }
        }
    }
}
