using UnityEngine;
using UnityEngine.InputSystem;

/// First-person desktop controller.
/// WASD / arrow keys to move, mouse to look. No jumping.
/// Add to the Player root GameObject — CharacterController is added automatically.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float maxSpeed = 4.75f;          // m/s, matches paper

    [Header("Mouse Look")]
    public float mouseSensitivity = 0.15f;  // degrees per pixel
    public float pitchClamp       = 80f;    // degrees up/down

    // Read by TrialManager (Step 5) to detect movement epoch onset
    public bool    IsMoving  { get; private set; }
    public Vector3 Velocity  { get; private set; }   // XZ only, world space

    CharacterController _cc;
    Transform           _camTransform;
    float               _pitch;
    float               _yVelocity;

    void Awake()
    {
        _cc        = GetComponent<CharacterController>();
        _cc.height = 1.75f;
        _cc.radius = 0.30f;
        _cc.center = new Vector3(0f, 0.875f, 0f);
        _cc.minMoveDistance = 0f;

        var cam = GetComponentInChildren<Camera>();
        if (cam != null)
            _camTransform = cam.transform;
        else
            Debug.LogError("[PlayerController] No Camera found as child of Player.");
    }

    void Start()
    {
        LockCursor();
    }

    void Update()
    {
        HandleCursorLock();
        if (Cursor.lockState == CursorLockMode.Locked)
            HandleMouseLook();
        HandleMovement();
    }

    // ── mouse look ────────────────────────────────────────────────────────────

    void HandleMouseLook()
    {
        Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        // Right stick on gamepad adds to the same look axes
        if (Gamepad.current != null)
            delta += Gamepad.current.rightStick.ReadValue() * (mouseSensitivity * 80f * Time.deltaTime);

        // Horizontal: rotate the whole player root (affects movement direction too)
        transform.Rotate(Vector3.up, delta.x);

        // Vertical: tilt camera only, clamped so you can't look fully upside-down
        _pitch = Mathf.Clamp(_pitch - delta.y, -pitchClamp, pitchClamp);
        if (_camTransform != null)
            _camTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    // ── movement ──────────────────────────────────────────────────────────────

    void HandleMovement()
    {
        var kb = Keyboard.current;
        float h = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed  ? 1f : 0f);
        float v = (kb.wKey.isPressed || kb.upArrowKey.isPressed    ? 1f : 0f)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed  ? 1f : 0f);

        // Left stick on gamepad, combined with keyboard input
        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            h += stick.x;
            v += stick.y;
        }

        h = Mathf.Clamp(h, -1f, 1f);
        v = Mathf.Clamp(v, -1f, 1f);

        Vector3 move = transform.forward * v + transform.right * h;
        if (move.sqrMagnitude > 1f) move.Normalize();
        move *= maxSpeed;

        // Minimal gravity so CharacterController stays grounded on the mesh collider
        _yVelocity = _cc.isGrounded ? -0.5f : _yVelocity + Physics.gravity.y * Time.deltaTime;
        move.y = _yVelocity;

        _cc.Move(move * Time.deltaTime);

        IsMoving = (move.x * move.x + move.z * move.z) > 0.01f;
        Velocity  = new Vector3(move.x, 0f, move.z);
    }

    // ── cursor management ─────────────────────────────────────────────────────

    void HandleCursorLock()
    {
        // Escape unlocks cursor (pause / focus loss)
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            UnlockCursor();

        // Click anywhere to re-lock
        if (Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }
}
