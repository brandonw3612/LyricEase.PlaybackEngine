using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyricEase.PlaybackEngine
{
    internal static class Methods
    {
        private static Random _random = new Random(Guid.NewGuid().GetHashCode());

        internal static List<int> GenerateAscendingSequence(int Count)
        {
            List<int> seq = new();
            for (int i = 0; i < Count; i++) seq.Add(i);
            return seq;
        }

        internal static List<int> GenerateShuffledSequence(int Count)
        {
            List<int> seq = GenerateAscendingSequence(Count);
            seq.Sort((_1, _2) => (_random.NextDouble() - 0.5d).CompareTo(0d));
            return seq;
        }

        internal static int GetPreviousIndex(int CurrentIndex, int CollectionLength) => (CurrentIndex - 1 + CollectionLength) % CollectionLength;

        internal static int GetNextIndex(int CurrentIndex, int CollectionLength) => (CurrentIndex + 1) % CollectionLength;
    }
}
