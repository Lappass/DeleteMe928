using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using FloatingObjects;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// The existing game uses Assembly-CSharp. Test assemblies cannot reference that
// predefined assembly, so this fixture accesses its components through reflection.
public class FloatSystemTests
{
    private Component player;
    private Component experience;
    private Component[] areas;
    private Keyboard keyboard;
    private Mouse mouse;
    private InputSettings originalInputSettings;
    private InputSettings testInputSettings;
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
        originalInputSettings = InputSystem.settings;
        testInputSettings = Object.Instantiate(originalInputSettings);
        InputSystem.settings = testInputSettings;
        testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        // Headless Unity has no physical devices. Create them before PlayerInput
        // enables so it can establish a valid input user during scene loading.
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        yield return SceneManager.LoadSceneAsync("PlatformerLevel");
        yield return null;
        player = Find("PlayerMovement").Single();
        player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
        experience = player.GetComponent(GameType("FloatExperience"));
        ((Behaviour)player).enabled = false;
        Teleport(new Vector3(-10, 10, 0));
        areas = Find("FloatArea");
        Assert.That(areas.Length, Is.EqualTo(3));
        float deadline = Time.time + 8;
        while (areas.Sum(a => Get<int>(a, "ActiveCount")) < 24 && Time.time < deadline) yield return null;
        Assert.That(areas.Sum(a => Get<int>(a, "ActiveCount")), Is.EqualTo(24));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
        if (testInputSettings != null) Object.Destroy(testInputSettings);
        yield return null;
    }

    private void Keys(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
        var input = player.GetComponent<PlayerInput>();
        TestContext.WriteLine($"Input: map={input.currentActionMap?.name}, moveEnabled={input.actions["Move"].enabled}, move={input.actions["Move"].ReadValue<Vector2>()}, jump={input.actions["Jump"].ReadValue<float>()}, A={keyboard.aKey.isPressed}, space={keyboard.spaceKey.isPressed}");
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
            var properties = new MaterialPropertyBlock();
            item.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
            Assert.That(Vector4.Distance(properties.GetColor("_BaseColor"), Get<Color>(item, "EffectColor")), Is.LessThan(.001f));
            Assert.That(item.GetComponent<MeshRenderer>().sharedMaterial.shader.name, Is.EqualTo("CloudHop/Soft Cloud"));
            Assert.That(item.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(1000));
            Assert.That(item.GetComponentInChildren<ParticleSystem>(), Is.Not.Null);
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
            var effects = area.GetComponentsInChildren(GameType("Float")).Cast<Component>().GroupBy(f => Get<FloatEffect>(f, "Effect")).ToArray();
            Assert.That(effects.Length, Is.EqualTo(4));
            Assert.That(effects.All(group => group.Count() == 2), Is.True);
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
        Call(experience, "TryApply", Find("Float")[0]);
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
    public IEnumerator DashAndFloatLaunchKeepSeparateMomentumAndResetOnRespawn()
    {
        Teleport(new Vector3(-10, 30, 0));
        Call(player, "TryDash");
        Assert.That((bool)player.GetType().GetField("dashing", Fields).GetValue(player), Is.True);
        Call(player, "ApplyGravityEffect", .25f, 3f);
        ((Behaviour)player).enabled = true;
        yield return new WaitForSeconds(.06f);
        Assert.That(Get<Vector3>(player, "ExternalVelocity").y, Is.LessThan(0), "Float gravity must still work during a dash.");
        Call(player, "ApplyLaunch", new Vector3(8, 10, 0));
        Assert.That((bool)player.GetType().GetField("dashing", Fields).GetValue(player), Is.False);
        Assert.That((int)player.GetType().GetField("dashCharges", Fields).GetValue(player), Is.Zero);
        Assert.That(Get<Vector3>(player, "ExternalVelocity"), Is.EqualTo(new Vector3(8, 10, 0)));
        Assert.That(((Vector3)player.GetType().GetField("velocityHorizontal", Fields).GetValue(player)).magnitude,
            Is.LessThanOrEqualTo(5.001f), "Old dash momentum must not stack onto the float launch.");
        Call(player, "ReturnToStart");
        Assert.That((Vector3)player.GetType().GetField("velocityHorizontal", Fields).GetValue(player), Is.EqualTo(Vector3.zero));
        Assert.That((int)player.GetType().GetField("dashCharges", Fields).GetValue(player), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator WallRunAndWallKickWorkAndBuoyancyReleasesTheWall()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(-11.1f, 10, 0);
        wall.transform.localScale = new Vector3(1, 10, 30);
        Teleport(new Vector3(-10, 10, 0));
        Set(player, "velocityHorizontal", new Vector3(0, 0, 12));
        Call(player, "TryStartWallRun");
        Assert.That(Get<bool>(player, "IsWallRunning"), Is.True);
        Call(player, "WallKick");
        Assert.That(Get<bool>(player, "IsWallRunning"), Is.False);
        Assert.That(Get<Vector3>(player, "ExternalVelocity").y, Is.GreaterThan(0));
        Assert.That(((Vector3)player.GetType().GetField("velocityHorizontal", Fields).GetValue(player)).x, Is.GreaterThan(0));
        Set(player, "ignoredWall", null);
        Call(player, "TryStartWallRun");
        Assert.That(Get<bool>(player, "IsWallRunning"), Is.True);
        Call(player, "ApplyGravityEffect", -.12f, 2f);
        Assert.That(Get<bool>(player, "IsWallRunning"), Is.False);
        ((Behaviour)player).enabled = true;
        yield return new WaitForSeconds(.1f);
        Assert.That(Get<bool>(player, "IsWallRunning"), Is.False);
        Object.Destroy(wall);
    }

    [UnityTest]
    public IEnumerator VisibleEffectIsHonoredAndLaunchFollowsPlayerInput()
    {
        Component cloud = Find("Float").First(f => Get<FloatEffect>(f, "Effect") == FloatEffect.HorizontalLaunch);
        Set(player, "dashCharges", 0);
        player.transform.rotation = Quaternion.Euler(0, 90, 0);
        Keys(Key.A);
        yield return null;
        Vector3 chosen = -player.transform.right;
        Assert.That((bool)Call(experience, "TryApply", cloud), Is.True);
        Vector3 velocity = Get<Vector3>(player, "ExternalVelocity");
        velocity.y = 0;
        Assert.That(Vector3.Dot(velocity.normalized, chosen), Is.GreaterThan(.999f));
        Assert.That(Get<FloatEffect>(experience, "LastEffect"), Is.EqualTo(FloatEffect.HorizontalLaunch));
        Assert.That(Get<bool>(player, "DashReady"), Is.True);
        Keys();
        yield return new WaitForSeconds(.32f);
        Assert.That((bool)Call(experience, "TryApply", cloud), Is.True);
        Assert.That(Get<FloatEffect>(experience, "LastEffect"), Is.EqualTo(FloatEffect.HorizontalLaunch), "Choosing the same color must not secretly reroll it.");
        velocity = Get<Vector3>(player, "ExternalVelocity");
        velocity.y = 0;
        Assert.That(Vector3.Dot(velocity.normalized, player.transform.forward), Is.GreaterThan(.999f));
    }

    [UnityTest]
    public IEnumerator GlideBrakeAndSteeringGiveAirborneControl()
    {
        Teleport(new Vector3(-10, 50, 0));
        Set(player, "velocityPhysics", new Vector3(0, -12, 0));
        Keys(Key.Space);
        ((Behaviour)player).enabled = true;
        yield return new WaitForSeconds(.3f);
        Assert.That(Get<bool>(player, "IsGliding"), Is.True);
        Assert.That(Get<Vector3>(player, "ExternalVelocity").y, Is.GreaterThanOrEqualTo(-4.51f));
        Keys();
        yield return new WaitForSeconds(.3f);
        Assert.That(Get<Vector3>(player, "ExternalVelocity").y, Is.LessThan(-7));
        Call(player, "ApplyLaunch", new Vector3(10, 5, 0));
        Keys(Key.E);
        yield return new WaitForSeconds(.2f);
        Assert.That(Get<bool>(player, "IsBraking"), Is.True);
        Assert.That(Get<Vector3>(player, "ExternalVelocity").x, Is.LessThan(6.5f));
        Call(player, "ApplyLaunch", new Vector3(8, 8, 0));
        Keys(Key.W);
        yield return new WaitForSeconds(.2f);
        Assert.That(Get<Vector3>(player, "ExternalVelocity").z, Is.GreaterThan(1));
    }

    [UnityTest]
    public IEnumerator HudShowsFootClearanceHazardsAndDoesNotBlockInput()
    {
        Component hud = player.GetComponent(GameType("CloudHUD"));
        Assert.That(hud, Is.Not.Null);
        Teleport(new Vector3(0, 1.05f, 0));
        Call(hud, "Refresh");
        Assert.That(Get<float>(hud, "GroundDistance"), Is.InRange(0, .2f));
        Assert.That(Get<bool>(hud, "SurfaceIsHazard"), Is.False);
        Teleport(new Vector3(-10, 5, 0));
        Call(hud, "Refresh");
        Assert.That(Get<float>(hud, "GroundDistance"), Is.InRange(6.9f, 7.1f));
        Assert.That(Get<bool>(hud, "SurfaceIsHazard"), Is.True);
        Teleport(new Vector3(1000, 10, 1000));
        Call(hud, "Refresh");
        Assert.That(Get<float>(hud, "GroundDistance"), Is.EqualTo(float.PositiveInfinity));
        Canvas canvas = Get<Canvas>(hud, "HudCanvas");
        Assert.That(canvas.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True);
        Assert.That(canvas.GetComponentsInChildren<Text>().Count(label => label.name.StartsWith("Cloud name")), Is.EqualTo(4));
        yield return null;
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
