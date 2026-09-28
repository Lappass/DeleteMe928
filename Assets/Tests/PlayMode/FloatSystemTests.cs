using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// The existing game uses Assembly-CSharp. Test assemblies cannot reference that
// predefined assembly, so this fixture accesses its components through reflection.
public class FloatSystemTests
{
    private Component player;
    private Component experience;
    private Component[] areas;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type GameType(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static Component[] Find(string name) => Object.FindObjectsByType(GameType(name)).Cast<Component>().ToArray();
    private static object Call(Component target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Public | Fields).Invoke(target, args);
    private static T Get<T>(Component target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
    private static void Set(Component target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return SceneManager.LoadSceneAsync("PlatformerLevel");
        yield return null;
        player = Find("PlayerMovement").Single();
        experience = player.GetComponent(GameType("FloatExperience"));
        ((Behaviour)player).enabled = false;
        Teleport(new Vector3(-10, 10, 0));
        areas = Find("FloatArea");
        Assert.That(areas.Length, Is.EqualTo(3));
        float deadline = Time.time + 8;
        while (areas.Sum(a => Get<int>(a, "ActiveCount")) < 24 && Time.time < deadline) yield return null;
        Assert.That(areas.Sum(a => Get<int>(a, "ActiveCount")), Is.EqualTo(24));
    }

    [UnityTest]
    public IEnumerator GeneratedObjectsRespectReservationsAndAppearanceQuotas()
    {
        Component[] floats = Find("Float");
        foreach (Component item in floats)
        {
            float radius = Get<float>(item, "ReservationRadius");
            Vector3 anchor = Get<Vector3>(item, "Anchor");
            Assert.That(Physics.CheckSphere(anchor, radius, ~0, QueryTriggerInteraction.Ignore), Is.False);
            Assert.That(item.GetComponent<MeshCollider>().isTrigger, Is.True);
            Assert.That(item.GetComponent<MeshCollider>().convex, Is.True);
            foreach (Component other in floats)
            {
                if (item == other) continue;
                Assert.That(Vector3.Distance(anchor, Get<Vector3>(other, "Anchor")),
                    Is.GreaterThanOrEqualTo(radius + Get<float>(other, "ReservationRadius") - .001f));
            }
        }
        foreach (Component area in areas)
        {
            var appearances = area.GetComponentsInChildren(GameType("Float")).Cast<Component>()
                .Select(f => Get<int>(f, "Appearance")).ToArray();
            for (int dimension = 0; dimension < 2; dimension++)
            {
                int[] counts = new int[3];
                foreach (int appearance in appearances) counts[dimension == 0 ? appearance / 3 : appearance % 3]++;
                Assert.That(counts.Max() - counts.Min(), Is.LessThanOrEqualTo(1));
            }
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator ContactIsSingleUseCooldownRetriesAndPoolReplenishes()
    {
        Component[] floats = Find("Float");
        Component first = floats[0], second = floats[1];
        Vector3 previous = Get<Vector3>(first, "Anchor");
        Teleport(first.transform.position);
        yield return new WaitForSeconds(.08f);
        Assert.That(Get<bool>(first, "IsAvailable"), Is.False);
        Assert.That(Get<Array>(experience, "RecentEffects").Length, Is.EqualTo(1));
        Teleport(second.transform.position);
        yield return new WaitForSeconds(.08f);
        Assert.That(Get<bool>(second, "IsAvailable"), Is.True);
        Assert.That(Get<Array>(experience, "RecentEffects").Length, Is.EqualTo(1));
        yield return new WaitForSeconds(.3f);
        Assert.That(Get<bool>(second, "IsAvailable"), Is.False);
        Assert.That(Get<Array>(experience, "RecentEffects").Length, Is.EqualTo(2));
        Teleport(new Vector3(-10, 10, 0));
        yield return new WaitForSeconds(4);
        Assert.That(areas.Sum(a => Get<int>(a, "ActiveCount")), Is.EqualTo(24));
        Assert.That(Get<bool>(first, "IsAvailable"), Is.True);
        Assert.That(Vector3.Distance(previous, Get<Vector3>(first, "Anchor")), Is.GreaterThanOrEqualTo(1));
        Assert.That(first.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(first.GetComponent<MeshCollider>().enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator GravityReplacesExpiresAndRespawnKeepsHistory()
    {
        Call(experience, "TryApply", areas[0]);
        int historyCount = Get<Array>(experience, "RecentEffects").Length;
        Call(player, "ApplyGravityEffect", .25f, 4f);
        Call(player, "ApplyGravityEffect", -.12f, .2f);
        Assert.That(Get<float>(player, "GravityMultiplier"), Is.EqualTo(-.12f));
        Teleport(new Vector3(-10, 30, 0));
        ((Behaviour)player).enabled = true;
        yield return new WaitForSeconds(.3f);
        Assert.That(Get<float>(player, "GravityMultiplier"), Is.EqualTo(1));
        Assert.That(Get<float>(player, "GravityEffectRemaining"), Is.Zero);
        Call(player, "ApplyGravityEffect", .3f, 3f);
        Call(player, "ApplyLaunch", new Vector3(5, 8, 0));
        Call(player, "ReturnToStart");
        Assert.That(Get<Vector3>(player, "ExternalVelocity"), Is.EqualTo(Vector3.zero));
        Assert.That(Get<float>(player, "GravityEffectRemaining"), Is.Zero);
        Assert.That(Get<Array>(experience, "RecentEffects").Length, Is.EqualTo(historyCount));
    }

    [UnityTest]
    public IEnumerator LaunchDecaysAndBuoyancyLiftsFromGroundWithSpeedCap()
    {
        Teleport(new Vector3(-10, 30, 0));
        Call(player, "ApplyLaunch", new Vector3(8, 10, 0));
        ((Behaviour)player).enabled = true;
        yield return new WaitForSeconds(.5f);
        Vector3 velocity = Get<Vector3>(player, "ExternalVelocity");
        Assert.That(velocity.x, Is.InRange(4.5f, 5.5f));
        Call(player, "ReturnToStart");
        yield return new WaitForSeconds(.15f);
        float initialHeight = player.transform.position.y;
        Call(player, "ApplyGravityEffect", -.16f, 2f);
        yield return new WaitForSeconds(1);
        Assert.That(player.transform.position.y, Is.GreaterThan(initialHeight + .5f));
        Assert.That(Get<Vector3>(player, "ExternalVelocity").y, Is.InRange(0, 3.001f));
    }

    [UnityTest]
    public IEnumerator CeilingClearsUpwardMomentum()
    {
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.transform.position = new Vector3(-10, 13, 0);
        ceiling.transform.localScale = new Vector3(8, 1, 8);
        Teleport(new Vector3(-10, 10, 0));
        Call(player, "ApplyLaunch", new Vector3(0, 11, 0));
        ((Behaviour)player).enabled = true;
        yield return new WaitForSeconds(.22f);
        Assert.That(Get<Vector3>(player, "ExternalVelocity").y, Is.LessThanOrEqualTo(0));
        Object.Destroy(ceiling);
    }

    [UnityTest]
    public IEnumerator CrowdedAreasRetryWithoutOverlapsOrGrowingPool()
    {
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = new Vector3(7, 6, 28);
        obstacle.transform.localScale = Vector3.one * 150;
        foreach (Component item in Find("Float")) item.gameObject.SetActive(false);
        Physics.SyncTransforms();
        yield return new WaitForSeconds(2);
        Assert.That(areas.Sum(a => Get<int>(a, "ActiveCount")), Is.Zero);
        Assert.That(areas.Sum(a => a.GetComponentsInChildren(GameType("Float"), true).Length), Is.EqualTo(24));
        Object.Destroy(obstacle);
        yield return new WaitForSeconds(8);
        Assert.That(areas.Sum(a => Get<int>(a, "ActiveCount")), Is.EqualTo(24));
    }

    private void Teleport(Vector3 position)
    {
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = position;
        controller.enabled = true;
        Physics.SyncTransforms();
    }
}
