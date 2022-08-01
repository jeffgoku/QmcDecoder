using System;

namespace QmcDecoder
{
    using static KeyUtils;
    internal class QmcDecoder2
    {
        string fileExt;

        int audioLen;
        byte[] decodedKey;
        BaseCipher cipher;
        int offset;

        int rawMetaExtra1;
        int rawMetaExtra2;

        readonly System.IO.Stream _inputData;

        public int LeftDataSize => audioLen - cipher.Offset;

        public QmcDecoder2(System.IO.Stream stream)
        {
            _inputData = stream;
            searchKey();
            if (decodedKey == null)
            {
                cipher = new NewStaticCipher();
            }
            else if (decodedKey.Length > 300)
            {
                cipher = new RC4Cipher(decodedKey);
            }
            else if (decodedKey.Length != 0)
            {
                cipher = new MapCipher(decodedKey);
            }
            else
            {
                throw new InvalidOperationException("unknown file format");
            }

            _inputData.Seek(0, System.IO.SeekOrigin.Begin);
        }

        public int Decode(Span<byte> data)
        {
            int n = LeftDataSize;
            if (n <= 0)
                return 0;

            if (n < data.Length)
            {
                data = data[..n];
            }
            n = _inputData.Read(data);
            cipher.Decrypt(data[..n]);
            return n;
        }

        void searchKey()
        {
            var fileSizeM4 = _inputData.Seek(-4, System.IO.SeekOrigin.End);

            Span<byte> buf = stackalloc byte[4];
            _inputData.Read(buf);

            if (buf[0] == 'Q' && buf[1] == 'T' && buf[2] == 'a' && buf[3] == 'g')
            {
                readRawMetaQTag();
            }
            else
            {
                var size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(buf);

                if (size < 0x300 && size != 0)
                {
                    readRawKey(size);
                }
                else
                {
                    // try to use default static cipher
                    audioLen = (int)(fileSizeM4 + 4);
                }
            }
        }

        void readRawKey(long rawKeyLen)
        {
            audioLen = (int)_inputData.Seek(-(4 + rawKeyLen), System.IO.SeekOrigin.End);

            byte[] rawKeyData = new byte[rawKeyLen];
            _inputData.Read(rawKeyData);

            //under windows the last byte of rawKeyData is 0
            string strKey = System.Text.Encoding.UTF8.GetString(rawKeyData, 0, (int)(rawKeyData[rawKeyLen - 1] == 0 ? rawKeyLen - 1 : rawKeyLen));
            //rawKeyData = null;
            decodedKey = DecryptKey(strKey);
        }

        void readRawMetaQTag()
        {
            // get raw meta data len
            _inputData.Seek(-8, System.IO.SeekOrigin.End);

            Span<byte> buf = stackalloc byte[4];
            _inputData.Read(buf);

            long rawMetaLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(buf);

            // read raw meta data
            audioLen = (int)_inputData.Seek(-(8 + rawMetaLen), System.IO.SeekOrigin.End);

            byte[] rawMetaData = new byte[rawMetaLen];
            _inputData.Read(rawMetaData);

            string metaData = System.Text.Encoding.UTF8.GetString(rawMetaData);
            var items = metaData.Split(',');

            if (items.Length != 3) {
                throw new InvalidOperationException("invalid raw meta data length");
            }

            decodedKey = DecryptKey(items[0]);

            rawMetaExtra1 = int.Parse(items[1]);
            rawMetaExtra2 = int.Parse(items[2]);
        }
    }
}
