using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;
    public float jumpForce = 7f;
    public float crouchSpeed = 1.5f;

    [Header("Crouch")]
    public float crouchHeight = 1.0f;
    public float standHeight = 1.8f;
    public float crouchCenterY = 0.5f;
    public float standCenterY = 0.9f;

    [Header("Ground Check")]
    public float groundCheckRadius = 0.3f;
    public float groundCheckOffset = 0.1f;
    public LayerMask groundLayer = ~0;

    CharacterController cc;
    Animator anim;
    Transform cam;

    Vector3 velocity;
    bool isGrounded;
    bool isCrouching;
    bool isLooting;
    float jumpCooldown;
    float lootTimer;

    // Animator hashes
    int hashSpeed;
    int hashMoveX;
    int hashMoveZ;
    int hashIsRunning;
    int hashEmoteWave;
    int hashEmoteCheer;
    int hashEmoteAngry;
    int hashEmoteClap;
    int hashJump;
    int hashIsGrounded;
    int hashIsCrouching;
    int hashLoot;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponentInChildren<Animator>();
        cam = Camera.main?.transform;

        hashSpeed = Animator.StringToHash("Speed");
        hashMoveX = Animator.StringToHash("MoveX");
        hashMoveZ = Animator.StringToHash("MoveZ");
        hashIsRunning = Animator.StringToHash("IsRunning");
        hashEmoteWave = Animator.StringToHash("EmoteWave");
        hashEmoteCheer = Animator.StringToHash("EmoteCheer");
        hashEmoteAngry = Animator.StringToHash("EmoteAngry");
        hashEmoteClap = Animator.StringToHash("EmoteClap");
        hashJump = Animator.StringToHash("Jump");
        hashIsGrounded = Animator.StringToHash("IsGrounded");
        hashIsCrouching = Animator.StringToHash("IsCrouching");
        hashLoot = Animator.StringToHash("Loot");

        // Fix groundLayer if serialized as 0 (Nothing)
        if (groundLayer == 0) groundLayer = ~0;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // Ground check — use CharacterController built-in detection
        if (jumpCooldown > 0f)
        {
            jumpCooldown -= Time.deltaTime;
            isGrounded = false;
        }
        else
        {
            isGrounded = cc.isGrounded;
        }

        // Debug ground (green = grounded, red = airborne)
        Debug.DrawRay(transform.position + Vector3.up * 0.1f, Vector3.down * 0.3f, isGrounded ? Color.green : Color.red);

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        // Loot lock — block input during loot animation
        if (isLooting)
        {
            lootTimer -= Time.deltaTime;
            if (lootTimer <= 0f) isLooting = false;

            velocity.y += gravity * Time.deltaTime;
            cc.Move(velocity * Time.deltaTime);

            if (anim != null)
            {
                anim.SetFloat(hashSpeed, 0f);
                anim.SetFloat(hashMoveX, 0f);
                anim.SetFloat(hashMoveZ, 0f);
                anim.SetBool(hashIsGrounded, isGrounded);
            }
            return;
        }

        // Interaction raycast (E key) — detect "Interactive" tagged objects
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 rayDir = transform.forward;
        float interactRange = 3.5f;
        Debug.DrawRay(rayOrigin, rayDir * interactRange, Color.yellow);

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, interactRange))
            {
                if (hit.collider.CompareTag("Interactive"))
                {
                    var crate = hit.collider.GetComponent<LootCrate>();
                    if (crate == null) crate = hit.collider.GetComponentInParent<LootCrate>();

                    if (crate != null && !crate.isLooted)
                    {
                        // Face the hit point (not the parent transform which may be offset)
                        Vector3 dir = (hit.point - transform.position);
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.01f)
                            transform.rotation = Quaternion.LookRotation(dir.normalized);

                        crate.Loot();
                        isLooting = true;
                        lootTimer = 2.5f;
                        isCrouching = false;
                        if (anim != null) anim.SetTrigger(hashLoot);
                        return;
                    }
                }
            }
        }

        // Input ZQSD / WASD
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.zKey.isPressed)
            input.y += 1f;
        if (Keyboard.current.sKey.isPressed)
            input.y -= 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.qKey.isPressed)
            input.x -= 1f;
        if (Keyboard.current.dKey.isPressed)
            input.x += 1f;

        // Crouch toggle (C key)
        if (Keyboard.current.cKey.wasPressedThisFrame && isGrounded)
            isCrouching = !isCrouching;

        // Un-crouch if running
        if (Keyboard.current.leftShiftKey.isPressed && isCrouching)
            isCrouching = false;

        // Adjust CharacterController height
        float targetHeight = isCrouching ? crouchHeight : standHeight;
        float targetCenter = isCrouching ? crouchCenterY : standCenterY;
        cc.height = Mathf.Lerp(cc.height, targetHeight, 10f * Time.deltaTime);
        cc.center = new Vector3(0f, Mathf.Lerp(cc.center.y, targetCenter, 10f * Time.deltaTime), 0f);

        bool wantRun = Keyboard.current.leftShiftKey.isPressed && !isCrouching;
        float speed = isCrouching ? crouchSpeed : (wantRun ? runSpeed : walkSpeed);

        // Movement relative to camera
        Vector3 moveDir = Vector3.zero;
        if (input.sqrMagnitude > 0.01f)
        {
            input = input.normalized;

            float camYaw = cam != null ? cam.eulerAngles.y : 0f;
            Vector3 forward = Quaternion.Euler(0, camYaw, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, camYaw, 0) * Vector3.right;

            moveDir = (forward * input.y + right * input.x).normalized;

            // Rotate character towards movement direction
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        cc.Move(moveDir * speed * Time.deltaTime);

        // Jump — only when grounded, un-crouch on jump
        if (isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            isCrouching = false;
            velocity.y = jumpForce;
            jumpCooldown = 0.25f; // ignore ground for 0.25s after jump
            isGrounded = false;
            if (anim != null) anim.SetTrigger(hashJump);
        }

        // Gravity
        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);

        // Emotes (1-4) — only when standing still and grounded
        if (anim != null && input.sqrMagnitude < 0.01f)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                anim.SetTrigger(hashEmoteWave);
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
                anim.SetTrigger(hashEmoteCheer);
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
                anim.SetTrigger(hashEmoteAngry);
            if (Keyboard.current.digit4Key.wasPressedThisFrame)
                anim.SetTrigger(hashEmoteClap);
        }

        // Animator — feed blend tree parameters
        if (anim != null)
        {
            // Convert world moveDir to local space for strafing blend tree
            Vector3 localMove = transform.InverseTransformDirection(moveDir);

            anim.SetFloat(hashMoveX, localMove.x, 0.1f, Time.deltaTime);
            anim.SetFloat(hashMoveZ, localMove.z, 0.1f, Time.deltaTime);
            anim.SetFloat(hashSpeed, input.magnitude, 0.1f, Time.deltaTime);
            anim.SetBool(hashIsRunning, wantRun && input.sqrMagnitude > 0.01f);
            anim.SetBool(hashIsGrounded, isGrounded);
            anim.SetBool(hashIsCrouching, isCrouching);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (cc == null) cc = GetComponent<CharacterController>();
        if (cc == null) return;

        float castDist = (cc.height / 2f) - cc.radius + groundCheckOffset;
        Vector3 origin = transform.position + cc.center;
        Vector3 end = origin + Vector3.down * castDist;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(end, cc.radius * 0.9f);
        Gizmos.DrawLine(origin, end);
    }
}
