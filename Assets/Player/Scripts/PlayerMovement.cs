using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private bool canJump;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpStartingVelocity;
    [SerializeField] private float gravity;
    [SerializeField] private float fallSpeedMax;
    [SerializeField] private float jumpHeldTimerMax;
    [SerializeField] private float jumpPreloadTimerMax;
    [SerializeField] private float coyoteTimerMax;
    [SerializeField] private LayerMask jumpableMask;
    [Header("Floating object effects")]
    [SerializeField, Min(0)] private float externalHorizontalDeceleration = 6f;
    [SerializeField, Min(1)] private float externalSpeedMax = 40f;
    [SerializeField, Min(0)] private float buoyancyUpSpeedMax = 3f;
    private float gravityEffectRemaining;
    private float gravityMultiplier = 1f;
    private float ignoreGroundUntil;
    public float BaseGravity => Mathf.Max(.01f, gravity);
    public float ExternalHorizontalDeceleration => externalHorizontalDeceleration;
    public float ExternalSpeedLimit => externalSpeedMax;
    public float GravityEffectRemaining => gravityEffectRemaining;
    public float GravityMultiplier => gravityMultiplier;
    public float GravityEffectDuration { get; private set; }
    public Vector3 ExternalVelocity => velocityPhysics;
    public Vector3 ActualVelocity => controller != null ? controller.velocity : Vector3.zero;
    public bool DashReady => dashCharges > 0 && !dashing;
    public bool IsGliding { get; private set; }
    public bool IsBraking { get; private set; }
    public bool IsGrounded => isGrounded;
    public float LifeTime => Time.time - lifeStartedAt;
    public float BestLifeTime => Mathf.Max(bestLifeTime, LifeTime);
    private float lifeStartedAt;
    private float bestLifeTime;
    [Header("Air control and recovery")]
    [SerializeField, Min(0)] private float airTurnDegrees = 150;
    [SerializeField, Min(0)] private float airBrakeDeceleration = 24;
    [SerializeField, Min(1)] private float glideFallSpeed = 3f;
    [SerializeField, Range(.05f, 1)] private float glideGravityMultiplier = .18f;
    [Header("Dash and wall run")]
    [SerializeField] private float groundAcceleration = 80f;
    [SerializeField] private float airAcceleration = 22f;
    [SerializeField] private float dashSpeed = 22f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float wallRunSpeed = 12f;
    [SerializeField] private float wallRunMinSpeed = 6f;
    [SerializeField] private float wallStickSpeed = 3f;
    [SerializeField] private float wallRunMaxTime = 1.6f;
    [SerializeField] private float wallJumpOutSpeed = 8f;
    [SerializeField] private AudioClip dashSound;
    private const float GroundedVelocity = -2f; //small downward velocity while grounded so the player doesn't hover
    private const float WallRunMaxFallSpeed = 12f; //fall off the wall once downward speed gets this high
    private const float WallNormalMaxY = 0.25f; //steeper than about 75 degrees
    private const float WallProbePadding = 0.4f;
    private const float WallRunGravityStart = 0.1f;
    private float jumpHeldTimer;
    private float jumpPreloadTimer;
    private float coyoteTimer;
    private bool jumping;
    private bool isGrounded;
    private bool wasGroundedLastFrame;
    private Collider groundedSurface;
    private Vector3 velocity;
    private Vector3 velocityInput;
    private Vector3 velocityPhysics;
    private Vector3 velocityHorizontal;
    private float inputInfluence = 1f;
    private int dashCharges = 1;
    private float dashTimer;
    private bool dashing;
    private bool wallRunning;
    private float wallRunTimer;
    private Vector3 wallNormal;
    private Collider wallCollider;
    private Collider ignoredWall;
    private CharacterController controller;
    private Controls controls;
    private AudioSource dashSource;
    private Vector3 startPosition;
    private Quaternion startRotation;

    public bool IsWallRunning => wallRunning;
    public Vector3 WallNormal => wallNormal;
    public Vector3 ChosenLaunchDirection
    {
        get
        {
            Vector2 input = controls != null ? controls.MoveInput() : Vector2.zero;
            Vector3 direction = transform.right * input.x + transform.forward * input.y;
            if (direction.sqrMagnitude < .01f) direction = transform.forward;
            direction.y = 0;
            return direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward;
        }
    }

    public void RefillAirDash() => dashCharges = 1;

    void Start()
    {
        //get the character controller and controls components
        controller = GetComponent<CharacterController>();
        controls = GetComponent<Controls>();
        dashSource = GetComponent<AudioSource>();
        if (dashSource == null) {
            dashSource = gameObject.AddComponent<AudioSource>();
        }
        dashSource.playOnAwake = false;
        dashSource.spatialBlend = 0f; //keep the dash sound full volume on the player

        //remember where the player started so they can be sent back there
        startPosition = transform.position;
        startRotation = transform.rotation;
        lifeStartedAt = Time.time;
    }

    /// <summary>
    /// Teleport the player back to their starting position and clear any momentum
    /// </summary>
    public void ReturnToStart()
    {
        EndlessWorld.Instance?.CancelLandingAssist();
        bestLifeTime = Mathf.Max(bestLifeTime, LifeTime);
        lifeStartedAt = Time.time;
        //the character controller overrides position changes while enabled, so turn it off while teleporting
        controller.enabled = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        controller.enabled = true;

        ClearExternalEffects();
        isGrounded = false;
        wasGroundedLastFrame = false;
    }

    public void RespawnAtCheckpoint(Vector3 position, Quaternion rotation)
    {
        EndlessWorld.Instance?.CancelLandingAssist();
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = true;
        ClearExternalEffects();
        isGrounded = false;
        wasGroundedLastFrame = false;
    }

    public void ApplyLaunch(Vector3 launchVelocity)
    {
        // A float launch interrupts traversal without granting a fresh air dash.
        dashing = false;
        dashTimer = 0;
        EndWallRun();
        inputInfluence = 1f;
        velocityHorizontal = Vector3.ClampMagnitude(velocityHorizontal, moveSpeed);
        velocityPhysics = Vector3.ClampMagnitude(launchVelocity, externalSpeedMax);
        CancelJumpAssist();
        isGrounded = false;
        ignoreGroundUntil = Time.time + .12f;
    }

    public void ApplyGravityEffect(float multiplier, float duration)
    {
        gravityMultiplier = Mathf.Clamp(multiplier, -2f, 2f);
        gravityEffectRemaining = Mathf.Max(0, duration);
        GravityEffectDuration = gravityEffectRemaining;
        if (gravityEffectRemaining == 0) gravityMultiplier = 1;
        CancelJumpAssist();
        if (gravityMultiplier < 0)
        {
            EndWallRun();
            // Catch a fall as well as releasing ground adhesion, making this a
            // readable lift effect even when collected during a fast descent.
            velocityPhysics.y = Mathf.Clamp(velocityPhysics.y, 0, buoyancyUpSpeedMax);
            isGrounded = false;
        }
        else if (gravityMultiplier < 1)
        {
            // A feather cloud is useful even if collected during a fast fall.
            velocityPhysics.y = Mathf.Max(velocityPhysics.y, -glideFallSpeed);
        }
    }

    public void ClearExternalEffects()
    {
        velocityPhysics = Vector3.zero;
        velocityHorizontal = Vector3.zero;
        velocityInput = Vector3.zero;
        gravityMultiplier = 1;
        gravityEffectRemaining = 0;
        GravityEffectDuration = 0;
        IsGliding = false;
        IsBraking = false;
        ignoreGroundUntil = 0;
        CancelJumpAssist();
        inputInfluence = 1f;
        dashing = false;
        dashTimer = 0f;
        dashCharges = 1;
        wallRunning = false;
        wallRunTimer = 0f;
        wallNormal = Vector3.zero;
        wallCollider = null;
        ignoredWall = null;
        GetComponent<FloatExperience>()?.ClearFeedback();
    }

    private void CancelJumpAssist()
    {
        jumping = false;
        jumpHeldTimer = jumpHeldTimerMax;
        jumpPreloadTimer = 0;
        coyoteTimer = 0;
    }

    void Update()
    {
        if (controls.JumpTriggered()) //if jump button is pressed
        {
            if (wallRunning) { //kick off the wall before a normal jump can consume the press
                WallKick();
            }
            else if (isGrounded) { //regular jump
                BeginJump();
            }
            else if (coyoteTimer > 0) { //coyote time jump
                BeginJump();
            }
            else { //if you are not grounded and didn't coyote jump, start the jump preload timer
                jumpPreloadTimer = jumpPreloadTimerMax;
            }
        }

        if (controls.SprintTriggered()) {
            TryDash();
        }

        //lower timers at the end of each frame
        jumpPreloadTimer -= Time.deltaTime;
        coyoteTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (gravityEffectRemaining > 0)
        {
            gravityEffectRemaining = Mathf.Max(0, gravityEffectRemaining - Time.fixedDeltaTime);
            if (gravityEffectRemaining == 0) gravityMultiplier = 1;
        }
        // --isGrounded logic--
        groundedSurface = null;
        isGrounded = Time.time >= ignoreGroundUntil && velocityPhysics.y <= 0 &&
            gravityMultiplier >= 0 && RaycastTouchesGround();
        if (isGrounded && groundedSurface != null)
            EndlessWorld.Instance?.RegisterSafeSurface(groundedSurface);
        if (isGrounded && !wasGroundedLastFrame) {
            GroundEnter();
            if (velocityPhysics.y <= 0)
                EndlessWorld.Instance?.NotifySafeLanding(groundedSurface);
        }
        if (!isGrounded && wasGroundedLastFrame) {
            GroundExit();
        }
        wasGroundedLastFrame = isGrounded;
        if (isGrounded && velocityPhysics.y <= 0) {
            velocityPhysics.y = GroundedVelocity;
        }
        if (isGrounded && wallRunning) {
            EndWallRun();
            ignoredWall = null; //landing clears the one-wall lockout
        }

        TickDash();

        // --movement input-- wish direction, scaled by move speed. Momentum code decides how fast we approach it
        Vector2 moveInput = Vector2.ClampMagnitude(controls.MoveInput(), 1f);
        velocityInput = (transform.right * moveInput.x + transform.forward * moveInput.y) * moveSpeed;
        IsBraking = !isGrounded && controls.BrakeHeld();
        if (IsBraking)
        {
            dashing = false;
            dashTimer = 0;
            EndWallRun();
            inputInfluence = 1;
        }
        IsGliding = !isGrounded && !dashing && !wallRunning && gravityMultiplier >= 0 &&
            velocityPhysics.y < -.1f && controls.JumpHeld();

        if (wallRunning && !ConfirmWall()) {
            EndWallRun();
        }
        if (!dashing && !wallRunning && !IsBraking && !IsGliding && !isGrounded && gravityMultiplier >= 0 && Time.time >= ignoreGroundUntil && HorizontalSpeed() >= wallRunMinSpeed) {
            TryStartWallRun();
        }

        // --gravity logic-- dash stays flat; wall run eases gravity back in; otherwise the usual jump-hold
        if (dashing) {
            // Ordinary dashes stay flat; active float gravity still takes effect.
            if (gravityEffectRemaining > 0) ApplyGravity(gravityMultiplier);
        }
        else if (wallRunning) {
            float blend = Mathf.Clamp01(wallRunTimer / wallRunMaxTime);
            ApplyGravity(Mathf.Lerp(WallRunGravityStart, 1f, blend) * gravityMultiplier);
            wallRunTimer += Time.fixedDeltaTime;
            if (wallRunTimer >= wallRunMaxTime || velocityPhysics.y < -WallRunMaxFallSpeed) {
                EndWallRun();
            }
            else {
                ApplyWallRunVelocity();
            }
        }
        else if (jumping && gravityEffectRemaining <= 0 && jumpHeldTimer < jumpHeldTimerMax) {
            if (controls.JumpHeld()) {
                jumpHeldTimer += Time.fixedDeltaTime;
            }
            else {
                jumpHeldTimer = jumpHeldTimerMax;
            }
        }
        else {
            ApplyGravity(gravityMultiplier * (IsGliding ? glideGravityMultiplier : 1));
        }
        if (IsGliding) velocityPhysics.y = Mathf.Max(velocityPhysics.y, -glideFallSpeed);

        if (!dashing && !wallRunning) {
            ApplyHorizontalAcceleration();
        }

        if (gravityMultiplier < 0) velocityPhysics.y = Mathf.Min(velocityPhysics.y, buoyancyUpSpeedMax);
        velocityPhysics = Vector3.ClampMagnitude(velocityPhysics, externalSpeedMax);
        // Input/dash momentum and the decaying float impulse are separate.
        velocity = velocityHorizontal + velocityPhysics;
        if (wallRunning) {
            velocity += -wallNormal * wallStickSpeed; //push into the wall so the probe keeps hitting it
        }

        CollisionFlags collisions = controller.Move(velocity * Time.fixedDeltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && velocityPhysics.y > 0)
        {
            velocityPhysics.y = 0;
            jumpHeldTimer = jumpHeldTimerMax;
        }
        Vector3 horizontal = new Vector3(velocityPhysics.x, 0, velocityPhysics.z);
        float deceleration = externalHorizontalDeceleration;
        if (IsBraking) deceleration = airBrakeDeceleration;
        else if (!isGrounded && moveInput.sqrMagnitude > .01f && horizontal.sqrMagnitude > .01f)
        {
            Vector3 desired = velocityInput.normalized;
            if (Vector3.Dot(horizontal.normalized, desired) < 0) deceleration *= 2;
            horizontal = Vector3.RotateTowards(horizontal, desired * horizontal.magnitude,
                airTurnDegrees * Mathf.Deg2Rad * Time.fixedDeltaTime, 0);
        }
        horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, deceleration * Time.fixedDeltaTime);
        velocityPhysics.x = horizontal.x;
        velocityPhysics.z = horizontal.z;
    }

    void ApplyHorizontalAcceleration()
    {
        float accel = isGrounded ? groundAcceleration : airAcceleration;
        Vector3 wish = velocityInput * inputInfluence;
        if (IsBraking) { accel = airBrakeDeceleration; wish = Vector3.zero; }
        velocityHorizontal = Vector3.MoveTowards(velocityHorizontal, wish, accel * Time.fixedDeltaTime);
        velocityHorizontal.y = 0f;
    }

    float HorizontalSpeed()
    {
        return new Vector3(velocityHorizontal.x, 0f, velocityHorizontal.z).magnitude;
    }

    void TickDash()
    {
        if (!dashing) {
            return;
        }
        dashTimer -= Time.fixedDeltaTime;
        if (dashTimer > 0f) {
            return;
        }
        dashing = false;
        inputInfluence = 1f;
        if (isGrounded) {
            dashCharges = 1; //a dash that never left the ground is ready again once it finishes
        }
    }

    void TryDash()
    {
        if (dashCharges <= 0 || wallRunning || dashing) {
            return;
        }

        Vector2 moveInput = Vector2.ClampMagnitude(controls.MoveInput(), 1f);
        Vector3 dir = transform.right * moveInput.x + transform.forward * moveInput.y;
        if (dir.sqrMagnitude < 0.01f) {
            dir = transform.forward; //no move input: dash the way the body is facing
        }
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) {
            return;
        }
        dir.Normalize();

        velocityHorizontal = dir * dashSpeed;
        // A deliberate dash replaces a cloud's sideways impulse and catches a fall.
        velocityPhysics.x = 0;
        velocityPhysics.z = 0;
        velocityPhysics.y = Mathf.Max(velocityPhysics.y, 0);
        inputInfluence = 0f;
        dashTimer = dashDuration;
        dashing = true;
        dashCharges--;
        if (dashSound != null) {
            dashSource.PlayOneShot(dashSound);
        }
    }

    void TryStartWallRun()
    {
        if (!TryFindWall(out RaycastHit hit)) {
            return;
        }

        wallRunning = true;
        wallRunTimer = 0f;
        wallNormal = hit.normal;
        wallCollider = hit.collider;
        inputInfluence = 0f;
        dashCharges = 1; //a new wall refills the air dash
    }

    void ApplyWallRunVelocity()
    {
        Vector3 along = WallAlong(wallNormal, velocityHorizontal);
        float speed = Mathf.Max(HorizontalSpeed(), wallRunSpeed);
        velocityHorizontal = along * speed;
    }

    /// <summary>
    /// Pop off the wall. Ignores canJump so a wall kick still works when ground jump is disabled.
    /// </summary>
    void WallKick()
    {
        Vector3 along = WallAlong(wallNormal, velocityHorizontal);
        float alongSpeed = HorizontalSpeed() * 0.5f;
        velocityHorizontal = wallNormal * wallJumpOutSpeed + along * alongSpeed;
        velocityHorizontal.y = 0f;
        velocityPhysics.y = jumpStartingVelocity;
        jumpHeldTimer = 0f;
        coyoteTimer = 0f;
        jumpPreloadTimer = 0f;
        jumping = true;
        dashCharges = 1;
        EndWallRun();
    }

    void EndWallRun()
    {
        if (!wallRunning) {
            return;
        }
        wallRunning = false;
        wallRunTimer = 0f;
        ignoredWall = wallCollider; //same wall cannot be grabbed again until the next landing
        wallCollider = null;
        if (!dashing) {
            inputInfluence = 1f;
        }
    }

    bool ConfirmWall()
    {
        Vector3 origin = transform.position + controller.center;
        float distance = controller.radius + WallProbePadding;
        if (!Physics.Raycast(origin, -wallNormal, out RaycastHit hit, distance, jumpableMask, QueryTriggerInteraction.Ignore)) {
            return false;
        }
        if (hit.collider != wallCollider || Mathf.Abs(hit.normal.y) >= WallNormalMaxY) {
            return false;
        }
        wallNormal = hit.normal;
        return true;
    }

    bool TryFindWall(out RaycastHit wallHit)
    {
        float distance = controller.radius + WallProbePadding;
        Vector3 origin = transform.position + controller.center;
        bool left = CastWall(origin, -transform.right, distance, out RaycastHit leftHit);
        bool right = CastWall(origin, transform.right, distance, out RaycastHit rightHit);
        if (left && right) {
            wallHit = leftHit.distance <= rightHit.distance ? leftHit : rightHit;
            return true;
        }
        if (left) {
            wallHit = leftHit;
            return true;
        }
        if (right) {
            wallHit = rightHit;
            return true;
        }
        wallHit = default;
        return false;
    }

    bool CastWall(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
    {
        if (Physics.Raycast(origin, direction, out hit, distance, jumpableMask, QueryTriggerInteraction.Ignore)) {
            if (Mathf.Abs(hit.normal.y) < WallNormalMaxY && hit.collider != ignoredWall) {
                return true;
            }
        }
        hit = default;
        return false;
    }

    /// <summary>
    /// Horizontal direction along the wall, chosen to match the way the player is already moving
    /// </summary>
    Vector3 WallAlong(Vector3 normal, Vector3 reference)
    {
        Vector3 along = Vector3.Cross(normal, Vector3.up);
        along.y = 0f;
        if (along.sqrMagnitude < 0.0001f) {
            return Vector3.zero;
        }
        along.Normalize();
        if (Vector3.Dot(reference, along) < 0f) {
            along = -along;
        }
        return along;
    }

    void ApplyGravity(float gravityMultiplier = 1f)
    {
        velocityPhysics.y -= gravity * gravityMultiplier * Time.fixedDeltaTime; //apply gravity to the physics y velocity
        if (velocityPhysics.y < -fallSpeedMax) { //make sure fall speed never exceeds fallSpeedMax
            velocityPhysics.y = -fallSpeedMax;
        }
        if (isGrounded && gravityMultiplier >= 0 && velocityPhysics.y < 0) { //if grounded, keep a small downward velocity so the player stays snapped to the ground
            velocityPhysics.y = GroundedVelocity;
        }
    }

    void BeginJump()
    {
        //if you can't jump, don't do anything
        if (!canJump) {
            return;
        }

        //if jumping, set the physics y velocity to the jump starting velocity, reset timers, and set jumping to true
        velocityPhysics.y = jumpStartingVelocity;
        jumpHeldTimer = 0;
        coyoteTimer = 0;
        jumpPreloadTimer = 0;
        jumping = true;
    }

    void EndJump(){
        jumping = false;
    }

    /// <summary>
    /// Called when you first start touching the ground
    /// </summary>
    void GroundEnter() {
        dashCharges = 1;
        ignoredWall = null;
        //if you are jumping and you touch the ground, end the jump
        if (jumping) {
            EndJump();
        }
        //if I just landed on the platform right after pressing the jump button, let me jump
        if (jumpPreloadTimer > 0) {
            BeginJump();
        }
    }

    /// <summary>
    /// Called each frame you are touching the ground
    /// </summary>
    void GroundStay(RaycastHit hit)
    {
        groundedSurface = hit.collider;
    }

    /// <summary>
    /// Called when you first stop touching the ground
    /// </summary>
    void GroundExit() {
        if (!jumping && Time.time >= ignoreGroundUntil && gravityMultiplier >= 0) {
            coyoteTimer = coyoteTimerMax; //start the coyote timer if you leave the ground and are not jumping
        }
    }

    /// <summary>
    /// Cast raycasts from the middle of the player and from 8 corners to test if the player is on the ground
    /// </summary>
    bool RaycastTouchesGround() {
        float rayLength = controller.height * .5f + controller.skinWidth + .05f; //reach just past the bottom of the capsule

        //test if the middle of the player is touching the ground
        if (RaycastTest(transform.position, rayLength)) {
            return true;
        }

        //test if any of the 8 corners of the player is touching the ground
        float halfWidth = transform.localScale.x * .5f;
        float diagonalWidth = transform.localScale.x * .35f;

        //test the 4 side of the player
        if (RaycastTest(transform.position + (transform.right * halfWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (-transform.right * halfWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (transform.forward * halfWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (-transform.forward * halfWidth), rayLength)) {
            return true;
        }
        //test the 4 corners of the player
        if (RaycastTest(transform.position + (transform.right * diagonalWidth) + (transform.forward * diagonalWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (transform.right * diagonalWidth) + (-transform.forward * diagonalWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (-transform.right * diagonalWidth) + (transform.forward * diagonalWidth), rayLength)) {
            return true;
        }
        if (RaycastTest(transform.position + (-transform.right * diagonalWidth) + (-transform.forward * diagonalWidth), rayLength)) {
            return true;
        }
        
        //if we are not touching the ground, return false
        return false;
    }

    /// <summary>
    /// Test a single raycast to see if it hits the ground
    /// </summary>
    /// <param name="startingPoint"> Where the ray begins (it will cast down from here)</param>
    /// <param name="rayLength">How long the ray casts</param>
    bool RaycastTest(Vector3 startingPoint, float rayLength) {
        //cast a ray down from the starting point and check if it hits the ground
        RaycastHit hit;
        if (Physics.Raycast(startingPoint, transform.TransformDirection(Vector3.down), out hit, rayLength, jumpableMask, QueryTriggerInteraction.Ignore)) {
            //if it hits the ground, call the GroundStay function and return true
            GroundStay(hit);
            return true;
        }
        else {
            //if it doesn't hit the ground, return false
            return false;
        }
    }
}
