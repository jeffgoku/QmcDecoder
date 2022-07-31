using System;

namespace QmcDecoder
{
    internal readonly struct TEA
    {
        // BlockSize is the size of a TEA block, in bytes.
        const int BlockSize = 8;
        // KeySize is the size of a TEA key, in bytes.
        const int KeySize = 16;
        // delta is the TEA key schedule constant.
        const uint delta = 0x9e3779b9;
        const int kNumRound = 64;

        readonly uint k0, k1, k2, k3;
        readonly int _round;

        public TEA(Span<byte> key) : this(key, kNumRound)
        {
        }

        public TEA(Span<byte> key, int round)
        {
            _round = round;
            k0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(key[..4]);
            k1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(key[4..8]);
            k2 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(key[8..12]);
            k3 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(key[12..]);
        }

        public void Encrypt(Span<byte> src, Span<byte> dst)
        {
            var v0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(src);
            var v1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(src[4..]);

            uint sum = 0;

            int round = _round >> 1;

            for (int i = 0; i < round; i++) {
                sum += delta;

                v0 += ((v1 << 4) + k0) ^ (v1 + sum) ^ ((v1 >> 5) + k1);
                v1 += ((v0 << 4) + k2) ^ (v0 + sum) ^ ((v0 >> 5) + k3);
            }

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(dst, v0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(dst[4..], v1);
        }

        public void Decrypt(Span<byte> src, Span<byte> dst)
        {
            var v0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(src);
            var v1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(src[4..]);

            int round = _round >> 1;
            uint sum = (uint)(delta * round); // in general, sum = delta * n

            for (int i = 0; i < round; i++)
            {
                v1 -= ((v0 << 4) + k2) ^ (v0 + sum) ^ ((v0 >> 5) + k3);
                v0 -= ((v1 << 4) + k0) ^ (v1 + sum) ^ ((v1 >> 5) + k1);
                
                sum -= delta;
            }
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(dst, v0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(dst[4..], v1);
        }
    }
}
