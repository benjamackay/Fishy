using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fishy.World
{
    /// <summary>
    /// HDU-5 — Control y exploración libre con Otto (el chungungo).
    ///
    /// Movimiento 2D en cuatro direcciones (vista cenital) con teclado (WASD o
    /// flechas) y/o botones en pantalla (ver <see cref="OttoOnScreenButton"/>).
    ///
    /// Cumple los criterios de aceptación:
    ///  • Al cargar el mundo, Otto aparece en la zona de inicio y se habilita el
    ///    movimiento (<see cref="EnableMovement"/> / <see cref="movementEnabled"/>).
    ///  • Desplazamiento continuo mientras la tecla/botón esté presionado.
    ///  • Caminar / correr (modificador) y un "salto" visual (hop cenital).
    ///  • Al soltar, Otto se detiene de inmediato en su posición actual.
    ///  • Los límites y zonas bloqueadas se resuelven por física (colliders);
    ///    ver <see cref="BlockedZone"/>, que además muestra el mensaje emergente.
    ///
    /// Requiere un Rigidbody2D (Body Type = Dynamic, Gravity Scale = 0) y un
    /// Collider2D en el mismo GameObject de Otto.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class OttoController : MonoBehaviour
    {
        
        [Header("Velocidades")]
        [Tooltip("Velocidad al caminar (unidades/segundo).")]
        public float walkSpeed = 3.5f;
        [Tooltip("Velocidad al correr (manteniendo Shift o el botón Correr).")]
        public float runSpeed = 6.5f;

        [Header("Movimiento")]
        [Tooltip("Si está activo, el movimiento se limita estrictamente a 4 direcciones (sin diagonales).")]
        public bool restrictToFourDirections = false;

        [Header("Salto (hop visual, vista cenital)")]
        [Tooltip("Transform HIJO del sprite que sube/baja al saltar (no el root de Otto). " +
                 "Si se deja vacío: se usa un SpriteRenderer hijo; si el sprite está en el root, " +
                 "se crea un hijo visual automáticamente; si hay Animator, el salto usa el trigger \"Jump\".")]
        public Transform spriteRoot;
        [Tooltip("Altura del salto en unidades.")]
        public float jumpHeight = 0.5f;
        [Tooltip("Duración total del salto en segundos.")]
        public float jumpDuration = 0.35f;

        [Header("Estado")]
        [Tooltip("El movimiento sólo responde cuando esto es true. Se activa al terminar de cargar el mundo.")]
        public bool movementEnabled = false;
        [Tooltip("Habilitar el movimiento automáticamente en Start (mundo ya cargado en la escena).")]
        public bool enableOnStart = true;
        [Tooltip("Punto de aparición. Si se asigna, Otto se coloca aquí al iniciar.")]
        public Transform spawnPoint;

        // ── Componentes ───────────────────────────────────────────────────────
        private Rigidbody2D rb;
        private Animator animator;

        // ── Entrada ───────────────────────────────────────────────────────────
        private InputAction moveAction;
        private InputAction runAction;
        private InputAction jumpAction;

        // Entrada por botones en pantalla (acumulada como banderas por dirección).
        private bool vUp, vDown, vLeft, vRight, vRun;

        // ── Movimiento ────────────────────────────────────────────────────────
        private Vector2 currentMove;          // dirección final usada este frame
        private Vector2 lastFacing = Vector2.down;
        private bool isJumping;
        private Transform jumpVisual;            // hijo que realiza el "hop" (NUNCA el root)
        private Vector3 jumpVisualBaseLocalPos;
        private bool jumpAnimatorOnly;           // true si el salto es sólo animación

        /// <summary>Velocidad actual real (para animaciones/UI). 0 = detenido.</summary>
        public float CurrentSpeed => currentMove.magnitude * (vRun || RunPressed() ? runSpeed : walkSpeed);
        /// <summary>Dirección a la que mira Otto (última dirección de avance).</summary>
        public Vector2 Facing => lastFacing;

        // ──────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponentInChildren<Animator>();

            // Config recomendada para vista cenital: sin gravedad, sin rotación.
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            ResolveJumpVisual();

            BuildInputActions();
        }

        private void BuildInputActions()
        {
            // Movimiento: WASD + Flechas (dos composites sobre la misma acción).
            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            // Soporte gamepad (stick izquierdo + d-pad) por si se usa mando.
            moveAction.AddBinding("<Gamepad>/leftStick");
            moveAction.AddBinding("<Gamepad>/dpad");

            // Correr: Shift / gamepad.
            runAction = new InputAction("Run", InputActionType.Button);
            runAction.AddBinding("<Keyboard>/leftShift");
            runAction.AddBinding("<Keyboard>/rightShift");
            runAction.AddBinding("<Gamepad>/buttonEast");

            // Saltar: Espacio / gamepad sur.
            jumpAction = new InputAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            runAction?.Enable();
            jumpAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            runAction?.Disable();
            jumpAction?.Disable();
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            runAction?.Dispose();
            jumpAction?.Dispose();
        }

        private void Start()
        {
            if (spawnPoint != null)
                rb.position = spawnPoint.position;

            if (enableOnStart)
                EnableMovement();
        }

        /// <summary>
        /// Habilita el movimiento. Llamar cuando el mundo terminó de cargar.
        /// </summary>
        public void EnableMovement() => movementEnabled = true;

        /// <summary>Deshabilita el movimiento (p.ej. durante diálogos/misiones).</summary>
        public void DisableMovement()
        {
            movementEnabled = false;
            currentMove = Vector2.zero;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        /// <summary>
        /// Reubica a Otto en una posición de inicio y lo deja detenido.
        /// </summary>
        public void TeleportTo(Vector3 position)
        {
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
            currentMove = Vector2.zero;
        }

        // ──────────────────────────────────────────────────────────────────────
        private void Update()
        {
            // Leer entrada de teclado/mando + entrada de botones en pantalla.
            Vector2 keyboard = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector2 virtualMove = new Vector2(
                (vRight ? 1f : 0f) - (vLeft ? 1f : 0f),
                (vUp ? 1f : 0f) - (vDown ? 1f : 0f));

            Vector2 move = keyboard + virtualMove;
            if (move.sqrMagnitude > 1f) move.Normalize();

            if (restrictToFourDirections && move != Vector2.zero)
            {
                // Conserva sólo el eje dominante -> movimiento estricto en cruz.
                if (Mathf.Abs(move.x) >= Mathf.Abs(move.y))
                    move = new Vector2(Mathf.Sign(move.x), 0f);
                else
                    move = new Vector2(0f, Mathf.Sign(move.y));
            }

            currentMove = movementEnabled ? move : Vector2.zero;

            if (currentMove != Vector2.zero)
                lastFacing = currentMove.normalized;

            // Salto (sólo si el movimiento está habilitado).
            if (movementEnabled && jumpAction != null && jumpAction.WasPressedThisFrame())
                TryJump();

            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            bool running = vRun || RunPressed();
            float speed = running ? runSpeed : walkSpeed;

            // Movimiento continuo mientras hay entrada; parada inmediata al soltar.
            // La física frena a Otto en límites y zonas bloqueadas (colliders).
            rb.linearVelocity = currentMove * speed;
        }

        private bool RunPressed() => runAction != null && runAction.IsPressed();

        // ── Salto (hop cenital, no afecta colisiones) ──────────────────────────
        /// <summary>
        /// Determina QUÉ transform se anima en el salto. Nunca debe ser el root de
        /// Otto: mover el root sobrescribiría la posición del Rigidbody y haría que
        /// Otto "volviera" a la posición capturada (el spawn). Por eso aquí sólo se
        /// acepta un hijo; si el sprite está en el propio root, se crea un hijo
        /// visual ("OttoVisual") que imita al SpriteRenderer del root.
        /// </summary>
        private void ResolveJumpVisual()
        {
            // 1) Override del inspector, válido sólo si es un hijo (no el root).
            if (spriteRoot != null && spriteRoot != transform)
            {
                jumpVisual = spriteRoot;
                jumpVisualBaseLocalPos = jumpVisual.localPosition;
                return;
            }

            // 2) Un SpriteRenderer que esté en un hijo (no en el root).
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.transform != transform)
                {
                    jumpVisual = sr.transform;
                    jumpVisualBaseLocalPos = jumpVisual.localPosition;
                    return;
                }
            }

            // 3) El sprite está en el root (o no hay hijo): NO movemos el root.
            if (animator != null)
            {
                // Si hay Animator, el salto se resuelve por animación (trigger "Jump").
                jumpAnimatorOnly = true;
                return;
            }

            var rootSr = GetComponent<SpriteRenderer>();
            if (rootSr != null)
            {
                // Creamos un hijo visual que imita al sprite del root y saltamos ese hijo.
                var visualGO = new GameObject("OttoVisual");
                visualGO.transform.SetParent(transform, false);
                visualGO.transform.localPosition = Vector3.zero;
                CopySpriteRenderer(rootSr, visualGO.AddComponent<SpriteRenderer>());
                rootSr.enabled = false; // se muestra el hijo en su lugar
                jumpVisual = visualGO.transform;
                jumpVisualBaseLocalPos = Vector3.zero;
            }
            else
            {
                Debug.LogWarning("[OttoController] Sin sprite hijo ni Animator: el salto no " +
                                 "tendrá efecto visual. Asigna 'spriteRoot' a un hijo con el sprite de Otto.");
            }
        }

        private static void CopySpriteRenderer(SpriteRenderer from, SpriteRenderer to)
        {
            to.sprite = from.sprite;
            to.color = from.color;
            to.flipX = from.flipX;
            to.flipY = from.flipY;
            to.drawMode = from.drawMode;
            to.size = from.size;
            to.sharedMaterial = from.sharedMaterial;
            to.sortingLayerID = from.sortingLayerID;
            to.sortingOrder = from.sortingOrder;
            to.maskInteraction = from.maskInteraction;
            to.spriteSortPoint = from.spriteSortPoint;
        }

        /// <summary>Inicia un salto si no hay uno en curso.</summary>
        public void TryJump()
        {
            if (isJumping) return;
            if (jumpVisual == null && !jumpAnimatorOnly) return;
            StartCoroutine(JumpRoutine());
        }

        private IEnumerator JumpRoutine()
        {
            isJumping = true;
            if (animator != null && HasParam("Jump", AnimatorControllerParameterType.Trigger))
                animator.SetTrigger("Jump");

            if (jumpVisual != null)
            {
                // Hop sobre el hijo: la posición (física) del root NO se toca.
                float t = 0f;
                while (t < jumpDuration)
                {
                    t += Time.deltaTime;
                    float n = Mathf.Clamp01(t / jumpDuration);
                    // Parábola 0->1->0 para el alto del salto.
                    float height = 4f * jumpHeight * n * (1f - n);
                    jumpVisual.localPosition = jumpVisualBaseLocalPos + Vector3.up * height;
                    yield return null;
                }
                jumpVisual.localPosition = jumpVisualBaseLocalPos;
            }
            else
            {
                // Salto sólo por animación.
                yield return new WaitForSeconds(jumpDuration);
            }

            isJumping = false;
        }

        // ── Animaciones (opcional; sólo si hay Animator con esos parámetros) ────
        private void UpdateAnimator()
        {
            if (animator == null) return;
            if (HasParam("MoveX", AnimatorControllerParameterType.Float)) animator.SetFloat("MoveX", lastFacing.x);
            if (HasParam("MoveY", AnimatorControllerParameterType.Float)) animator.SetFloat("MoveY", lastFacing.y);
            if (HasParam("Speed", AnimatorControllerParameterType.Float)) animator.SetFloat("Speed", currentMove.magnitude);
            if (HasParam("IsRunning", AnimatorControllerParameterType.Bool)) animator.SetBool("IsRunning", (vRun || RunPressed()) && currentMove != Vector2.zero);
            // Girar el sprite horizontalmente
            SpriteRenderer sprite = animator.GetComponent<SpriteRenderer>();

            if (sprite != null && currentMove.x != 0)
                sprite.flipX = currentMove.x > 0;
        }

        private bool HasParam(string name, AnimatorControllerParameterType type)
        {
            foreach (var p in animator.parameters)
                if (p.type == type && p.name == name) return true;
            return false;
        }

        // ── API para botones en pantalla (táctil) ──────────────────────────────
        public void SetVirtualUp(bool pressed) => vUp = pressed;
        public void SetVirtualDown(bool pressed) => vDown = pressed;
        public void SetVirtualLeft(bool pressed) => vLeft = pressed;
        public void SetVirtualRight(bool pressed) => vRight = pressed;
        public void SetVirtualRun(bool pressed) => vRun = pressed;

        /// <summary>Mapea una dirección de <see cref="OttoOnScreenButton"/> a las banderas.</summary>
        public void SetVirtualDirection(OttoDirection dir, bool pressed)
        {
            switch (dir)
            {
                case OttoDirection.Up: vUp = pressed; break;
                case OttoDirection.Down: vDown = pressed; break;
                case OttoDirection.Left: vLeft = pressed; break;
                case OttoDirection.Right: vRight = pressed; break;
            }
        }
    }

    public enum OttoDirection { Up, Down, Left, Right }
}
