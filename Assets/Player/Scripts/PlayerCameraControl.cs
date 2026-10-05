using UnityEngine;

public class PlayerCameraControl : MonoBehaviour
{

    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float mouseSensitivity;
    [SerializeField] private float stickSensitivity;
    [SerializeField] private float wallRunRoll = 10f;

    private float rotationX = 0f;
    private float roll;
    private float inputX;
    private float inputY;
    private Controls controls;
    private PlayerMovement movement;

    void Start()
    {
        //lock mouse and disable cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        //get the controls component
        controls = GetComponent<Controls>();
        movement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        //get inputX based on mouse or right stick movement
        inputX = controls.LookMouseInput().x * mouseSensitivity;
        inputX += controls.LookStickInput().x * stickSensitivity * Time.deltaTime;

        //get inputY based on mouse or right stick movement
        inputY = controls.LookMouseInput().y * mouseSensitivity;
        inputY += controls.LookStickInput().y * stickSensitivity * Time.deltaTime;
        
        //rotate the entire player left and right based on inputX
        transform.Rotate(Vector3.up * inputX, Space.World);
        
        //rotate the camera up and down based on inputY, clamped to 90 degrees up and down
        rotationX -= inputY; //invert the inputY so that moving the mouse up looks up and moving the mouse down looks down
        rotationX = Mathf.Clamp(rotationX, -90f, 90f); //clamp the rotationX to 90 degrees up and down

        //tilt toward the wall while wall running; positive Z rolls the view the other way, so the sign of the wall normal does the lean
        float targetRoll = 0f;
        if (movement.IsWallRunning) {
            float side = Vector3.Dot(movement.WallNormal, transform.right);
            if (Mathf.Abs(side) > 0.01f) {
                targetRoll = Mathf.Sign(side) * wallRunRoll;
            }
        }
        roll = Mathf.Lerp(roll, targetRoll, Mathf.Clamp01(Time.deltaTime * 8f));
        cameraHolder.localRotation = Quaternion.Euler(rotationX, 0f, roll);
    }
}

