using System;
using BenchmarkDotNet.Attributes;

namespace Multiformats.Hash.Benchmarks
{
    [MemoryDiagnoser]
    public class MultihashSpanBenchmarks
    {
        private byte[] _peerId;

        [GlobalSetup]
        public void Setup()
        {
            byte[] publicKey = new byte[36];
            for (int i = 0; i < publicKey.Length; i++)
                publicKey[i] = (byte)(i * 37 + 11);

            _peerId = Multihash.Encode(publicKey, HashType.ID);
        }

        [Benchmark]
        public int TryDecodeDigest() => Multihash.TryDecode(_peerId, out _, out ReadOnlySpan<byte> digest) ? digest.Length : -1;
    }
}
