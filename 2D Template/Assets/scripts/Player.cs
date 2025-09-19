using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public float moveSpeed;
    public float jumpheight;
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 3f;
    public Animator animator; 
    
    Vector2 startPosition;

    private void Awake()
    {
        startPosition = transform.position;
    }

    // Which layers should the player phase through while dashing (set in Inspector).
    // Do NOT include your ground/floor layer here.
    public LayerMask phaseLayerMask;

    // Ground layer for jump reset (set in Inspector)
    public LayerMask groundLayer;

    // Optional: assign a Transform used to check for ground contacts (feet position).
    // If left empty we compute a check point at the bottom of the player's collider.
    public Transform groundCheck;
    public float groundCheckRadius = 0.08f;

    // Optional: assign a TrailRenderer on the player to show a trail during dash.
    // If left empty a TrailRenderer will be created at runtime (no external asset required).
    public TrailRenderer dashTrail;

    // Runtime-created trail settings (used only when dashTrail not assigned)
    public Color runtimeTrailColor = Color.cyan;
    public float runtimeTrailWidth = 0.2f;
    public float runtimeTrailTime = 0.25f;

    // Jump control
    public int maxJumps = 2; // allow up to double-jump
    private int jumpCount = 0;

    // NEW: Assign scene GameObjects (roots) here in the Inspector that the player should NOT phase through while dashing.
    // This prevents both per-collider ignoring and also excludes the assets' layers from global layer ignores.
    [Tooltip("Assign scene GameObjects (roots) that the player should NOT phase through while dashing.")]
    public List<GameObject> doNotPhaseAssets = new List<GameObject>();

    private Rigidbody2D rb2d;
    private float _movment;

    // Dash state
    private bool isDashing = false;
    private float lastDashTime = -Mathf.Infinity;
    private float dashEndTime = 0f;
    private int dashDirection = 1;

    // Collision handling
    private Collider2D playerCollider;
    // Track per-collider ignore state so we can restore after dash
    private List<Collider2D> ignoredColliders = new List<Collider2D>();

    // Grounded flag (computed each frame)
    private bool grounded = false;

    // Dash UI
    private Image dashFillImage;
    private Text dashCooldownText;
    private GameObject dashUIRoot;

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        // ensure jumpCount is reset at start
        jumpCount = 0;

        // If no TrailRenderer assigned, create one at runtime so no external asset is required.
        if (dashTrail == null)
        {
            GameObject trObj = new GameObject("RuntimeDashTrail");
            trObj.transform.SetParent(transform, false);
            dashTrail = trObj.AddComponent<TrailRenderer>();
            dashTrail.time = runtimeTrailTime;
            dashTrail.startWidth = runtimeTrailWidth;
            dashTrail.endWidth = 0f;
            dashTrail.numCapVertices = 4;
            dashTrail.autodestruct = false;

            // Use a simple built-in shader material created at runtime (no asset files required).
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.hideFlags = HideFlags.DontSave;
            dashTrail.material = mat;

            dashTrail.startColor = runtimeTrailColor;
            dashTrail.endColor = new Color(runtimeTrailColor.r, runtimeTrailColor.g, runtimeTrailColor.b, 0f);
        }

        if (dashTrail != null) dashTrail.emitting = false;

        CreateDashUI();
        UpdateDashUI(); // initialize UI display
    }

    void Update()
    {
        // Ground check: prefer explicit groundCheck transform; otherwise compute a point at bottom of player's collider.
        Vector2 checkPoint;
        if (groundCheck != null)
        {
            checkPoint = groundCheck.position;
        }
        else if (playerCollider != null)
        {
            Bounds b = playerCollider.bounds;
            checkPoint = new Vector2(b.center.x, b.min.y - 0.01f);
        }
        else
        {
            checkPoint = transform.position;
        }

        // Primary grounded detection uses configured groundLayer
        grounded = Physics2D.OverlapCircle(checkPoint, groundCheckRadius, groundLayer);

        // Reset jump count only when firmly grounded.
        if (grounded)
        {
            jumpCount = 0;
        }
        else
        {
            // Keep existing fallback for projects that used IsTouchingLayers previously
            if (playerCollider != null && playerCollider.IsTouchingLayers(groundLayer))
            {
                jumpCount = 0;
                grounded = true;
            }
        }

        // Maintain dash velocity while dashing
        if (isDashing)
        {
            rb2d.linearVelocity = new Vector2(dashDirection * dashSpeed, rb2d.linearVelocity.y);
            if (Time.time >= dashEndTime)
            {
                EndDash();
            }
        }
        else
        {
            rb2d.linearVelocity = new Vector2(_movment, rb2d.linearVelocity.y);
        }

        // Enter key dash: only when not currently dashing, cooldown passed and player is moving.
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
        {
            // Determine movement direction from input first, fallback to current velocity.
            float movementValue = Mathf.Abs(_movment) > 0.001f ? _movment : rb2d.linearVelocity.x;
            if (!isDashing && Time.time - lastDashTime > dashCooldown && Mathf.Abs(movementValue) > 0.01f)
            {
                int dir = movementValue > 0f ? 1 : -1;
                StartDash(dir);
                animator.SetTrigger("Dash");
            }
        }

        UpdateDashUI();
        animator.SetFloat("Speed", _movment);
    }

    public void Move(InputAction.CallbackContext ctx)
    {
        float inputX = ctx.ReadValue<Vector2>().x;
        _movment = inputX * moveSpeed;
    }

    public void Jump(InputAction.CallbackContext ctx)
    {
        if (ctx.ReadValue<float>() == 1)
        {
            // Allow jump only if under maxJumps (prevents more than allowed jumps)
            // This allows the player to perform up to maxJumps (double jump by default),
            // and requires touching the ground to reset the count.
            if (jumpCount < maxJumps)
            {
                rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, jumpheight);
                jumpCount++;
                animator.SetTrigger("Jump");
            }
        }
    }

    private void StartDash(int direction)
    {
        if (isDashing) return;

        isDashing = true;
        dashDirection = direction;
        dashEndTime = Time.time + dashDuration;
        lastDashTime = Time.time;

        // Enable trail if assigned
        if (dashTrail != null)
        {
            dashTrail.Clear();
            dashTrail.emitting = true;
            // Ensure trail length is appropriate for the dash
            dashTrail.time = Mathf.Max(dashDuration, dashTrail.time);
        }

        // Ignore collisions with layers specified in phaseLayerMask so player can "phase through" them.
        // Do NOT include ground/floor in that mask so floor collisions remain.
        SetPhaseIgnore(true);

        // Additionally, ignore all individual Collider2D instances that are NOT on groundLayer.
        // This ensures BoxCollider2D (and other collider types) are phased through during dash.
        IgnoreNonGroundColliders(true);
    }

    private void EndDash()
    {
        isDashing = false;

        // Disable trail
        if (dashTrail != null)
        {
            dashTrail.emitting = false;
        }

        // Restore per-collider collisions for phased colliders
        IgnoreNonGroundColliders(false);

        // Restore collisions for phased layers
        SetPhaseIgnore(false);
    }

    private void SetPhaseIgnore(bool ignore)
    {
        // Apply IgnoreLayerCollision between player's layer and every layer in phaseLayerMask,
        // but explicitly exclude any layers included in groundLayer to ensure floor always collides.
        // ALSO exclude layers used by doNotPhaseAssets so those assets are never globally ignored.
        int playerLayer = gameObject.layer;

        // Compute effective mask = phaseLayerMask MINUS groundLayer bits
        int effectiveMask = phaseLayerMask.value & ~groundLayer.value;

        // Exclude layers used by the doNotPhaseAssets (if any)
        if (doNotPhaseAssets != null)
        {
            int assetLayers = 0;
            foreach (var asset in doNotPhaseAssets)
            {
                if (asset != null)
                {
                    assetLayers |= GetHierarchyLayerMask(asset);
                }
            }
            effectiveMask &= ~assetLayers;
        }

        for (int layer = 0; layer < 32; layer++)
        {
            if (((1 << layer) & effectiveMask) != 0)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, layer, ignore);
            }
        }
    }

    private void IgnoreNonGroundColliders(bool ignore)
    {
        if (playerCollider == null) return;

        if (ignore)
        {
            ignoredColliders.Clear();
            // Find all 2D colliders in scene and ignore collisions with those not on ground layers.
            Collider2D[] all = FindObjectsOfType<Collider2D>();
            foreach (var col in all)
            {
                if (col == playerCollider) continue;
                if (col.isTrigger) continue; // don't modify trigger behavior

                // If the collider belongs to any of the doNotPhaseAssets (or their children), skip it.
                bool belongsToProtected = false;
                if (doNotPhaseAssets != null)
                {
                    foreach (var asset in doNotPhaseAssets)
                    {
                        if (asset != null && IsChildOfOrSame(col.gameObject, asset))
                        {
                            belongsToProtected = true;
                            break;
                        }
                    }
                }
                if (belongsToProtected) continue;

                int layerBit = 1 << col.gameObject.layer;
                // Skip colliders that belong to groundLayer (we want to keep floor collisions)
                if ((groundLayer.value & layerBit) != 0) continue;
                // Ignore collision between player and this collider
                Physics2D.IgnoreCollision(playerCollider, col, true);
                ignoredColliders.Add(col);
            }
        }
        else
        {
            // Restore collisions for previously ignored colliders
            for (int i = 0; i < ignoredColliders.Count; i++)
            {
                var col = ignoredColliders[i];
                if (col != null)
                {
                    Physics2D.IgnoreCollision(playerCollider, col, false);
                }
            }
            ignoredColliders.Clear();
        }
    }

    // Helper: returns a bitmask of layers used by go and all its children
    private int GetHierarchyLayerMask(GameObject go)
    {
        if (go == null) return 0;
        int mask = 0;
        var transforms = go.GetComponentsInChildren<Transform>(true);
        foreach (var t in transforms)
        {
            mask |= 1 << t.gameObject.layer;
        }
        return mask;
    }

    // Helper: returns true if obj is the same GameObject as root or is a child of root
    private bool IsChildOfOrSame(GameObject obj, GameObject root)
    {
        if (obj == null || root == null) return false;
        var t = obj.transform;
        return t == root.transform || t.IsChildOf(root.transform);
    }

    // When we physically collide with something, treat the first contact with an "upward" normal as ground.
    // This makes the object the player first touches act like floor and resets jump count.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;

        foreach (var contact in collision.contacts)
        {
            // contact.normal points from the other collider to the player.
            // A positive Y (e.g. > 0.5) means the player is touching the top of the collider (standing on it).
            if (contact.normal.y > 0.5f)
            {
                grounded = true;
                jumpCount = 0;
                break;
            }
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision == null) return;

        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                grounded = true;
                // keep jumpCount reset while staying on surface
                jumpCount = 0;
                return;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // On exit we clear grounded; Update() will re-evaluate ground via overlap checks as needed.
        grounded = false;
    }

    // ---- Dash UI helpers ----
    private void CreateDashUI()
    {
        // Root Canvas
        GameObject canvasGO = new GameObject("DashUICanvas");
        canvasGO.transform.SetParent(null);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Root for dash UI elements (smaller than before)
        dashUIRoot = new GameObject("DashUIRoot");
        dashUIRoot.transform.SetParent(canvasGO.transform, false);
        RectTransform rootRT = dashUIRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0f, 1f);
        rootRT.anchorMax = new Vector2(0f, 1f);
        rootRT.pivot = new Vector2(0f, 1f);
        rootRT.anchoredPosition = new Vector2(10f, -10f); // 10 px from top-left
        rootRT.sizeDelta = new Vector2(140f, 28f); // smaller overall UI

        // Create a simple white sprite for images
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        Sprite whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

        // Border image (black) - sits behind background and provides a visible border
        GameObject border = new GameObject("DashBorder");
        border.transform.SetParent(dashUIRoot.transform, false);
        Image borderImg = border.AddComponent<Image>();
        borderImg.sprite = whiteSprite;
        borderImg.color = Color.black;
        RectTransform borderRT = border.GetComponent<RectTransform>();
        borderRT.anchorMin = new Vector2(0f, 0f);
        borderRT.anchorMax = new Vector2(1f, 1f);
        borderRT.offsetMin = Vector2.zero;
        borderRT.offsetMax = Vector2.zero;

        // Background image (inset to reveal border)
        GameObject bg = new GameObject("DashBG");
        bg.transform.SetParent(dashUIRoot.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.sprite = whiteSprite;
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);
        RectTransform bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0f);
        bgRT.anchorMax = new Vector2(1f, 1f);
        // inset by 2 px so border shows
        bgRT.offsetMin = new Vector2(2f, 2f);
        bgRT.offsetMax = new Vector2(-2f, -2f);

        // Fill image (left side) - parented to background so it stays inside the inset area
        GameObject fill = new GameObject("DashFill");
        fill.transform.SetParent(bg.transform, false);
        dashFillImage = fill.AddComponent<Image>();
        dashFillImage.sprite = whiteSprite;
        dashFillImage.type = Image.Type.Filled;
        dashFillImage.fillMethod = Image.FillMethod.Horizontal;
        dashFillImage.fillOrigin = 0;
        dashFillImage.fillAmount = 1f;
        RectTransform fillRT = fill.GetComponent<RectTransform>();
        // left-anchored fixed width, stretch vertically to match background
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.pivot = new Vector2(0f, 0.5f);
        fillRT.sizeDelta = new Vector2(88f, 0f); // width fixed, height stretches to bg
        fillRT.anchoredPosition = new Vector2(2f, 0f); // 2 px inset from bg left

        // Text showing seconds / ready (right side) - parented to background
        GameObject txt = new GameObject("DashText");
        txt.transform.SetParent(bg.transform, false);
        dashCooldownText = txt.AddComponent<Text>();
        dashCooldownText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        dashCooldownText.fontSize = 12;
        dashCooldownText.alignment = TextAnchor.MiddleCenter;
        dashCooldownText.color = Color.white;
        RectTransform txtRT = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0f, 0f);
        txtRT.anchorMax = new Vector2(1f, 1f);
        // Leave space for the fill (88 width + 2 left inset + small gap)
        txtRT.offsetMin = new Vector2(96f, 2f);
        txtRT.offsetMax = new Vector2(-6f, -2f);
    }

    private void UpdateDashUI()
    {
        if (dashFillImage == null || dashCooldownText == null) return;

        // elapsed fraction from last dash towards cooldown completion
        float elapsed = Mathf.Clamp01((Time.time - lastDashTime) / dashCooldown);
        dashFillImage.fillAmount = elapsed;

        if (elapsed >= 1f)
        {
            dashCooldownText.text = "Ready";
            dashFillImage.color = Color.green;
        }
        else
        {
            float remaining = dashCooldown - (Time.time - lastDashTime);
            remaining = Mathf.Max(0f, remaining);
            dashCooldownText.text = Mathf.CeilToInt(remaining).ToString() + "s";
            // color goes from red (start) to yellow (near ready)
            dashFillImage.color = Color.Lerp(Color.red, Color.yellow, elapsed);
        }
    }
    public void Die() { 
        
        transform.position = startPosition;
    }
}