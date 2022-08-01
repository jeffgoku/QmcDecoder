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

		static public byte[] DecryptKey(ReadOnlySpan<char> rawKey)
		{
			const string prefixStr = "QQMusic EncV2,Key:";

			int i = rawKey.Length - 1;
			while (rawKey[i] == 0)
			{
				--i;
			}
			int keyLen = i + 1;
			int padding = 0;
			while (rawKey[i] == '=')
			{
				--i;
				padding++;
			}

			Span<byte> rawKeyDec = new byte[3 * (keyLen / 4) - padding];
			if (!Convert.TryFromBase64Chars(rawKey, rawKeyDec, out keyLen))
			{
				throw new ArgumentException("key is not base64 encoded");
			}

			if (keyLen > prefixStr.Length)
			{
				Span<byte> prefixBytes = stackalloc byte[prefixStr.Length];
				int n = System.Text.Encoding.UTF8.GetBytes(prefixStr, prefixBytes);
				if (n == prefixBytes.Length && prefixBytes.SequenceCompareTo(rawKeyDec[..n]) == 0)
					rawKeyDec = rawKeyDec[n..];
			}

			var simpleKey = simpleMakeKey(106, 8);
			Span<byte> teaKey = stackalloc byte[16];
			for (i = 0; i < 8; i++)
			{
				teaKey[i << 1] = simpleKey[i];
				teaKey[(i << 1) + 1] = rawKeyDec[i];
			}

			var rs = decryptTencentTea(rawKeyDec[8..], teaKey);

			var ret = new byte[8 + rs.Length];
			rawKeyDec[..8].CopyTo(ret);
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
