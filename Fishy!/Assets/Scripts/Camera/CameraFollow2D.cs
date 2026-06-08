using UnityEngine;

namespace Fishy.World
{
    /// <summary>
    /// Cámara 2D que sigue a Otto con suavizado, ideal para exploración de mundo
    /// abierto (HDU-5).
    ///
    /// Características:
    ///  • Seguimiento suave (SmoothDamp), sin tirones.
    ///  • "Look-ahead": la cámara se adelanta hacia donde se mueve Otto.
    ///  • Límites opcionales del mapa (por valores o por un Collider2D), para que la
    ///    cámara no muestre fuera del mundo.
    ///
    /// Colócalo en la Main Camera (debe ser ortográfica, como en 2D/URP).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("Objetivo")]
        [Tooltip("A quién sigue la cámara. Si se deja vacío, busca a Otto (OttoController).")]
        public Transform target;
        [Tooltip("Buscar automáticamente a Otto si no hay objetivo.")]
        public bool autoFindOtto = true;

        [Header("Seguimiento")]
        [Tooltip("Desfase respecto al objetivo. La Z debe ser negativa (ej. -10) para 2D.")]
        public Vector3 offset = new Vector3(0f, 0f, -10f);
        [Tooltip("Tiempo de suavizado: más alto = cámara más perezosa. 0 = pegada.")]
        [Range(0f, 1f)] public float smoothTime = 0.18f;
        [Tooltip("Colocar la cámara sobre el objetivo de inmediato al iniciar.")]
        public bool snapOnStart = true;

        [Header("Look-ahead (se adelanta al movimiento)")]
        public bool useLookAhead = true;
        [Tooltip("Cuánto se adelanta la cámara (en unidades) en la dirección de avance.")]
        public float lookAheadDistance = 1.5f;
        [Tooltip("Suavizado del adelanto.")]
        [Range(0f, 2f)] public float lookAheadSmooth = 0.4f;

        [Header("Límites del mapa (opcional)")]
        [Tooltip("Limitar el área visible. Útil para no mostrar fuera del mundo.")]
        public bool useBounds = false;
        [Tooltip("Si se asigna, los límites se toman de este Collider2D (ej. un BoxCollider2D que cubra el mapa).")]
        public Collider2D boundsCollider;
        [Tooltip("Límite inferior-izquierdo (si no hay boundsCollider).")]
        public Vector2 minBounds = new Vector2(-50f, -50f);
        [Tooltip("Límite superior-derecho (si no hay boundsCollider).")]
        public Vector2 maxBounds = new Vector2(50f, 50f);

        private Camera cam;
        private Rigidbody2D targetRb;
        private OttoController otto;

        private Vector3 followVelocity;
        private Vector3 lookAhead;
        private Vector3 lookAheadVelocity;
        private Vector3 lastTargetPos;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            if (target == null && autoFindOtto)
                TryFindOtto();

            if (target != null)
            {
                CacheTarget();
                if (snapOnStart) SnapToTarget();
            }
        }

        /// <summary>Cambia el objetivo a seguir en tiempo de ejecución.</summary>
        public void SetTarget(Transform newTarget, bool snap = false)
        {
            target = newTarget;
            CacheTarget();
            if (snap && target != null) SnapToTarget();
        }

        private void TryFindOtto()
        {
            otto = FindFirstObjectByType<OttoController>();
            if (otto != null) target = otto.transform;
        }

        private void CacheTarget()
        {
            targetRb = target != null ? target.GetComponent<Rigidbody2D>() : null;
            if (target != null) lastTargetPos = target.position;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (autoFindOtto) { TryFindOtto(); CacheTarget(); }
                if (target == null) return;
            }

            // Posición deseada base.
            Vector3 desired = target.position + offset;

            // Adelanto en la dirección de movimiento.
            if (useLookAhead)
            {
                Vector2 vel = GetTargetVelocity();
                Vector3 desiredLook = vel.sqrMagnitude > 0.01f
                    ? (Vector3)(vel.normalized * lookAheadDistance)
                    : Vector3.zero;
                lookAhead = Vector3.SmoothDamp(lookAhead, desiredLook, ref lookAheadVelocity, lookAheadSmooth);
                desired += lookAhead;
            }

            // Suavizado del seguimiento.
            Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref followVelocity, smoothTime);
            pos.z = offset.z; // mantener la profundidad fija
            pos = ClampToBounds(pos);

            transform.position = pos;
            lastTargetPos = target.position;
        }

        private Vector2 GetTargetVelocity()
        {
            if (targetRb != null) return targetRb.linearVelocity;
            // Sin Rigidbody: estimar por el desplazamiento del frame.
            return (Vector2)(target.position - lastTargetPos) / Mathf.Max(Time.deltaTime, 1e-5f);
        }

        /// <summary>Coloca la cámara sobre el objetivo sin suavizado.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            Vector3 pos = target.position + offset;
            pos.z = offset.z;
            pos = ClampToBounds(pos);
            transform.position = pos;
            followVelocity = Vector3.zero;
            lookAhead = Vector3.zero;
            lookAheadVelocity = Vector3.zero;
        }

        private Vector3 ClampToBounds(Vector3 pos)
        {
            Vector2 min, max;
            if (boundsCollider != null)
            {
                min = boundsCollider.bounds.min;
                max = boundsCollider.bounds.max;
            }
            else if (useBounds)
            {
                min = minBounds;
                max = maxBounds;
            }
            else
            {
                return pos; // sin límites = mundo abierto sin recorte
            }

            // Margen = mitad del área visible (sólo cámara ortográfica).
            float halfH = cam != null && cam.orthographic ? cam.orthographicSize : 0f;
            float halfW = halfH * (cam != null ? cam.aspect : 1f);

            // Si el mapa es más pequeño que la vista, centrar en ese eje.
            if (max.x - min.x > 2f * halfW)
                pos.x = Mathf.Clamp(pos.x, min.x + halfW, max.x - halfW);
            else
                pos.x = (min.x + max.x) * 0.5f;

            if (max.y - min.y > 2f * halfH)
                pos.y = Mathf.Clamp(pos.y, min.y + halfH, max.y - halfH);
            else
                pos.y = (min.y + max.y) * 0.5f;

            return pos;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!useBounds && boundsCollider == null) return;
            Vector2 min = boundsCollider != null ? (Vector2)boundsCollider.bounds.min : minBounds;
            Vector2 max = boundsCollider != null ? (Vector2)boundsCollider.bounds.max : maxBounds;
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
            Gizmos.DrawWireCube(center, new Vector3(max.x - min.x, max.y - min.y, 0.1f));
        }
#endif
    }
}
