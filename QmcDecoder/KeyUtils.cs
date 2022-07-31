using System;

namespace QmcDecoder
{
	internal static class KeyUtils
	{
		static byte[] simpleMakeKey(byte salt, int length)
		{
			var keyBuf = new byte[length];
			for (int i = 0; i < length; i++)
			{
				var tmp = Math.Tan(salt + i * 0.1);
				keyBuf[i] = (byte)(Math.Abs(tmp) * 100.0);
			}
			return keyBuf;
		}

		static public byte[] DecryptKey(string rawKey)
		{
			var rawKeyDec = Convert.FromBase64String(rawKey);

			const int skipLen = 18; // the length of "QQMusic EncV2,Key:"

			var simpleKey = simpleMakeKey(106, 8);
			Span<byte> teaKey = stackalloc byte[16];
			for (int i = 0; i < 8; i++)
			{
				teaKey[i << 1] = simpleKey[i];
				teaKey[(i << 1) + 1] = rawKeyDec[skipLen + i];
			}

			var rs = decryptTencentTea(new Span<byte>(rawKeyDec, skipLen + 8, rawKeyDec.Length - skipLen - 8), teaKey);

			var ret = new byte[8 + rs.Length];
			Array.Copy(rawKeyDec, skipLen, ret, 0, 8);
			Array.Copy(rs, 0, ret, 8, rs.Length);
			return ret;
		}

		static byte[] decryptTencentTea(Span<byte> inBuf, Span<byte> key)
		{
			const int saltLen = 2;
			const int zeroLen = 7;
			if (inBuf.Length % 8 != 0)
			{
				throw new ArgumentException("inBuf size not a multiple of the block size");
			}
			if (inBuf.Length < 16)
			{
				throw new ArgumentException("inBuf size too small");
			}

			var tea = new TEA(key, 32);

			Span<byte> destBuf = stackalloc byte[8];
			tea.Decrypt(inBuf, destBuf);
			var padLen = destBuf[0] & 0x7;
			var outLen = inBuf.Length - 1 - padLen - saltLen - zeroLen;
			if (padLen + saltLen != 8)
			{
				throw new InvalidOperationException("invalid pad len");
			}
			var outBuf = new byte[outLen];

			int ivPrev = 0;

			var inBufPos = 8;

			var destIdx = 1 + padLen;

			for (int i = 1; i <= saltLen;)
			{
				if (destIdx < 8)
				{
					destIdx++;
					i++;
				}
				else if (destIdx == 8)
				{
					for (int j = 0; j < 8; ++j)
					{
						destBuf[j] ^= inBuf[inBufPos + j];
					}
					tea.Decrypt(destBuf, destBuf);
					ivPrev = inBufPos - 8;
					inBufPos += 8;
					destIdx = 0;
				}
			}

			var outPos = 0;
			while (outPos < outLen)
			{
				if (destIdx < 8)
				{
					outBuf[outPos] = (byte)(destBuf[destIdx] ^ inBuf[ivPrev + destIdx]);
					destIdx++;
					outPos++;
				}
				else if (destIdx == 8)
				{
					for (int j = 0; j < 8; ++j)
					{
						destBuf[j] ^= inBuf[inBufPos + j];
					}
					tea.Decrypt(destBuf, destBuf);
					ivPrev = inBufPos - 8;
					inBufPos += 8;
					destIdx = 0;
				}
			}

			for (int i = 1; i <= zeroLen; i++)
			{
				if (destBuf[destIdx] != inBuf[ivPrev + destIdx])
				{
					throw new InvalidOperationException("zero check failed");
				}
			}

			return outBuf;
		}
	}
}
