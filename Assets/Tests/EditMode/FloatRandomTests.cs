using System;
using System.Collections.Generic;
using FloatingObjects;
using NUnit.Framework;

public class FloatRandomTests
{
    [Test]
    public void EffectCountsStayBalancedAcrossSpawnsAndReplacements()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var random = new FloatRandom(seed);
            var counts = new int[4];
            var population = new List<FloatEffect>();
            for (int spawn = 0; spawn < 100; spawn++)
            {
                if (population.Count == 8)
                {
                    int index = random.Index(population.Count);
                    counts[(int)population[index]]--;
                    population.RemoveAt(index);
                }
                FloatEffect next = random.ChooseEffect(counts);
                population.Add(next);
                counts[(int)next]++;
                Assert.That(MaxDifference(counts), Is.LessThanOrEqualTo(1));
            }
        }
    }

    [Test]
    public void AppearanceCountsRemainBalancedAfterArbitraryConsumption()
    {
        var random = new FloatRandom(928);
        var size = new int[3];
        var shape = new int[3];
        var population = new List<int>();
        for (int step = 0; step < 2000; step++)
        {
            if (population.Count == 8)
            {
                int index = random.Index(population.Count);
                int removed = population[index];
                population.RemoveAt(index);
                size[removed / 3]--; shape[removed % 3]--;
            }
            int appearance = random.ChooseAppearance(size, shape);
            population.Add(appearance);
            size[appearance / 3]++; shape[appearance % 3]++;
            Assert.That(MaxDifference(size), Is.LessThanOrEqualTo(1));
            Assert.That(MaxDifference(shape), Is.LessThanOrEqualTo(1));
        }
    }

    [Test]
    public void SameSeedReproducesMixedRandomOperations()
    {
        var first = new FloatRandom(928);
        var second = new FloatRandom(928);
        for (int i = 0; i < 1000; i++)
        {
            Assert.That(first.ChooseEffect(new[] { 2, 1, 2, 2 }), Is.EqualTo(second.ChooseEffect(new[] { 2, 1, 2, 2 })));
            Assert.That(first.Range(.6f, 1.8f), Is.EqualTo(second.Range(.6f, 1.8f)));
            Assert.That(first.ChooseAppearance(new[] { 2, 3, 2 }, new[] { 3, 2, 2 }),
                Is.EqualTo(second.ChooseAppearance(new[] { 2, 3, 2 }, new[] { 3, 2, 2 })));
        }
    }

    private static int MaxDifference(int[] counts)
    {
        int min = int.MaxValue, max = int.MinValue;
        foreach (int count in counts) { min = Math.Min(min, count); max = Math.Max(max, count); }
        return max - min;
    }
}
