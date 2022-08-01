using System;

namespace QmcDecoder
{
    internal class MapCipher : BaseCipher
    {
        readonly byte[] _key;

        public MapCipher(byte[] key)
        {
            _key = new byte[key.Length];
            Array.Copy(key, _key, key.Length);

            _getMaskFunc = getMask;
        }

        byte getMask()
        {
            var offset = _offset++;
            offset %= 0x7FFF;

            var idx = (offset * offset + 71214) % _key.Length;

            return rotate(_key[idx], (byte)(idx & 0x7));
        }

        byte rotate(byte value, byte bits) {
            var rotate = (bits + 4) % 8;
            var left = value << rotate;
            var right = value >> rotate;
            return (byte)(left | right);
        }
    }
}
