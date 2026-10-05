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
    private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
    private InputSettings.BackgroundBehavior previousBackgroundBehavior;
    private InputSettings.UpdateMode previousUpdateMode;
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
        previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        previousUpdateMode = InputSystem.settings.updateMode;
        previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        // Headless Unity has no physical devices. Create them before PlayerInput
        // enables so it can establish a valid input user during scene loading.
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        yield return SceneManager.LoadSceneAsync("PlatformerLevel");
        yield return null;
        player = Find("PlayerMovement").Single();
        var playerInput = player.GetComponent<PlayerInput>();
        playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
        playerInput.SwitchCurrentActionMap("Player");
        playerInput.ActivateInput();
        experience = player.GetComponent(GameType("FloatExperience"));
        ((Behaviour)player).enabled = false;
        Teleport(new Vector3(-10, 10, 0));
        Call(Find("EndlessWorld").Single(), "CancelLandingAssist");
        areas = Find("FloatArea").Where(area => area.GetComponentInParent(GameType("EndlessSegment")) == null).ToArray();
        Assert.That(areas.Length, Is.EqualTo(3));
        yield return new WaitForSeconds(2);
        Assert.That(areas.Sum(a => Get<int>(a, "ActiveCount")), Is.GreaterThan(0), "The configured route should offer at least one clear spawn point.");
        foreach (Component area in areas)
            Assert.That(Get<int>(area, "ActiveCount"), Is.InRange(0, 8), area.name);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
        InputSystem.settings.updateMode = previousUpdateMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
        yield return null;
    }

    private void Keys(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
    }

    [UnityTest]
    public IEnumerator GeneratedObjectsRespectReservationsAndAppearanceQuotas()
    {
        Component[] floats = Find("Float").Where(item => item.gameObject.activeSelf && Get<bool>(item, "IsAvailable")).ToArray();
        Assert.That(floats.Length, Is.InRange(1, 48), "Active cloud count must stay bounded by the fixed and streamed pools.");
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
            int[] sizes = new int[3];
            int[] shapes = new int[3];
            int[] effectCounts = new int[4];
            foreach (Component item in area.GetComponentsInChildren(GameType("Float")))
            {
                int appearance = Get<int>(item, "Appearance");
                sizes[appearance / 3]++;
                shapes[appearance % 3]++;
                effectCounts[(int)Get<FloatEffect>(item, "Effect")]++;
            }
            IList pendingSpawns = (IList)area.GetType().GetField("pendingSpawns", Fields).GetValue(area);
            foreach (object pending in pendingSpawns)
            {
                Type pendingType = pending.GetType();
                if (!(bool)pendingType.GetField("IsSet").GetValue(pending)) continue;
                int appearance = (int)pendingType.GetField("Appearance").GetValue(pending);
                FloatEffect effect = (FloatEffect)pendingType.GetField("Effect").GetValue(pending);
                sizes[appearance / 3]++;
                shapes[appearance % 3]++;
                effectCounts[(int)effect]++;
            }
            if (sizes[0] + sizes[1] + sizes[2] == 0) continue;
            for (int dimension = 0; dimension < 2; dimension++)
            {
                int[] counts = dimension == 0 ? sizes : shapes;
                Assert.That(counts.Max() - counts.Min(), Is.LessThanOrEqualTo(1),
                    $"{area.name} appearance dimension {dimension} counts: {string.Join(",", counts)}");
            }
            Assert.That(effectCounts.Max() - effectCounts.Min(), Is.LessThanOrEqualTo(1));
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator ContactIsSingleUseCooldownRetriesAndPoolReplenishes()
    {
        int availableBeforeContact = areas.Sum(a => Get<int>(a, "ActiveCount"));
        Component[] floats = areas.SelectMany(area => area.GetComponentsInChildren(GameType("Float")))
            .Cast<Component>().Where(item => Get<bool>(item, "IsAvailable")).ToArray();
        Assert.That(floats.Length, Is.GreaterThanOrEqualTo(2));
        Component first = floats[0], second = floats[1];
        Vector3 previous = Get<Vector3>(first, "Anchor");
        Vector3 secondPrevious = Get<Vector3>(second, "Anchor");
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
        Assert.That(areas.Sum(a => Get<int>(a, "ActiveCount")), Is.GreaterThanOrEqualTo(availableBeforeContact - 1));
        Assert.That(areas.All(a => Get<int>(a, "ActiveCount") <= 8), Is.True);
        float replenishDeadline = Time.time + 8;
        while (!Get<bool>(first, "IsAvailable") && !Get<bool>(second, "IsAvailable") && Time.time < replenishDeadline) yield return null;
        Component recycled = Get<bool>(first, "IsAvailable") ? first : second;
        Vector3 oldPosition = recycled == first ? previous : secondPrevious;
        Assert.That(Get<bool>(recycled, "IsAvailable"), Is.True);
        Assert.That(Vector3.Distance(oldPosition, Get<Vector3>(recycled, "Anchor")), Is.GreaterThanOrEqualTo(1));
        Assert.That(recycled.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(recycled.GetComponent<MeshCollider>().enabled, Is.True);
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
        Vector3 chosen = -player.transform.right;
        Assert.That((bool)Call(experience, "TryApply", cloud), Is.True);
        Vector3 velocity = Get<Vector3>(player, "ExternalVelocity");
        velocity.y = 0;
        Vector2 moveInput = (Vector2)Call(player.GetComponent(GameType("Controls")), "MoveInput");
        PlayerInput input = player.GetComponent<PlayerInput>();
        Assert.That(Vector3.Dot(velocity.normalized, chosen), Is.GreaterThan(.999f),
            $"Move input={moveInput}, A pressed={keyboard.aKey.isPressed}, Move enabled={input.actions["Move"].enabled}, Move controls={string.Join(",", input.actions["Move"].controls.Select(control => control.path))}, paired ids={string.Join(",", input.devices.Select(device => device.deviceId))}, action device ids={string.Join(",", input.actions.devices?.Select(device => device.deviceId) ?? Enumerable.Empty<int>())}, scheme={input.currentControlScheme}, launch={velocity}, facing={player.transform.forward}, action map={input.currentActionMap?.name}");
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
        ((Behaviour)player).enabled = true;
        for (int i = 0; i < 15; i++)
        {
            Keys(Key.Space);
            yield return new WaitForFixedUpdate();
        }
        PlayerInput input = player.GetComponent<PlayerInput>();
        Assert.That(Get<bool>(player, "IsGliding"), Is.True,
            $"Jump held={(bool)Call(player.GetComponent(GameType("Controls")), "JumpHeld")}, Space pressed={keyboard.spaceKey.isPressed}, Jump enabled={input.actions["Jump"].enabled}, devices={string.Join(",", input.devices.Select(device => device.name))}, grounded={Get<bool>(player, "IsGrounded")}, external velocity={Get<Vector3>(player, "ExternalVelocity")}");
        Assert.That(Get<Vector3>(player, "ExternalVelocity").y, Is.GreaterThanOrEqualTo(-4.51f));
        Keys();
        yield return new WaitForSeconds(.3f);
        Assert.That(Get<Vector3>(player, "ExternalVelocity").y, Is.LessThan(-7));
        Call(player, "ApplyLaunch", new Vector3(10, 5, 0));
        for (int i = 0; i < 10; i++)
        {
            Keys(Key.E);
            yield return new WaitForFixedUpdate();
        }
        Assert.That(Get<bool>(player, "IsBraking"), Is.True);
        Assert.That(Get<Vector3>(player, "ExternalVelocity").x, Is.LessThan(6.5f));
        Call(player, "ApplyLaunch", new Vector3(8, 8, 0));
        for (int i = 0; i < 10; i++)
        {
            Keys(Key.W);
            yield return new WaitForFixedUpdate();
        }
        Assert.That(Get<Vector3>(player, "ExternalVelocity").z, Is.GreaterThan(1));
    }

    [UnityTest]
    public IEnumerator HudShowsFootClearanceHazardsAndDoesNotBlockInput()
    {
        Component hud = player.GetComponent(GameType("CloudHUD"));
        if (hud == null) hud = player.gameObject.AddComponent(GameType("CloudHUD"));
        yield return null;
        Assert.That(hud, Is.Not.Null);
        Teleport(new Vector3(0, 1.05f, 0));
        Call(hud, "Refresh");
        Assert.That(Get<float>(hud, "GroundDistance"), Is.InRange(0, .2f));
        Assert.That(Get<bool>(hud, "SurfaceIsHazard"), Is.False);
        Teleport(new Vector3(-10, 5, 0));
        Call(hud, "Refresh");
        Assert.That(Get<float>(hud, "GroundDistance"), Is.InRange(6.9f, 7.1f));
        Assert.That(Get<bool>(hud, "SurfaceIsHazard"), Is.False, "The opening route now has safe base ground beneath its platforms.");
        Teleport(new Vector3(1000, 10, 15));
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
        int pooledCount = areas.Sum(a => a.GetComponentsInChildren(GameType("Float"), true).Length);
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = new Vector3(7, 6, 28);
        obstacle.transform.localScale = Vector3.one * 150;
        foreach (Component item in Find("Float")) item.gameObject.SetActive(false);
        Physics.SyncTransforms();
        yield return new WaitForSeconds(2);
        Assert.That(areas.Sum(a => Get<int>(a, "ActiveCount")), Is.Zero);
        Assert.That(areas.Sum(a => a.GetComponentsInChildren(GameType("Float"), true).Length), Is.EqualTo(pooledCount));
        Object.Destroy(obstacle);
        yield return new WaitForSeconds(4);
        Assert.That(areas.Sum(a => Get<int>(a, "ActiveCount")), Is.GreaterThan(0));
        Assert.That(areas.All(a => Get<int>(a, "ActiveCount") <= 8), Is.True);
    }

    [UnityTest]
    public IEnumerator EndlessSectionsAreDeterministicAndKeepSafeGround()
    {
        Component[] sections = Find("EndlessSegment");
        Assert.That(sections.Length, Is.EqualTo(3), "Three future sections should be ready beyond the authored opening.");
        Component first = sections.Single(section => Get<int>(section, "SegmentIndex") == 0);
        Assert.That(Get<float>(first, "SafeAreaRatio"), Is.GreaterThanOrEqualTo(.7f));
        MeshCollider terrain = first.GetComponentsInChildren<MeshCollider>()
            .Single(collider => collider.name == "Safe ground with open lava vent");
        Component lavaTrigger = first.GetComponentInChildren(GameType("Lava"));
        Vector2 holeCenter = (Vector2)first.GetType().GetField("holeCenter", Fields).GetValue(first);
        Vector3 triggerInGround = terrain.transform.InverseTransformPoint(lavaTrigger.transform.position);
        Assert.That(triggerInGround.x, Is.EqualTo(holeCenter.x).Within(.001f));
        Assert.That(triggerInGround.z, Is.EqualTo(holeCenter.y).Within(.001f),
            "The visible/collidable vent must line up with the actual opening in the ground mesh.");
        float largestRadius = Get<float>(first, "MaximumHoleRadius");
        Assert.That(largestRadius, Is.GreaterThan(3.7f), "The new lava vent should be substantially larger.");
        Call(first, "UpdateHoleMesh", 1.4f);
        int smallVentTriangleCount = terrain.sharedMesh.triangles.Length;
        Call(first, "UpdateHoleMesh", largestRadius);
        int expandedVentTriangleCount = terrain.sharedMesh.triangles.Length;
        Assert.That(expandedVentTriangleCount, Is.LessThan(smallVentTriangleCount), "The expanding vent must remove real floor triangles.");
        Assert.That(Get<float>(first, "SafeAreaRatio"), Is.GreaterThanOrEqualTo(.7f));
        Vector3 ventCenter = terrain.transform.TransformPoint(new Vector3(holeCenter.x, 1f, holeCenter.y));
        Assert.That(terrain.Raycast(new Ray(ventCenter, Vector3.down), out _, 2f), Is.False,
            "There must be a real collider opening at the visual vent center.");
        Vector3 safeProbe = terrain.transform.TransformPoint(new Vector3(holeCenter.x >= 0 ? -20f : 20f, 1f, holeCenter.y));
        Assert.That(terrain.Raycast(new Ray(safeProbe, Vector3.down), out _, 2f), Is.True,
            "Safe ground next to the vent must remain solid.");
        Call(first, "UpdateHoleMesh", 1.4f);
        Assert.That(terrain.sharedMesh.triangles.Length, Is.GreaterThan(expandedVentTriangleCount), "Shrinking the vent restores safe floor.");
        Assert.That(first.GetComponentInChildren(GameType("Lava")), Is.Not.Null);
        Assert.That(first.GetComponentInChildren<CapsuleCollider>().isTrigger, Is.True);
        Assert.That(Get<Component>(first, "CloudArea"), Is.Not.Null);
        Component[] steps = first.GetComponentsInChildren(GameType("EndlessSafePlatform"))
            .Cast<Component>().Where(step => step.GetComponentInParent(GameType("EndlessSegment")) == first).ToArray();
        Assert.That(steps.Length, Is.GreaterThanOrEqualTo(8));
        foreach (Component step in steps)
        {
            Mesh mesh = step.GetComponent<MeshCollider>().sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.EqualTo(12), "Generated steps are thick five-sided prisms.");
        }
        foreach (Component section in sections)
        {
            Component[] mainRoute = section.GetComponentsInChildren(GameType("EndlessSafePlatform"))
                .Cast<Component>()
                .Where(step => step.name.StartsWith("Step "))
                .OrderBy(step => step.transform.position.z)
                .ToArray();
            Assert.That(mainRoute.Length, Is.EqualTo(8));
            for (int i = 1; i < mainRoute.Length; i++)
            {
                Vector3 previous = mainRoute[i - 1].transform.position;
                Vector3 next = mainRoute[i].transform.position;
                Assert.That(next.x - previous.x, Is.InRange(-2.301f, 2.301f),
                    $"Section {Get<int>(section, "SegmentIndex")} has a reachable sideways step.");
                Assert.That(next.y - previous.y, Is.InRange(-.911f, .911f),
                    $"Section {Get<int>(section, "SegmentIndex")} has a reachable height change.");
                Assert.That(next.z - previous.z, Is.EqualTo(4f).Within(.001f));
                Assert.That(new Vector2(next.x - previous.x, next.z - previous.z).magnitude, Is.LessThan(4.62f));
            }
        }
        Component[] orderedSections = sections.OrderBy(section => Get<int>(section, "SegmentIndex")).ToArray();
        for (int i = 1; i < orderedSections.Length; i++)
        {
            Component previousEnd = orderedSections[i - 1].GetComponentsInChildren(GameType("EndlessSafePlatform"))
                .Cast<Component>().Single(step => step.name == "Step 8");
            Component nextStart = orderedSections[i].GetComponentsInChildren(GameType("EndlessSafePlatform"))
                .Cast<Component>().Single(step => step.name == "Step 1");
            Assert.That(nextStart.transform.position.x - previousEnd.transform.position.x, Is.InRange(-2.301f, 2.301f));
            Assert.That(nextStart.transform.position.y - previousEnd.transform.position.y, Is.InRange(-.911f, .911f));
            Assert.That(nextStart.transform.position.z - previousEnd.transform.position.z, Is.EqualTo(4f).Within(.001f));
        }

        Component world = Find("EndlessWorld").Single();
        var previewObject = new GameObject("Determinism preview");
        var preview = previewObject.AddComponent(GameType("EndlessSegment"));
        Material platform = (Material)world.GetType().GetField("platformMaterial", Fields).GetValue(world);
        Material lava = (Material)world.GetType().GetField("lavaMaterial", Fields).GetValue(world);
        Material cloud = (Material)world.GetType().GetField("cloudMaterial", Fields).GetValue(world);
        Call(preview, "Build", 0, 51.65f, 18.65f, 6.4f, 32f, 44f, 928, 0f, 18.65f, platform, lava, cloud);
        Vector3[] actualPositions = steps.Select(step => step.transform.position).OrderBy(position => position.z).ThenBy(position => position.x).ToArray();
        Vector3[] previewPositions = preview.GetComponentsInChildren(GameType("EndlessSafePlatform"))
            .Cast<Component>().Select(step => step.transform.position).OrderBy(position => position.z).ThenBy(position => position.x).ToArray();
        Assert.That(previewPositions.Length, Is.EqualTo(actualPositions.Length));
        for (int i = 0; i < actualPositions.Length; i++)
            Assert.That(Vector3.Distance(actualPositions[i], previewPositions[i]), Is.LessThan(.0001f), $"Platform {i} should match the fixed seed.");
        Object.Destroy(previewObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator AdvancingStreamsSectionsAndLavaRespawnsAtCheckpoint()
    {
        Component world = Find("EndlessWorld").Single();
        Component initial = Find("EndlessSegment").Single(section => Get<int>(section, "SegmentIndex") == 0);
        Component checkpoint = initial.GetComponentsInChildren(GameType("EndlessSafePlatform"))
            .Cast<Component>().First(step => step.GetComponentInParent(GameType("EndlessSegment")) == initial);
        Collider checkpointCollider = checkpoint.GetComponent<Collider>();
        Set(player, "isGrounded", true);
        Call(world, "RegisterSafeSurface", checkpointCollider);
        Vector3 expected = (Vector3)checkpoint.GetType().GetProperty("RespawnPosition").GetValue(checkpoint);

        Teleport(new Vector3(10, 10, 200));
        yield return null;
        yield return null;
        int[] activeIndices = Find("EndlessSegment").Select(section => Get<int>(section, "SegmentIndex")).ToArray();
        Assert.That(activeIndices.Max(), Is.GreaterThan(2));
        Assert.That(activeIndices.Length, Is.LessThanOrEqualTo(5), "Only nearby sections and the pinned checkpoint section should remain.");
        Assert.That(activeIndices, Does.Contain(0), "The section holding the last safe platform stays alive.");
        Assert.That(activeIndices.Contains(1), Is.False);
        Assert.That(activeIndices.Contains(2), Is.False);

        Call(player, "ApplyGravityEffect", .25f, 4f);
        GameObject lavaObject = new GameObject("Test lava vent");
        var lava = lavaObject.AddComponent(GameType("Lava"));
        Call(lava, "OnTriggerEnter", player.GetComponent<CharacterController>());
        Assert.That(Vector3.Distance(player.transform.position, expected), Is.LessThan(.01f));
        Assert.That(Get<float>(player, "GravityEffectRemaining"), Is.Zero);
        Object.Destroy(lavaObject);
        yield return null;
        // Unity destroys recycled section GameObjects at the end of the frame.
        yield return null;
        int[] respawnedIndices = Find("EndlessSegment").Select(section => Get<int>(section, "SegmentIndex")).ToArray();
        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, respawnedIndices,
            "Respawning at an old checkpoint rebuilds its missing forward sections and releases stale distant ones.");
    }

    [UnityTest]
    public IEnumerator SafeLandingLaunchesTowardACloudWithinThreeSecondsAndRespawnCancelsIt()
    {
        Component world = Find("EndlessWorld").Single();
        Component cloud = areas.SelectMany(area => area.GetComponentsInChildren(GameType("Float")))
            .Cast<Component>().First(item => Get<bool>(item, "IsAvailable"));
        Vector3 anchor = Get<Vector3>(cloud, "Anchor");
        Assert.That(Physics.Raycast(anchor, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore), Is.True);
        Teleport(hit.point + Vector3.up * 1.05f);
        Set(player, "isGrounded", true);
        float landedAt = Time.time;
        Call(world, "NotifySafeLanding", hit.collider);
        Assert.That(Get<bool>(world, "LandingAssistPending"), Is.True);
        yield return new WaitForSeconds(1.7f);
        Vector3 launch = Get<Vector3>(player, "ExternalVelocity");
        Assert.That(launch.y, Is.GreaterThan(4f));
        Assert.That(launch.magnitude, Is.LessThanOrEqualTo(36.01f));
        Assert.That(Time.time - landedAt, Is.LessThan(3f));
        Assert.That(Get<bool>(world, "LandingAssistPending"), Is.False);

        Call(player, "ClearExternalEffects");
        Teleport(hit.point + Vector3.up * 1.05f);
        Set(player, "isGrounded", true);
        Call(world, "NotifySafeLanding", hit.collider);
        Call(world, "Respawn", player);
        Assert.That(Get<bool>(world, "LandingAssistPending"), Is.False);
        yield return new WaitForSeconds(1.7f);
        Assert.That(Get<Vector3>(player, "ExternalVelocity"), Is.EqualTo(Vector3.zero));
    }

    [UnityTest]
    public IEnumerator ActualLandingStartsCloudLiftAndPlayerJumpCancelsIt()
    {
        Component world = Find("EndlessWorld").Single();
        Collider startPad = GameObject.Find("StartPad").GetComponent<Collider>();
        Vector3 padCenter = startPad.bounds.center;
        Call(player, "ClearExternalEffects");
        Teleport(new Vector3(padCenter.x, startPad.bounds.max.y + 2.5f, padCenter.z));
        Set(player, "isGrounded", false);
        Set(player, "wasGroundedLastFrame", false);
        ((Behaviour)player).enabled = true;
        float deadline = Time.time + 2f;
        while (Time.time < deadline && !Get<bool>(world, "LandingAssistPending"))
            yield return null;
        Assert.That(Get<bool>(player, "IsGrounded"), Is.True, "The player should land on the safe opening platform.");
        Assert.That(Get<bool>(world, "LandingAssistPending"), Is.True, "A real GroundEnter should schedule the lift.");
        Call(player, "BeginJump");
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(Get<bool>(world, "LandingAssistPending"), Is.False, "A deliberate jump should cancel the automatic lift.");
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
