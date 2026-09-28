using System;
using System.Collections.Generic;

namespace FloatingObjects
{
    public enum FloatEffect { VerticalLaunch, HorizontalLaunch, LowGravity, Buoyancy }

    // Kept independent of Unity so the scheduling rules can be tested without a scene.
    public sealed class FloatRandom
    {
        private readonly Random random;
        private readonly List<FloatEffect> bag = new List<FloatEffect>();
        private readonly Queue<FloatEffect> history = new Queue<FloatEffect>();
        private FloatEffect? previous;
        private int previousDirection = -1;
        public FloatEffect[] RecentEffects => history.ToArray();

        public FloatRandom(int seed) { random = new Random(seed); }
        public int Index(int count) => random.Next(count);
        public float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();

        public FloatEffect NextEffect()
        {
            if (bag.Count == 0)
            {
                for (int i = 0; i < 4; i++) bag.Add((FloatEffect)i);
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    FloatEffect temp = bag[i]; bag[i] = bag[j]; bag[j] = temp;
                }
                if (previous.HasValue && bag[0] == previous.Value)
                {
                    int j = random.Next(1, 4);
                    FloatEffect temp = bag[0]; bag[0] = bag[j]; bag[j] = temp;
                }
            }
            FloatEffect result = bag[0];
            bag.RemoveAt(0);
            previous = result;
            history.Enqueue(result);
            if (history.Count > 4) history.Dequeue();
            return result;
        }

        // Direction sectors stay different even after inward boundary bias.
        public int NextDirection(int[] allowed)
        {
            var choices = new List<int>();
            foreach (int direction in allowed)
                if (direction != previousDirection) choices.Add(direction);
            if (choices.Count == 0) throw new ArgumentException("Provide at least two distinct direction sectors.");
            previousDirection = choices[random.Next(choices.Count)];
            return previousDirection;
        }

        // Fill the least represented size and shape independently; ties are random.
        public int ChooseAppearance(int[] sizeCounts, int[] shapeCounts)
        {
            if (sizeCounts.Length != 3 || shapeCounts.Length != 3)
                throw new ArgumentException("Three size and shape counts are required.");
            return LeastRepresented(sizeCounts) * 3 + LeastRepresented(shapeCounts);
        }

        private int LeastRepresented(int[] counts)
        {
            int min = Math.Min(counts[0], Math.Min(counts[1], counts[2]));
            var choices = new List<int>();
            for (int i = 0; i < 3; i++) if (counts[i] == min) choices.Add(i);
            return choices[random.Next(choices.Count)];
        }
    }
}
