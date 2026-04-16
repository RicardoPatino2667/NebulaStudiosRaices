using System.Collections;
using UnityEngine;

// ============================================================
//  ENUMS
// ============================================================

public enum WeaponType { None, Machete, BowAndArrow }

// ============================================================
//  CLASES DE CONFIGURACIÓN
// ============================================================

[System.Serializable]
public class MovementSettings
{
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    public float rotationSpeed = 15f;
    public float airControlFactor = 0.4f;

    [Header("Salto")]
    public float jumpForce = 7f;
    public float gravityMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Suelo")]
    public float groundCheckRadius = 0.25f;
    public LayerMask groundLayers;
}

[System.Serializable]
public class WeaponSettings
{
    [Header("Machete")]
    public GameObject machetePrefab;
    public float macheteAttackRate = 0.5f;
    public float macheteDamage = 25f;
    public float macheteRange = 1.8f;
    public LayerMask macheteHitLayers;

    [Header("Arco")]
    public GameObject bowPrefab;
    public GameObject arrowPrefab;
    public float bowChargeTime = 1.2f;
    public float arrowMinSpeed = 12f;
    public float arrowMaxSpeed = 30f;
    public float arrowDamage = 35f;
    public float bowCooldown = 0.8f;
}

[System.Serializable]
public class InteractionSettings
{
    public float interactRange = 2.5f;
    public LayerMask interactLayers;
    public string interactTag = "Interactable";
}

// ============================================================
//  PLAYER CONTROLLER
// ============================================================

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("=== MOVIMIENTO ===")]
    public MovementSettings movement = new MovementSettings();

    [Header("=== ARMAS ===")]
    public WeaponSettings weapons = new WeaponSettings();

    [Header("=== INTERACCIÓN ===")]
    public InteractionSettings interaction = new InteractionSettings();

    [Header("=== REFERENCIAS ===")]
    [Tooltip("Punto vacío en la mano derecha donde se instancian las armas")]
    public Transform weaponAnchor;

    [Tooltip("Punto en los pies del player para detectar suelo")]
    public Transform groundCheck;

    [Header("=== ANIMACIONES ===")]
    public Animator animator;

    // ── Componentes ─────────────────────────────────────────
    private Rigidbody rb;
    private Transform camTransform;

    // ── Movimiento ──────────────────────────────────────────
    private Vector3 moveDirection;
    private bool isSprinting;
    private bool isGrounded;

    // ── Armas ───────────────────────────────────────────────
    private WeaponType currentWeapon = WeaponType.None;
    private GameObject equippedWeaponGO;
    private float nextAttackTime = 0f;
    private float nextBowTime = 0f;
    private bool isChargingBow = false;
    private float bowChargeStart = 0f;

    // ── Interacción ─────────────────────────────────────────
    private IInteractable nearestInteractable;

    // ── Hashes animator ─────────────────────────────────────
    private static readonly int A_Speed = Animator.StringToHash("Speed");
    private static readonly int A_Grounded = Animator.StringToHash("IsGrounded");
    private static readonly int A_Jump = Animator.StringToHash("Jump");
    private static readonly int A_Attack = Animator.StringToHash("Attack");
    private static readonly int A_DrawBow = Animator.StringToHash("DrawBow");
    private static readonly int A_ReleaseBow = Animator.StringToHash("ReleaseBow");
    private static readonly int A_WeaponIdx = Animator.StringToHash("WeaponIndex");

    // ============================================================
    //  AWAKE
    // ============================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // La cámara debe ser independiente del player (ver ThirdPersonCamera más abajo)
        camTransform = Camera.main != null ? Camera.main.transform : null;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ============================================================
    //  UPDATE / FIXED UPDATE
    // ============================================================

    private void Update()
    {
        CheckGround();
        HandleMovementInput();
        HandleJumpInput();
        HandleWeaponSwitch();
        HandleAttackInput();
        HandleInteractionInput();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
        ApplyCustomGravity();
    }

    // ============================================================
    //  MOVIMIENTO
    // ============================================================

    private void HandleMovementInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        isSprinting = Input.GetKey(KeyCode.LeftShift) && isGrounded;

        // Dirección relativa a la cámara, proyectada en el plano horizontal
        if (camTransform != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(camTransform.right, Vector3.up).normalized;
            moveDirection = (forward * v + right * h).normalized;
        }
        else
        {
            moveDirection = new Vector3(h, 0f, v).normalized;
        }

        // Rotar el player solo si hay input
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot,
                Time.deltaTime * movement.rotationSpeed
            );
        }
    }

    private void ApplyMovement()
    {
        float speed = isSprinting ? movement.sprintSpeed : movement.walkSpeed;
        float factor = isGrounded ? 1f : movement.airControlFactor;

        Vector3 desired = moveDirection * speed * factor;
        Vector3 current = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 smoothed = Vector3.Lerp(current, desired, Time.fixedDeltaTime * 15f);

        rb.linearVelocity = new Vector3(smoothed.x, rb.linearVelocity.y, smoothed.z);
    }

    // ============================================================
    //  SALTO Y GRAVEDAD
    // ============================================================

    private void HandleJumpInput()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * movement.jumpForce, ForceMode.Impulse);
            if (animator) animator.SetTrigger(A_Jump);
        }
    }

    private void ApplyCustomGravity()
    {
        if (rb.linearVelocity.y < 0f)
            rb.AddForce(Physics.gravity * (movement.gravityMultiplier - 1f), ForceMode.Acceleration);
        else if (rb.linearVelocity.y > 0f && !Input.GetButton("Jump"))
            rb.AddForce(Physics.gravity * (movement.lowJumpMultiplier - 1f), ForceMode.Acceleration);
    }

    private void CheckGround()
    {
        if (groundCheck == null) return;
        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            movement.groundCheckRadius,
            movement.groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    // ============================================================
    //  SISTEMA DE ARMAS (prefabs)
    // ============================================================

    private void HandleWeaponSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) EquipWeapon(WeaponType.None);
        if (Input.GetKeyDown(KeyCode.Alpha2)) EquipWeapon(WeaponType.Machete);
        if (Input.GetKeyDown(KeyCode.Alpha3)) EquipWeapon(WeaponType.BowAndArrow);

        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            int total = System.Enum.GetValues(typeof(WeaponType)).Length;
            int next = ((int)currentWeapon + (scroll > 0 ? 1 : -1) + total) % total;
            EquipWeapon((WeaponType)next);
        }
    }

    public void EquipWeapon(WeaponType type)
    {
        if (isChargingBow)
        {
            isChargingBow = false;
            if (animator) animator.ResetTrigger(A_DrawBow);
        }

        // Destruir arma actual instanciada
        if (equippedWeaponGO != null)
        {
            Destroy(equippedWeaponGO);
            equippedWeaponGO = null;
        }

        currentWeapon = type;

        // Elegir prefab
        GameObject prefab = null;
        switch (type)
        {
            case WeaponType.Machete: prefab = weapons.machetePrefab; break;
            case WeaponType.BowAndArrow: prefab = weapons.bowPrefab; break;
        }

        // Instanciar en el anchor de la mano
        if (prefab != null)
        {
            Transform anchor = weaponAnchor != null ? weaponAnchor : transform;
            equippedWeaponGO = Instantiate(prefab, anchor);
            equippedWeaponGO.transform.localPosition = Vector3.zero;
            equippedWeaponGO.transform.localRotation = Quaternion.identity;
            equippedWeaponGO.transform.localScale = Vector3.one;
        }

        if (animator) animator.SetInteger(A_WeaponIdx, (int)currentWeapon);
        Debug.Log($"[PlayerController] Arma equipada: {currentWeapon}");
    }

    // ============================================================
    //  ATAQUES
    // ============================================================

    private void HandleAttackInput()
    {
        switch (currentWeapon)
        {
            case WeaponType.Machete: HandleMacheteInput(); break;
            case WeaponType.BowAndArrow: HandleBowInput(); break;
        }
    }

    // ── Machete ─────────────────────────────────────────────

    private void HandleMacheteInput()
    {
        if (Input.GetMouseButtonDown(0)) MacheteAttack();
    }

    private void MacheteAttack()
    {
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + 1f / weapons.macheteAttackRate;

        if (animator) animator.SetTrigger(A_Attack);

        Vector3 center = transform.position
                       + transform.forward * (weapons.macheteRange * 0.5f)
                       + Vector3.up * 0.8f;

        Collider[] hits = Physics.OverlapSphere(
            center, weapons.macheteRange * 0.5f,
            weapons.macheteHitLayers, QueryTriggerInteraction.Ignore
        );

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            hit.GetComponent<IDamageable>()?.TakeDamage(weapons.macheteDamage, gameObject);
            Debug.Log($"[Machete] Golpeó: {hit.name}");
        }
    }

    // ── Arco ────────────────────────────────────────────────

    private void HandleBowInput()
    {
        if (Input.GetMouseButtonDown(0) && Time.time >= nextBowTime)
        {
            isChargingBow = true;
            bowChargeStart = Time.time;
            if (animator) animator.SetBool(A_DrawBow, true);
        }

        if (Input.GetMouseButtonUp(0) && isChargingBow)
        {
            float ratio = Mathf.Clamp01((Time.time - bowChargeStart) / weapons.bowChargeTime);
            FireArrow(ratio);
        }
    }

    private void FireArrow(float chargeRatio)
    {
        isChargingBow = false;
        nextBowTime = Time.time + weapons.bowCooldown;

        if (animator)
        {
            animator.SetBool(A_DrawBow, false);
            animator.SetTrigger(A_ReleaseBow);
        }

        if (weapons.arrowPrefab == null)
        {
            Debug.LogWarning("[PlayerController] Falta asignar el prefab de flecha.");
            return;
        }

        Vector3 fireDir = camTransform != null ? camTransform.forward : transform.forward;
        Vector3 spawnPos = weaponAnchor != null ? weaponAnchor.position
                                                 : transform.position + Vector3.up;

        GameObject arrowGO = Instantiate(weapons.arrowPrefab, spawnPos, Quaternion.LookRotation(fireDir));
        Arrow arrowComp = arrowGO.GetComponent<Arrow>();

        if (arrowComp != null)
        {
            float speed = Mathf.Lerp(weapons.arrowMinSpeed, weapons.arrowMaxSpeed, chargeRatio);
            arrowComp.Initialize(fireDir, speed, weapons.arrowDamage * chargeRatio, gameObject);
        }
        else
        {
            Rigidbody arrowRb = arrowGO.GetComponent<Rigidbody>();
            if (arrowRb != null)
                arrowRb.linearVelocity = fireDir * Mathf.Lerp(weapons.arrowMinSpeed, weapons.arrowMaxSpeed, chargeRatio);
        }

        Debug.Log($"[Arco] Disparado. Carga: {chargeRatio * 100f:F0}%");
    }

    // ============================================================
    //  INTERACCIÓN
    // ============================================================

    private void HandleInteractionInput()
    {
        nearestInteractable = FindNearestInteractable();

        if (Input.GetKeyDown(KeyCode.E) && nearestInteractable != null)
            nearestInteractable.Interact(gameObject);
    }

    private IInteractable FindNearestInteractable()
    {
        Collider[] cols = Physics.OverlapSphere(
            transform.position, interaction.interactRange,
            interaction.interactLayers, QueryTriggerInteraction.Collide
        );

        IInteractable best = null;
        float bestDist = float.MaxValue;

        foreach (var col in cols)
        {
            if (!string.IsNullOrEmpty(interaction.interactTag) && !col.CompareTag(interaction.interactTag)) continue;
            IInteractable candidate = col.GetComponent<IInteractable>();
            if (candidate == null) continue;
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < bestDist) { bestDist = dist; best = candidate; }
        }
        return best;
    }

    // ============================================================
    //  ANIMATOR
    // ============================================================

    private void UpdateAnimator()
    {
        if (!animator) return;
        float spd = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude / movement.sprintSpeed;
        animator.SetFloat(A_Speed, spd, 0.1f, Time.deltaTime);
        animator.SetBool(A_Grounded, isGrounded);
    }

    // ============================================================
    //  GIZMOS
    // ============================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + transform.forward * (weapons.macheteRange * 0.5f) + Vector3.up * 0.8f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, weapons.macheteRange * 0.5f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interaction.interactRange);

        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, movement.groundCheckRadius);
        }
    }
#endif

    // ── Propiedades públicas ─────────────────────────────────
    public WeaponType CurrentWeapon => currentWeapon;
    public bool IsGrounded => isGrounded;
    public bool IsChargingBow => isChargingBow;
    public float BowChargeRatio => isChargingBow
                                                ? Mathf.Clamp01((Time.time - bowChargeStart) / weapons.bowChargeTime)
                                                : 0f;
    public IInteractable NearestInteractable => nearestInteractable;
}


// ============================================================
//  THIRD PERSON CAMERA  ← ponlo en la Main Camera
// ============================================================
//  SETUP:
//  1. Saca la Main Camera del hierarchy del player (nivel raíz).
//  2. Añade este script a la Main Camera.
//  3. Arrastra el player al campo "target".

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Posición")]
    public float distance = 5f;
    public float height = 2f;

    [Header("Suavizado")]
    public float positionDamping = 6f;
    public float rotationDamping = 6f;

    [Header("Órbita con ratón")]
    public float mouseSensitivity = 3f;
    public float minPitch = -20f;
    public float maxPitch = 60f;

    [Header("Colisión")]
    public float collisionRadius = 0.3f;
    public LayerMask collisionLayers;

    private float yaw;
    private float pitch = 15f;

    private void Start()
    {
        if (target == null) return;
        yaw = target.eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Leer ratón
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivotPoint = target.position + Vector3.up * height;
        Vector3 desiredPos = pivotPoint - rotation * Vector3.forward * distance;

        // Evitar que la cámara atraviese paredes
        if (Physics.SphereCast(
                pivotPoint, collisionRadius,
                (desiredPos - pivotPoint).normalized,
                out RaycastHit hit, distance,
                collisionLayers, QueryTriggerInteraction.Ignore))
        {
            desiredPos = hit.point + hit.normal * collisionRadius;
        }

        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * positionDamping);
        transform.LookAt(pivotPoint);
    }

    // El PlayerController puede leer el yaw para saber hacia dónde apunta la cámara
    public float Yaw => yaw;
}


// ============================================================
//  INTERFACES
// ============================================================

public interface IDamageable
{
    void TakeDamage(float amount, GameObject source);
}

public interface IInteractable
{
    void Interact(GameObject interactor);
}


// ============================================================
//  ARROW  (script que va en el prefab de la flecha)
// ============================================================

[RequireComponent(typeof(Rigidbody))]
public class Arrow : MonoBehaviour
{
    private float damage;
    private GameObject owner;
    private Rigidbody rb;
    private bool hasHit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Destroy(gameObject, 10f);
    }

    public void Initialize(Vector3 direction, float speed, float arrowDamage, GameObject shooter)
    {
        damage = arrowDamage;
        owner = shooter;
        rb.linearVelocity = direction * speed;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void FixedUpdate()
    {
        if (!hasHit && rb.linearVelocity.sqrMagnitude > 0.1f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
    }

    private void OnCollisionEnter(Collision col)
    {
        if (hasHit || col.gameObject == owner) return;
        hasHit = true;

        col.gameObject.GetComponent<IDamageable>()?.TakeDamage(damage, owner);

        rb.isKinematic = true;
        transform.SetParent(col.transform);
        Destroy(gameObject, 5f);
    }
}


// ============================================================
//  EJEMPLO DE OBJETO INTERACTUABLE
// ============================================================

public class InteractableObject : MonoBehaviour, IInteractable
{
    [TextArea] public string message = "¡Objeto activado!";
    public UnityEngine.Events.UnityEvent onInteract;

    public void Interact(GameObject interactor)
    {
        Debug.Log($"[Interactable] {message}");
        onInteract?.Invoke();
    }
}