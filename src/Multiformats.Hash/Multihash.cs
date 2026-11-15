using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BinaryEncoding;
using Multiformats.Base;
using Multiformats.Hash.Algorithms;

namespace Multiformats.Hash
{
    public class Multihash
    {
        private static readonly Dictionary<HashType, string> _names = CreateNames();
        private static readonly Registry _registry = new Registry();

        public HashType Code { get; }
        public string Name { get; }
        public int Length => Digest?.Length ?? 0;
        public byte[] Digest { get; }

        public static HashType[] SupportedHashCodes => _registry.SupportedHashTypes;

        private byte[] _bytes;
        private readonly bool _encodeOnDemand;

        protected Multihash(byte[] bytes)
        {
            _bytes = bytes;
        }

        protected Multihash(HashType code, byte[] digest)
        {
            Code = code;
            Name = GetName((int)code);
            Digest = digest;
            _encodeOnDemand = true;
        }

        private byte[] Bytes => _bytes ?? (_encodeOnDemand ? EncodeOnce() : null);

        private byte[] EncodeOnce()
        {
            Interlocked.CompareExchange(ref _bytes, Encode(Digest, Code), null);
            return _bytes;
        }

        public string ToString(MultibaseEncoding encoding) => Multibase.EncodeRaw(encoding, Bytes);
        public override string ToString() => ToString(MultibaseEncoding.Base16Lower);

        [Obsolete("Use ToString() instead")]
        public string B58String() => ToString(MultibaseEncoding.Base58Btc);

        [Obsolete("Use ToString() instead")]
        public string ToHexString() => ToString(MultibaseEncoding.Base16Lower);

        public byte[] ToBytes() => Bytes;

        public override bool Equals(object obj)
        {
            var other = (Multihash)obj;
            return other != null && Bytes.SequenceEqual(other.Bytes);
        }

        public override int GetHashCode() => (int)Code ^ Length ^ Digest.Sum(b => b);

        public bool Verify(byte[] data) => Sum(Code, data, Length).Equals(this);
        public Task<bool> VerifyAsync(byte[] data) => SumAsync(Code, data, Length).ContinueWith(mh => mh.Result?.Equals(this) ?? false);

        private static readonly MultibaseEncoding[] _encodings =
        {
            MultibaseEncoding.Base16Lower,
            MultibaseEncoding.Base32Lower,
            MultibaseEncoding.Base58Btc,
            MultibaseEncoding.Base64,
            MultibaseEncoding.Base2,
            MultibaseEncoding.Base8,
        };

        public static bool TryParse(string s, out Multihash mh, out MultibaseEncoding encoding)
        {
            foreach (var _encoding in _encodings)
            {
                try
                {
                    encoding = _encoding;
                    if (TryParse(s, encoding, out mh))
                    {
                        return true;
                    }
                }
                catch (Exception) { }
            }

            mh = null;
            encoding = (MultibaseEncoding)(-1);
            return false;
        }

        public static bool TryParse(string s, out Multihash mh)
        {
            return TryParse(s, out mh, out _);
        }

        public static Multihash Parse(string s)
        {
            if (!TryParse(s, out var mh))
                throw new FormatException("Not a valid multihash");

            return mh;
        }

        public static bool TryParse(string s, MultibaseEncoding encoding, out Multihash mh)
        {
            try
            {
                var bytes = Multibase.DecodeRaw(encoding, s);
                mh = Decode(bytes);
                return true;
            }
            catch (Exception) { }

            mh = null;
            return false;
        }

        public static Multihash Parse(string s, MultibaseEncoding encoding)
        {
            if (!TryParse(s, encoding, out var mh))
                throw new FormatException("Not a valid multihash");

            return mh;
        }

        [Obsolete("Use Parse/TryParse instead")]
        public static Multihash FromHexString(string s) => Cast(Multibase.Base16.Decode(s));

        [Obsolete("Use Parse/TryParse instead")]
        public static Multihash FromB58String(string s) => Cast(Multibase.Base58.Decode(s));

        public static Multihash Cast(byte[] buf) => Decode(buf);

        public static Multihash Decode(byte[] buf)
        {
            if (buf == null)
                throw new ArgumentNullException(nameof(buf));

            if (buf.Length < 2)
                throw new Exception("Too short");

            var offset = Binary.Varint.Read(buf, 0, out uint code);
            offset += Binary.Varint.Read(buf, offset, out uint length);

            if (length > buf.Length - offset)
                throw new Exception("Incosistent length");

            return new Multihash((HashType)code, buf.Slice(offset));
        }

        /// <summary>Reads the hash code and digest of a multihash without allocating; the digest matches <see cref="Decode(byte[])"/>.</summary>
        public static bool TryDecode(ReadOnlySpan<byte> buffer, out HashType code, out ReadOnlySpan<byte> digest)
        {
            code = default;
            digest = default;

            if (buffer.Length < 2)
                return false;

            if (!TryReadVarint(buffer, out uint rawCode, out int codeSize))
                return false;

            if (!TryReadVarint(buffer.Slice(codeSize), out uint length, out int lengthSize))
                return false;

            int offset = codeSize + lengthSize;
            if (length > buffer.Length - offset)
                return false;

            code = (HashType)rawCode;
            digest = buffer.Slice(offset);
            return true;
        }

        private static bool TryReadVarint(ReadOnlySpan<byte> buffer, out uint value, out int size)
        {
            value = 0;
            for (int i = 0; i < 5 && i < buffer.Length; i++)
            {
                byte b = buffer[i];
                if (i == 4 && b > 0x0F)
                    break;

                value |= (uint)(b & 0x7F) << (7 * i);
                if (b < 0x80)
                {
                    size = i + 1;
                    return true;
                }
            }

            value = 0;
            size = 0;
            return false;
        }

        private static int VarintSize(uint value)
        {
            int size = 1;
            while (value >= 0x80)
            {
                value >>= 7;
                size++;
            }

            return size;
        }

        private static int WriteVarint(byte[] buffer, int offset, uint value)
        {
            while (value >= 0x80)
            {
                buffer[offset++] = (byte)(value | 0x80);
                value >>= 7;
            }

            buffer[offset++] = (byte)value;
            return offset;
        }

        private static byte[] Encode(uint code, byte[] data)
        {
            uint length = (uint)data.Length;
            byte[] result = new byte[VarintSize(code) + VarintSize(length) + data.Length];
            int offset = WriteVarint(result, 0, code);
            offset = WriteVarint(result, offset, length);
            Buffer.BlockCopy(data, 0, result, offset, data.Length);
            return result;
        }

        public static byte[] Encode(byte[] data, HashType code) => Encode((uint)code, data);
        public static Multihash Encode(string s, HashType code) => Encode(Multibase.Base32.Decode(s), code);
        public static byte[] Encode<TAlgorithm>(byte[] data) where TAlgorithm : IMultihashAlgorithm
        {
            var algo = Registry.GetHashType<TAlgorithm>();
            if (!algo.HasValue)
                throw new NotSupportedException($"{typeof(TAlgorithm)} is not supported.");

            return Encode((uint)algo.Value, data);
        }

        private static Multihash Sum(IMultihashAlgorithm algo, byte[] data, int length) => new Multihash(algo.Code, algo.ComputeHash(data, length).Slice(0, length != -1 ? length : algo.DefaultLength));
        public static Multihash Sum(HashType code, byte[] data, int length = -1) => _registry.Use(code, algo => Sum(algo, data, length));
        public static Multihash Sum<TAlgorithm>(byte[] data, int length = -1) where TAlgorithm : IMultihashAlgorithm => _registry.Use<TAlgorithm, Multihash>(algo => Sum(algo, data, length));
        private static Task<Multihash> SumAsync(IMultihashAlgorithm algo, byte[] data, int length) => algo.ComputeHashAsync(data, length).ContinueWith(t => new Multihash(algo.Code, t.Result.Slice(0, length != -1 ? length : algo.DefaultLength)));
        public static Task<Multihash> SumAsync(HashType type, byte[] data, int length = -1) => _registry.UseAsync(type, algo => SumAsync(algo, data, length));
        public static Task<Multihash> SumAsync<TAlgorithm>(byte[] data, int length = -1) where TAlgorithm : IMultihashAlgorithm => _registry.UseAsync<TAlgorithm, Multihash>(algo => SumAsync(algo, data, length));

        public static implicit operator Multihash(byte[] buf) => Decode(buf);
        public static implicit operator byte[](Multihash mh) => mh.Bytes;
        public static implicit operator Multihash(string s) => Parse(s);
        public static implicit operator string(Multihash mh) => mh.ToString(MultibaseEncoding.Base16Lower);

        private static string FormatName(HashType code) => code.ToString().Replace("_", "-").ToLower();

        private static Dictionary<HashType, string> CreateNames()
        {
            Dictionary<HashType, string> names = new Dictionary<HashType, string>();
            foreach (HashType code in Enum.GetValues<HashType>())
                names[code] = FormatName(code);
            return names;
        }

        public static string GetName(HashType code) => _names.TryGetValue(code, out string name) ? name : FormatName(code);
        public static string GetName(int code) => _names.TryGetValue((HashType)code, out string name) ? name : "unsupported";

        public static HashType? GetCode(string name) => Enum.TryParse(name.Replace("-", "_"), true, out HashType result) ? result : new HashType?();
    }
}
