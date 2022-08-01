using System;

namespace QmcDecoder
{
    internal class RC4Cipher : BaseCipher
    {
        const int rc4SegmentSize = 5120;
        const int rc4FirstSegmentSize = 128;

        readonly byte[] _key;
        readonly byte[] _box;
        uint _hash;

        public RC4Cipher(byte[] key)
        {
            int n = key.Length;
            _key = new byte[n];
            _box = new byte[n << 1]; // second half used for temp box data in endASegment
            Array.Copy(key, _key, n);

            for (int i = 0; i < n; ++i)
            {
                _box[i] = (byte)i;
            }

            var j = 0;
            for (int i = 0; i < n; i++)
            {
                j = (j + _box[i] + key[i % n]) % n;

                (_box[i], _box[j]) = (_box[j], _box[i]);
            }
            getHashBase();
        }

        void getHashBase()
        {
            _hash = 1;

            for (int i = 0; i < _key.Length; i++)
            {
                var v = (uint)_key[i];

                if (v == 0)
                {
                    continue;
                }
                var nextHash = _hash * v;

                if (nextHash == 0 || nextHash <= _hash)
                {
                    break;

                }
                _hash = nextHash;
            }
        }

        public override void Decrypt(Span<byte> src)
        {
            var toProcess = src.Length;
            var processed = 0;

            if (_offset < rc4FirstSegmentSize)
            {
                var blockSize = toProcess;

                if (blockSize > rc4FirstSegmentSize - _offset)
                {
                    blockSize = rc4FirstSegmentSize - _offset;

                }
                encFirstSegment(src[..blockSize], _offset);
                _offset += blockSize;
                toProcess -= blockSize;
                processed += blockSize;

                if (toProcess == 0)
                {
                    return;
                }
            }

            if (_offset % rc4SegmentSize != 0)
            {
                var blockSize = toProcess;

                if (blockSize > rc4SegmentSize - _offset % rc4SegmentSize)
                {
                    blockSize = rc4SegmentSize - _offset % rc4SegmentSize;
                }
                encASegment(src[processed..(processed + blockSize)], _offset);

                _offset += blockSize;
                toProcess -= blockSize;
                processed += blockSize;

                if (toProcess == 0)
                {
                    return;
                }
            }
            while (toProcess > rc4SegmentSize)
            {
                encASegment(src[processed..(processed + rc4SegmentSize)], _offset);

                _offset += rc4SegmentSize;
                toProcess -= rc4SegmentSize;
                processed += rc4SegmentSize;
            }

            if (toProcess > 0)
            {
                encASegment(src[processed..], _offset);
                _offset += toProcess;
            }
        }

        void encFirstSegment(Span<byte> buf, int offset)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] ^= _key[getSegmentSkip(offset + i)];
            }
        }

        void encASegment(Span<byte> buf, int offset)
        {
            int n = _key.Length;
            Span<byte> tempBox = new (_box, n, n);
            new Span<byte>(_box, 0, n).CopyTo(tempBox);

            int j = 0, k = 0;

            var skipLen = (offset % rc4SegmentSize) + getSegmentSkip(offset / rc4SegmentSize);

            for (int i = -skipLen; i < buf.Length; i++)
            {
                j = (j + 1) % n;

                k = (tempBox[j] + k) % n;

                (tempBox[j], tempBox[k]) = (tempBox[k], tempBox[j]);

                if (i >= 0)
                {
                    buf[i] ^= tempBox[(tempBox[j] + tempBox[k]) % n];
                }
            }
        }
        int getSegmentSkip(int id)
        {
            var seed = (int)_key[id % _key.Length];
            var idx = (long)(_hash / (double)((id + 1) * seed) * 100.0);

            return (int)(idx % _key.Length);
        }
    }
}
