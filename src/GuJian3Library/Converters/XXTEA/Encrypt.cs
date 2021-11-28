// -------------------------------------------------------
// © Kaplas. Licensed under MIT. See LICENSE for details.
// -------------------------------------------------------
namespace GuJian3Library.Converters.XXTEA
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Yarhl.FileFormat;
    using Yarhl.IO;

    /// <summary>
    /// GuJian 3 file encrypter.
    /// </summary>
    public class Encrypt : IInitializer<string>, IConverter<BinaryFormat, BinaryFormat>
    {
        private uint[] _key;

        /// <summary>
        /// Initializes the encryption parameters.
        /// </summary>
        /// <param name="parameters">Encryptor configuration.</param>
        public void Initialize(string parameters)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(parameters);
            _key = new uint[4];
            Buffer.BlockCopy(bytes, 0, _key, 0, 16);
        }

        /// <summary>
        /// Encrypts a BinaryFormat using XXTEA .
        /// </summary>
        /// <param name="source">Binary format.</param>
        /// <returns>The encrypted binary.</returns>
        public BinaryFormat Convert(BinaryFormat source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (_key == null)
            {
                throw new FormatException("Uninitialized key.");
            }

            var result = new BinaryFormat();

            DataStream input = source.Stream;
            DataStream output = result.Stream;
            input.Position = 0;

            byte[] buffer = new byte[0x1000];
            while (!source.Stream.EndOfStream)
            {
                int size = (int)Math.Min(0x1000, input.Length - input.Position);
                int readCount = input.Read(buffer, 0, size);

                EncryptChunk(buffer, readCount, _key);

                output.Write(buffer, 0, readCount);
            }

            return result;
        }

        private static void EncryptChunk(byte[] buffer, int size, IReadOnlyList<uint> key)
        {
            int chunkCount = size / 256;

            for (int i = 0; i < chunkCount; i++)
            {
                uint[] data = new uint[64];
                Buffer.BlockCopy(buffer, i * 256, data, 0, 256);
                EncryptBlock(data, 64, key);
                Buffer.BlockCopy(data, 0, buffer, i * 256, 256);
            }

            // Process the last block
            int lastBlockLength = (size - (chunkCount * 256)) / 4;
            if (lastBlockLength > 1)
            {
                uint[] data = new uint[lastBlockLength];
                Buffer.BlockCopy(buffer, chunkCount * 256, data, 0, lastBlockLength * 4);
                EncryptBlock(data, lastBlockLength, key);
                Buffer.BlockCopy(data, 0, buffer, chunkCount * 256, lastBlockLength * 4);
            }

            // Process the last bytes
            int lastBytesCount = size - (chunkCount * 256) - (lastBlockLength * 4);
            if (lastBytesCount > 0)
            {
                for (int i = size - lastBytesCount; i < size; i++)
                {
                    buffer[i] = (byte)(buffer[i] ^ (size - i) ^ 0xB7);
                }
            }
        }

        // XXTEA algorithm
        // See: https://en.wikipedia.org/wiki/XXTEA
        private static void EncryptBlock(IList<uint> data, int blockLength, IReadOnlyList<uint> key)
        {
            const uint delta = 0x9E3779B9;
            uint rounds = (uint)(6 + (52 / blockLength));
            uint sum = 0;
            uint z = data[blockLength - 1];
            do
            {
                sum += delta;
                uint e = (sum >> 2) & 3;
                for (int p = 0; p < blockLength; p++)
                {
                    uint y = (p < blockLength - 1) ? data[p + 1] : data[0];

                    uint value1 = (z >> 5) ^ (y << 2);
                    uint value2 = (y >> 3) ^ (z << 4);
                    uint value3 = sum ^ y;
                    uint value4 = key[(int)((p & 3) ^ e)] ^ z;

                    data[p] += (value1 + value2) ^ (value3 + value4);

                    z = data[p];
                }

                rounds--;
            }
            while (rounds > 0);
        }
    }
}
