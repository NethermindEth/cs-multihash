using System;
using System.Linq;
using BinaryEncoding;
using Xunit;

namespace Multiformats.Hash.Tests
{
    public class EncodingCompatibilityTests
    {
        private static string LegacyName(HashType code) => code.ToString().Replace("_", "-").ToLower();

        private static string LegacyName(int code) => Enum.IsDefined(typeof(HashType), (HashType)code) ? LegacyName((HashType)code) : "unsupported";

        private static byte[] LegacyEncode(byte[] data, HashType code) =>
            Binary.Varint.GetBytes((uint)code).Concat(Binary.Varint.GetBytes((uint)data.Length)).Concat(data).ToArray();

        [Fact]
        public void GetName_MatchesLegacyForAllCodes()
        {
            foreach (HashType code in Enum.GetValues<HashType>())
            {
                Assert.Equal(LegacyName(code), Multihash.GetName(code));
                Assert.Equal(LegacyName((int)code), Multihash.GetName((int)code));
            }

            foreach (int code in new[] { -1, 1, 0x7f, 0x80, 0x3fff, 0x4000, int.MaxValue, int.MinValue })
            {
                Assert.Equal(LegacyName((HashType)code), Multihash.GetName((HashType)code));
                Assert.Equal(LegacyName(code), Multihash.GetName(code));
            }
        }

        [Fact]
        public void Encode_MatchesLegacyForVarintBoundaries()
        {
            Random random = new Random(1115);
            uint[] codes = { 0, 1, 0x7f, 0x80, 0x3fff, 0x4000, 0x1fffff, 0x200000, 0xfffffff, 0x10000000, uint.MaxValue };
            int[] lengths = { 0, 1, 32, 127, 128, 300, 16384 };
            foreach (uint code in codes)
            {
                foreach (int length in lengths)
                {
                    byte[] data = new byte[length];
                    random.NextBytes(data);
                    Assert.Equal(LegacyEncode(data, (HashType)code), Multihash.Encode(data, (HashType)code));
                }
            }
        }

        [Fact]
        public void ToBytes_AndName_MatchLegacyAfterDecode()
        {
            Random random = new Random(511);
            foreach (HashType code in new[] { HashType.ID, HashType.SHA2_256, HashType.BLAKE2B_256, HashType.KECCAK_512, (HashType)0x3fff })
            {
                foreach (int length in new[] { 0, 1, 32, 36, 200 })
                {
                    byte[] data = new byte[length];
                    random.NextBytes(data);
                    byte[] encoded = LegacyEncode(data, code);
                    Multihash mh = Multihash.Decode(encoded);

                    Assert.Equal(encoded, mh.ToBytes());
                    Assert.Same(mh.ToBytes(), mh.ToBytes());
                    Assert.Equal(LegacyName((int)code), mh.Name);
                    Assert.Equal(data, mh.Digest);
                }
            }
        }

        [Fact]
        public void TryDecode_MatchesDecode()
        {
            Random random = new Random(42);
            foreach (HashType code in new[] { HashType.ID, HashType.SHA2_256, HashType.BLAKE2B_256, (HashType)0x3fff, (HashType)uint.MaxValue })
            {
                foreach (int length in new[] { 0, 1, 32, 36, 200 })
                {
                    byte[] data = new byte[length];
                    random.NextBytes(data);
                    byte[] encoded = LegacyEncode(data, code);
                    byte[] withTail = encoded.Concat(new byte[] { 1, 2, 3 }).ToArray();

                    foreach (byte[] buffer in new[] { encoded, withTail })
                    {
                        Multihash mh = Multihash.Decode(buffer);
                        Assert.True(Multihash.TryDecode(buffer, out HashType decodedCode, out ReadOnlySpan<byte> digest));
                        Assert.Equal(mh.Code, decodedCode);
                        Assert.Equal(mh.Digest, digest.ToArray());
                    }
                }
            }
        }

        [Fact]
        public void TryDecode_RejectsMalformedInput()
        {
            Assert.False(Multihash.TryDecode(ReadOnlySpan<byte>.Empty, out _, out _));
            Assert.False(Multihash.TryDecode(new byte[] { 0x12 }, out _, out _));
            Assert.False(Multihash.TryDecode(new byte[] { 0x12, 0x20, 1, 2 }, out _, out _));
            Assert.False(Multihash.TryDecode(new byte[] { 0x80, 0x80 }, out _, out _));
            Assert.False(Multihash.TryDecode(new byte[] { 0x12, 0x80 }, out _, out _));
            Assert.False(Multihash.TryDecode(new byte[] { 0xff, 0xff, 0xff, 0xff, 0x7f, 0x00 }, out _, out _));
        }
    }
}
