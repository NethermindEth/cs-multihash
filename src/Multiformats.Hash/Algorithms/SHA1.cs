using System;
using System.Composition;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;

namespace Multiformats.Hash.Algorithms
{
    [Export(typeof(IMultihashAlgorithm))]
    [MultihashAlgorithmExport(HashType.SHA1, "sha1", 20)]
    public class SHA1 : MultihashAlgorithm
    {
        private readonly Func<IDigest> _factory;

        public SHA1()
            : base(HashType.SHA1, "sha1", 20)
        {
            _factory = () => new Sha1Digest();
        }

        public override byte[] ComputeHash(byte[] data, int length = -1) => _factory().ComputeHash(data);
    }
}
