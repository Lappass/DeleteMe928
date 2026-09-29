using System;
using System.Collections.Generic;

namespace FloatingObjects
{
    public enum FloatEffect { VerticalLaunch, HorizontalLaunch, LowGravity, Buoyancy }

    // Kept independent of Unity so the scheduling rules can be tested without a scene.
    public sealed class FloatRandom
    {
        private readonly Random random;
        public FloatRandom(int seed) { random = new Random(seed); }
        public int Index(int count) => random.Next(count);
        public float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();

        // Fill the least represented size and shape independently; ties are random.
        public int ChooseAppearance(int[] sizeCounts, int[] shapeCounts)
        {
            if (sizeCounts.Length != 3 || shapeCounts.Length != 3)
                throw new ArgumentException("Three size and shape counts are required.");
            return LeastRepresented(sizeCounts) * 3 + LeastRepresented(shapeCounts);
        }

        public FloatEffect ChooseEffect(int[] counts)
        {
            if (counts.Length != 4) throw new ArgumentException("Four effect counts are required.");
            return (FloatEffect)LeastRepresented(counts);
        }

        private int LeastRepresented(int[] counts)
        {
            int min = int.MaxValue;
            foreach (int count in counts) min = Math.Min(min, count);
            var choices = new List<int>();
            for (int i = 0; i < counts.Length; i++) if (counts[i] == min) choices.Add(i);
            return choices[random.Next(choices.Count)];
        }
    }
}
