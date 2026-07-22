using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    /// <summary>
    /// Explicit random source for all Core probability calculations. Callers own
    /// seed selection and transaction identity; Core never reaches into Unity APIs.
    /// </summary>
    public interface IRandomSource
    {
        double NextDouble();
        int NextIntInclusive(int min, int max);
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SystemRandomSource(int seed) => _random = new Random(seed);
        public SystemRandomSource() => _random = new Random();

        public double NextDouble() => _random.NextDouble();

        public int NextIntInclusive(int min, int max)
        {
            if (min > max) throw new ArgumentOutOfRangeException(nameof(min));
            if (max == int.MaxValue)
                return min + (int)Math.Floor(NextDouble() * ((long)max - min + 1));
            return _random.Next(min, max + 1);
        }
    }

    /// <summary>Deterministic test source that cycles through supplied values.</summary>
    public sealed class SequenceRandomSource : IRandomSource
    {
        private readonly IReadOnlyList<double> _values;
        private int _index;

        public SequenceRandomSource(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0)
                throw new ArgumentException("At least one value is required.", nameof(values));
            _values = values;
        }

        public double NextDouble()
        {
            double value = _values[_index++ % _values.Count];
            if (value < 0.0 || value >= 1.0)
                throw new InvalidOperationException("Sequence values must be in [0, 1).");
            return value;
        }

        public int NextIntInclusive(int min, int max)
        {
            if (min > max) throw new ArgumentOutOfRangeException(nameof(min));
            return min + (int)Math.Floor(NextDouble() * ((long)max - min + 1));
        }
    }
}
