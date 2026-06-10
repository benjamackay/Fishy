#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Fishy.World;
using Fishy.Desconocidos;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Herramientas de montaje de zonas (HDU-2 / HDU-5).
    ///
    /// • Fishy → Configurar Zona Desconocidos (manager + zona bloqueada):
    ///   monta TODO el conjunto del desbloqueo: ZonaDesconocidosManager,
    ///   WorldZoneManager y una BlockedZone conectada (cinemática incluida).
    ///
    /// • Fishy → Crear Zona Bloqueada (BlockedZone):
    ///   crea solo una zona bloqueada adicional.
    ///
    /// Después de crear: mueve la zona, ajusta el tamaño en el BoxCollider2D y usa
    /// clic derecho en BlockedZone → "Ajustar overlay al collider". Guarda con Ctrl+S.
    /// </summary>
    public static class FishyZoneSetup
    {
        private const string SquareSpritePath =
            "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png";

        private static readonly Vector2 DefaultSize = new Vector2(10f, 10f);

        // ── Montaje completo: manager + zona + conexión ─────────────────────────
        [MenuItem("Fishy/Configurar Zona Desconocidos (manager + zona bloqueada)")]
        public static void SetupDesconocidos()
        {
            // 1. ZonaDesconocidosManager (lleva la cuenta de los NPCs).
            var zdm = Object.FindFirstObjectByType<ZonaDesconocidosManager>();
            bool managerCreado = zdm == null;
            if (zdm == null)
            {
                var go = new GameObject("ZonaDesconocidosManager");
                Undo.RegisterCreatedObjectUndo(go, "Crear ZonaDesconocidosManager");
                zdm = go.AddComponent<ZonaDesconocidosManager>();
                zdm.progresoAlCompletar = 25f;   // registra el avance en la BD
            }

            // 2. Zona bloqueada destino (la que se desbloquea con la cinemática).
            BlockedZone zone = null;
            if (!string.IsNullOrEmpty(zdm.siguienteZonaId) && WorldZoneManagerSafe() != null)
                zone = WorldZoneManagerSafe().GetZone(zdm.siguienteZonaId);
            bool zonaCreada = zone == null;
            if (zone == null)
                zone = CreateZoneObject(out _);

            // 3. Conectar manager → zona.
            zdm.siguienteZonaId = zone.zoneId;
            EditorUtility.SetDirty(zdm);

            int npcCount = Object.FindObjectsByType<DesconocidosNPC>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            Selection.activeGameObject = zone.gameObject;
            EditorGUIUtility.PingObject(zone.gameObject);
            EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);

            EditorUtility.DisplayDialog(
                "Fishy — Zona Desconocidos configurada",
                (managerCreado ? "• ZonaDesconocidosManager creado.\n" : "• ZonaDesconocidosManager ya existía.\n") +
                (zonaCreada    ? $"• BlockedZone '{zone.zoneId}' creada y conectada.\n" : $"• BlockedZone '{zone.zoneId}' ya estaba conectada.\n") +
                $"• NPCs Desconocidos detectados en escena: {npcCount}.\n\n" +
                "Ahora:\n" +
                "1. Mueve la zona sobre el área del mapa a bloquear.\n" +
                "2. Ajusta el tamaño en su BoxCollider2D (Size).\n" +
                "3. Clic derecho en BlockedZone → 'Ajustar overlay al collider'.\n" +
                "4. GUARDA LA ESCENA (Ctrl+S) — sin guardar, en Play no existirá.\n\n" +
                "Al terminar de hablar con todos los NPCs verás la cinemática de desbloqueo.",
                "OK");

            Debug.Log($"[Fishy] Zona Desconocidos configurada: manager + zona '{zone.zoneId}' " +
                      $"({npcCount} NPCs detectados). Recuerda guardar la escena (Ctrl+S).");
        }

        // ── Crear solo una zona bloqueada ───────────────────────────────────────
        [MenuItem("Fishy/Crear Zona Bloqueada (BlockedZone)")]
        public static void CreateBlockedZone()
        {
            var zone = CreateZoneObject(out string zoneId);

            // Conectar como zona-gatillante de Desconocidos si está libre.
            string vinculo = "• Sin conectar a ZonaDesconocidosManager (no hay, o ya tiene zona).";
            var zdm = Object.FindFirstObjectByType<ZonaDesconocidosManager>();
            if (zdm != null && string.IsNullOrEmpty(zdm.siguienteZonaId))
            {
                zdm.siguienteZonaId = zoneId;
                EditorUtility.SetDirty(zdm);
                vinculo = $"• ZonaDesconocidosManager.siguienteZonaId = \"{zoneId}\" (conectado).";
            }

            Selection.activeGameObject = zone.gameObject;
            EditorGUIUtility.PingObject(zone.gameObject);
            EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);

            EditorUtility.DisplayDialog(
                "Fishy — Zona Bloqueada creada",
                $"Se creó '{zone.gameObject.name}' (zoneId = \"{zoneId}\").\n\n" + vinculo + "\n\n" +
                "1. Mueve la zona sobre el área a bloquear.\n" +
                "2. Ajusta el tamaño en el BoxCollider2D (Size).\n" +
                "3. Clic derecho en BlockedZone → 'Ajustar overlay al collider'.\n" +
                "4. Guarda la escena (Ctrl+S).",
                "OK");
        }

        // ── Construcción común ──────────────────────────────────────────────────
        private static BlockedZone CreateZoneObject(out string zoneId)
        {
            var existing = Object.FindObjectsByType<BlockedZone>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            zoneId = "zona_" + (existing.Length + 2);   // la primera creada = zona_2

            Vector3 pos = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot
                : Vector3.zero;
            pos.z = 0f;

            var root = new GameObject("ZonaBloqueada_" + zoneId);
            Undo.RegisterCreatedObjectUndo(root, "Crear Zona Bloqueada");
            root.transform.position = pos;

            var box = root.AddComponent<BoxCollider2D>();
            box.size = DefaultSize;
            box.isTrigger = false;          // sólido: Otto se detiene en el borde

            // Overlay oscurecido que cubre el área.
            var overlayGO = new GameObject("Overlay");
            overlayGO.transform.SetParent(root.transform, false);
            var sr = overlayGO.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSquareSprite();
            sr.color = new Color(0f, 0f, 0f, 0.6f);
            sr.sortingOrder = 100;          // por encima del mapa
            if (sr.sprite != null)
            {
                Vector2 spriteSize = sr.sprite.bounds.size;
                overlayGO.transform.localScale = new Vector3(
                    DefaultSize.x / spriteSize.x, DefaultSize.y / spriteSize.y, 1f);
            }

            var zone = root.AddComponent<BlockedZone>();
            zone.zoneId = zoneId;
            zone.overlay = sr;
            zone.isLocked = true;

            // WorldZoneManager: asegurar y registrar TODAS las zonas de la escena.
            var wzm = WorldZoneManagerSafe();
            if (wzm == null)
            {
                var go = new GameObject("WorldZoneManager");
                Undo.RegisterCreatedObjectUndo(go, "Crear WorldZoneManager");
                wzm = go.AddComponent<WorldZoneManager>();
            }
            SyncZoneRules(wzm);
            EditorUtility.SetDirty(wzm);

            return zone;
        }

        private static WorldZoneManager WorldZoneManagerSafe()
            => Object.FindFirstObjectByType<WorldZoneManager>();

        /// <summary>
        /// Registra en el manager todas las BlockedZone de la escena que falten.
        /// (Si la lista quedara parcial, el Awake en runtime ya no autocompleta.)
        /// </summary>
        private static void SyncZoneRules(WorldZoneManager wzm)
        {
            var all = Object.FindObjectsByType<BlockedZone>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var z in all)
            {
                bool registered = false;
                foreach (var rule in wzm.zones)
                    if (rule.zone == z) { registered = true; break; }
                if (!registered)
                    wzm.zones.Add(new WorldZoneManager.ZoneRule { zone = z, progresoRequerido = -1f });
            }
        }

        private static Sprite LoadSquareSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            if (sprite != null) return sprite;
            // Fallback: sprite integrado de uGUI (siempre existe).
            sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            if (sprite == null)
                Debug.LogWarning("[Fishy] No se encontró un sprite para el overlay; " +
                                 "asigna uno manualmente en el SpriteRenderer 'Overlay'.");
            return sprite;
        }
    }
}
#endif
