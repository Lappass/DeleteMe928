using System;
using System.Collections.Generic;
using FloatingObjects;
using NUnit.Framework;

public class FloatRandomTests
{
    [Test]
    public void EveryBagContainsAllEffectsAndBoundariesNeverRepeat()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var random = new FloatRandom(seed);
            FloatEffect? previous = null;
            for (int bag = 0; bag < 100; bag++)
            {
                var seen = new HashSet<FloatEffect>();
                for (int i = 0; i < 4; i++)
                {
                    FloatEffect next = random.NextEffect();
                    Assert.That(next, Is.Not.EqualTo(previous));
                    Assert.That(seen.Add(next), Is.True);
                    previous = next;
                }
            }
            Assert.That(random.RecentEffects.Length, Is.EqualTo(4));
        }
    }

    [Test]
    public void HistoryContainsOnlyLastFourEffectsInOrder()
    {
        var random = new FloatRandom(928);
        for (int i = 0; i < 12; i++) random.NextEffect();
        var expected = new FloatEffect[4];
        for (int i = 0; i < 4; i++) expected[i] = random.NextEffect();
        Assert.That(random.RecentEffects, Is.EqualTo(expected));
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
    public void DirectionNeverRepeatsAndRespectsAllowedSectors()
    {
        var random = new FloatRandom(928);
        int previous = -1;
        for (int i = 0; i < 2000; i++)
        {
            int[] allowed = i % 2 == 0 ? new[] { 0, 1, 7 } : new[] { 3, 4, 5 };
            int direction = random.NextDirection(allowed);
            Assert.That(direction, Is.Not.EqualTo(previous));
            Assert.That(allowed, Does.Contain(direction));
            previous = direction;
            direction = random.NextDirection(allowed);
            Assert.That(direction, Is.Not.EqualTo(previous));
            previous = direction;
        }
    }

    [Test]
    public void SameSeedReproducesMixedRandomOperations()
    {
        var first = new FloatRandom(928);
        var second = new FloatRandom(928);
        for (int i = 0; i < 1000; i++)
        {
            Assert.That(first.NextEffect(), Is.EqualTo(second.NextEffect()));
            Assert.That(first.Range(.6f, 1.8f), Is.EqualTo(second.Range(.6f, 1.8f)));
            Assert.That(first.NextDirection(new[] { 0, 1, 2 }), Is.EqualTo(second.NextDirection(new[] { 0, 1, 2 })));
            Assert.That(first.ChooseAppearance(new[] { 2, 3, 2 }, new[] { 3, 2, 2 }),
                Is.EqualTo(second.ChooseAppearance(new[] { 2, 3, 2 }, new[] { 3, 2, 2 })));
        }
    }

    private static int MaxDifference(int[] counts) =>
        Math.Max(counts[0], Math.Max(counts[1], counts[2])) - Math.Min(counts[0], Math.Min(counts[1], counts[2]));
}
