using BenchmarkDotNet.Attributes;

namespace Multiformats.Hash.Benchmarks
{
    [MemoryDiagnoser]
    public class MultihashBenchmarks
    {
        private byte[] _publicKey;
        private byte[] _peerId;

        [GlobalSetup]
        public void Setup()
        {
            _publicKey = new byte[36];
            _publicKey[0] = 0x08;
            _publicKey[1] = 0x01;
            _publicKey[2] = 0x12;
            _publicKey[3] = 0x20;
            for (int i = 4; i < _publicKey.Length; i++)
                _publicKey[i] = (byte)(i * 37 + 11);

            _peerId = Multihash.Encode(_publicKey, HashType.ID);
        }

        [Benchmark]
        public byte[] Encode() => Multihash.Encode(_publicKey, HashType.ID);

        [Benchmark]
        public byte[] DecodeDigest() => Multihash.Decode(_peerId).Digest;

        [Benchmark]
        public byte[] DecodeToBytes() => Multihash.Decode(_peerId).ToBytes();

        [Benchmark]
        public string GetName() => Multihash.GetName((int)HashType.SHA2_256);
    }
}
