using System;

namespace QmcDecoder
{
    internal abstract class BaseCipher
    {
        protected int _offset;

        protected delegate byte GetMaskDelegate();

        protected GetMaskDelegate _getMaskFunc;

        public int Offset => _offset;

        public virtual void Decrypt(Span<byte> buffer)
        {
            for (int i = 0, end = buffer.Length; i < end; i++)
            {
                buffer[i] ^= _getMaskFunc();
            }
        }
    }
}
