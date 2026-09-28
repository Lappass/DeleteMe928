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

    
    
    void Start()
    {
        //get the character controller and controls components
        controller = GetComponent<CharacterController>();
        controls = GetComponent<Controls>();
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
        // --isGrounded logic--
        isGrounded = RaycastTouchesGround();
        if (isGrounded && !wasGroundedLastFrame) {
            GroundEnter();
        }
        if (!isGrounded && wasGroundedLastFrame) {
            GroundExit();
        }
        wasGroundedLastFrame = isGrounded;
        if (isGrounded && velocityPhysics.y <= 0) {
            velocityPhysics.y = 0;
        }

        // --gravity logic-- only apply gravity if you are not jumping or if you are jumping but the jump button is not being held down
        if (jumping && jumpHeldTimer < jumpHeldTimerMax) {
            if (controls.JumpHeld()) {
                jumpHeldTimer += Time.fixedDeltaTime;
            }
            else {
                jumpHeldTimer = jumpHeldTimerMax;
            }
        }
        else {
            ApplyGravity();
        }

        // --movement logic--
        Vector2 moveInput = Vector2.ClampMagnitude(controls.MoveInput(), 1f); //get move input vector and clamp to 1
        velocityInput = transform.right * moveInput.x + transform.forward * moveInput.y; //get input velocity
  
        velocityInput *= moveSpeed; //scale by move speed
        
        velocity = velocityInput + velocityPhysics; //combine input velocity and physics velocity
        
        controller.Move(velocity * Time.fixedDeltaTime); //move the player based on the combined velocity
    }

    void ApplyGravity(float gravityMultiplier = 1f)
    {
        velocityPhysics.y -= gravity * gravityMultiplier * Time.fixedDeltaTime; //apply gravity to the physics y velocity
        if (velocityPhysics.y < -fallSpeedMax) { //make sure fall speed never exceeds fallSpeedMax
            velocityPhysics.y = -fallSpeedMax;
        }
        if (isGrounded && velocityPhysics.y < 0) { //if grounded, reset y velocity to 0
            velocityPhysics.y = 0;
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
        if (!jumping) {
            coyoteTimer = coyoteTimerMax; //start the coyote timer if you leave the ground and are not jumping
        }
    }
    
    /// <summary>
    /// Cast raycasts from the middle of the player and from 8 corners to test if the player is on the ground
    /// </summary>
    bool RaycastTouchesGround() {
        float rayLength = controller.height * .6f;

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
        
        //call leave ground results if we were grounded and now we're not
        if (isGrounded) { 
            GroundExit();
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
