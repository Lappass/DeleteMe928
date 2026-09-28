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
    public float GravityEffectRemaining => gravityEffectRemaining;
    public float GravityMultiplier => gravityMultiplier;
    public Vector3 ExternalVelocity => velocityPhysics;
    private const float GroundedVelocity = -2f; //small downward velocity while grounded so the player doesn't hover
    private float jumpHeldTimer;
    private float jumpPreloadTimer;
    private float coyoteTimer;
    private bool jumping;
    private bool isGrounded;
    private bool wasGroundedLastFrame;
    private Vector3 velocity;
    private Vector3 velocityInput;
    private Vector3 velocityPhysics;
    private CharacterController controller;
    private Controls controls;
    private Vector3 startPosition;
    private Quaternion startRotation;



    void Start()
    {
        //get the character controller and controls components
        controller = GetComponent<CharacterController>();
        controls = GetComponent<Controls>();

        //remember where the player started so they can be sent back there
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    /// <summary>
    /// Teleport the player back to their starting position and clear any momentum
    /// </summary>
    public void ReturnToStart()
    {
        //the character controller overrides position changes while enabled, so turn it off while teleporting
        controller.enabled = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        controller.enabled = true;

        ClearExternalEffects();
        isGrounded = false;
        wasGroundedLastFrame = false;
    }

    public void ApplyLaunch(Vector3 launchVelocity)
    {
        velocityPhysics = Vector3.ClampMagnitude(launchVelocity, externalSpeedMax);
        CancelJumpAssist();
        isGrounded = false;
        ignoreGroundUntil = Time.time + .12f;
    }

    public void ApplyGravityEffect(float multiplier, float duration)
    {
        gravityMultiplier = Mathf.Clamp(multiplier, -2f, 2f);
        gravityEffectRemaining = Mathf.Max(0, duration);
        if (gravityEffectRemaining == 0) gravityMultiplier = 1;
        CancelJumpAssist();
        if (gravityMultiplier < 0)
        {
            // Catch a fall as well as releasing ground adhesion, making this a
            // readable lift effect even when collected during a fast descent.
            velocityPhysics.y = Mathf.Clamp(velocityPhysics.y, 0, buoyancyUpSpeedMax);
            isGrounded = false;
        }
    }

    public void ClearExternalEffects()
    {
        velocityPhysics = Vector3.zero;
        gravityMultiplier = 1;
        gravityEffectRemaining = 0;
        ignoreGroundUntil = 0;
        CancelJumpAssist();
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
        //
        if (controls.JumpTriggered()) //if jump button is pressed
        {
            if (isGrounded) { //regular jump
                BeginJump();
            }
            else if (coyoteTimer > 0) { //coyote time jump
                BeginJump();
            }
            else { //if you are not grounded and didn't coyote jump, start the jump preload timer
                jumpPreloadTimer = jumpPreloadTimerMax;
            }
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
        isGrounded = Time.time >= ignoreGroundUntil && velocityPhysics.y <= 0 &&
            gravityMultiplier >= 0 && RaycastTouchesGround();
        if (isGrounded && !wasGroundedLastFrame) {
            GroundEnter();
        }
        if (!isGrounded && wasGroundedLastFrame) {
            GroundExit();
        }
        wasGroundedLastFrame = isGrounded;
        if (isGrounded && velocityPhysics.y <= 0) {
            velocityPhysics.y = GroundedVelocity;
        }

        // --gravity logic-- only apply gravity if you are not jumping or if you are jumping but the jump button is not being held down
        if (jumping && gravityEffectRemaining <= 0 && jumpHeldTimer < jumpHeldTimerMax) {
            if (controls.JumpHeld()) {
                jumpHeldTimer += Time.fixedDeltaTime;
            }
            else {
                jumpHeldTimer = jumpHeldTimerMax;
            }
        }
        else {
            ApplyGravity(gravityMultiplier);
        }

        // --movement logic--
        Vector2 moveInput = Vector2.ClampMagnitude(controls.MoveInput(), 1f); //get move input vector and clamp to 1
        velocityInput = transform.right * moveInput.x + transform.forward * moveInput.y; //get input velocity
  
        velocityInput *= moveSpeed; //scale by move speed
        
        if (gravityMultiplier < 0) velocityPhysics.y = Mathf.Min(velocityPhysics.y, buoyancyUpSpeedMax);
        velocityPhysics = Vector3.ClampMagnitude(velocityPhysics, externalSpeedMax);
        velocity = velocityInput + velocityPhysics; //combine input velocity and physics velocity
        
        CollisionFlags collisions = controller.Move(velocity * Time.fixedDeltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && velocityPhysics.y > 0)
        {
            velocityPhysics.y = 0;
            jumpHeldTimer = jumpHeldTimerMax;
        }
        Vector3 horizontal = new Vector3(velocityPhysics.x, 0, velocityPhysics.z);
        horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, externalHorizontalDeceleration * Time.fixedDeltaTime);
        velocityPhysics.x = horizontal.x;
        velocityPhysics.z = horizontal.z;
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
        //USE THIS IF YOU WANT SOMETHING TO HAPPEN EACH FRAME YOU ARE TOUCHING THE GROUND
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
