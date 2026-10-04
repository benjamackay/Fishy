#if UNITY_EDITOR
using System.Linq;
using Fishy.Phone;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Deja la Misión 3 jugable en MainScene: los tres testimonios del rumor.
    ///
    ///  - <b>Coipo</b> dice primero su presentación y, la vez siguiente, su testimonio:
    ///    se agrega HDU3_M3_TESTIMONIO_COIPO a sus <c>dialogosSiguientes</c>.
    ///  - <b>Flamenco y Pato Juarjual</b> tienen un NPC propio, con un solo diálogo (su
    ///    testimonio). Son personajes aparte de los que abren los chats de las otras
    ///    misiones: se crean desde Neutral_NPC, junto al launcher de cada uno y con su
    ///    mismo sprite, para que se reconozcan.
    ///
    /// Es idempotente: si un NPC ya dice ese diálogo no se crea otro, así que se puede
    /// correr de nuevo sin duplicar nada. <b>La posición es un punto de partida</b>
    /// (al lado del launcher): hay que mirarla en el editor y moverla si choca con algo.
    ///
    /// Menú: Fishy ▸ Configurar testimonios del pantano. Sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyConfigurarTestimonios.EjecutarBatch `
    ///             -logFile -
    /// </summary>
    public static class FishyConfigurarTestimonios
    {
        private const string Escena = "Assets/Scenes/MainScene.unity";
        private const string PrefabNpc = "Assets/Prefabs/Neutral_NPC.prefab";

        private const string IntroCoipo = "HDU1_NPC_COIPO";
        private const string TestimonioCoipo = "HDU3_M3_TESTIMONIO_COIPO";

        /// <summary>(diálogo, nombre del objeto nuevo, launcher junto al que va, sprite).
        /// El sprite va por ruta y no copiado del launcher: "Flamenco Inv" no tiene uno
        /// propio y el NPC salía invisible.</summary>
        private static readonly (string dialogo, string nombre, string launcher, string sprite)[] Nuevos =
        {
            ("HDU3_M3_TESTIMONIO_FLAMENCO", "Flamenco (testimonio)", "Flamenco Inv",
             "Assets/Sprites/NPCs/flamenco_idle.png"),
            ("HDU3_M3_TESTIMONIO_PATO",     "Pato Juarjual (testimonio)", "Pato",
             "Assets/Sprites/NPCs/pato_juarjual_idle.png"),
        };

        /// <summary>Cuánto se corre el NPC nuevo respecto de su launcher.</summary>
        private static readonly Vector3 Separacion = new Vector3(-1.5f, 0f, 0f);

        [MenuItem("Fishy/Configurar testimonios del pantano")]
        public static void Ejecutar()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log(Configurar());
        }

        public static void EjecutarBatch()
        {
            string resumen = Configurar();
            Debug.Log(resumen);
            EditorApplication.Exit(resumen.Contains("ERROR") ? 1 : 0);
        }

        private static string Configurar()
        {
            var escena = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            var log = new System.Text.StringBuilder("TESTIMONIOS DEL PANTANO\n");

            var npcs = Object.FindObjectsByType<NPC>(FindObjectsInactive.Include);

            // ── Coipo: presentación y después testimonio ──────────────────────
            NPC coipo = npcs.FirstOrDefault(n => n.dialogoId == IntroCoipo);
            if (coipo == null)
            {
                log.AppendLine($"  ERROR  no hay ningún NPC con dialogoId {IntroCoipo}");
                return log.ToString();
            }
            if (coipo.dialogosSiguientes.Contains(TestimonioCoipo))
            {
                log.AppendLine($"  (ya estaba) {coipo.name} sigue con {TestimonioCoipo}");
            }
            else
            {
                Undo.RecordObject(coipo, "Testimonio de Coipo");
                coipo.dialogosSiguientes.Add(TestimonioCoipo);
                PrefabUtility.RecordPrefabInstancePropertyModifications(coipo);
                EditorUtility.SetDirty(coipo);
                log.AppendLine($"  OK     {coipo.name}: {IntroCoipo} → {TestimonioCoipo}");
            }

            // ── Flamenco y Pato: un NPC nuevo cada uno ─────────────────────────
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabNpc);
            if (prefab == null)
            {
                log.AppendLine($"  ERROR  no existe {PrefabNpc}");
                return log.ToString();
            }

            var launchers = Object.FindObjectsByType<PhoneChatLauncher>(FindObjectsInactive.Include);

            foreach (var (dialogo, nombre, nombreLauncher, rutaSprite) in Nuevos)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(rutaSprite);
                if (sprite == null)
                    log.AppendLine($"  AVISO  no encontré el sprite {rutaSprite}");

                NPC existente = npcs.FirstOrDefault(n => n.DiceDialogo(dialogo));
                if (existente != null)
                {
                    // Ya existe: solo se completa el dibujo si quedó sin uno.
                    var sr = existente.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.sprite == null && sprite != null)
                    {
                        Undo.RecordObject(sr, "Sprite del testimonio");
                        sr.sprite = sprite;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
                        EditorUtility.SetDirty(sr);
                        log.AppendLine($"  OK     {existente.name}: se le puso el sprite que le faltaba");
                    }
                    else
                    {
                        log.AppendLine($"  (ya estaba) {existente.name} dice {dialogo}");
                    }
                    continue;
                }

                PhoneChatLauncher launcher = launchers.FirstOrDefault(l => l.name == nombreLauncher);
                if (launcher == null)
                {
                    log.AppendLine($"  ERROR  no encontré el launcher '{nombreLauncher}' para ubicar {dialogo}");
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
                Undo.RegisterCreatedObjectUndo(go, $"Crear {nombre}");
                go.name = nombre;
                go.transform.SetParent(launcher.transform.parent, worldPositionStays: false);
                go.transform.position = launcher.transform.position + Separacion;
                go.transform.localScale = launcher.transform.localScale;

                // El dibujo del personaje, en la misma capa que el launcher para que
                // no quede detrás del suelo.
                var spriteOrigen = launcher.GetComponentInChildren<SpriteRenderer>(true);
                var spriteNuevo = go.GetComponent<SpriteRenderer>();
                if (spriteNuevo != null)
                {
                    spriteNuevo.sprite = sprite != null ? sprite : spriteOrigen?.sprite;
                    if (spriteOrigen != null)
                    {
                        spriteNuevo.sortingLayerID = spriteOrigen.sortingLayerID;
                        spriteNuevo.sortingOrder = spriteOrigen.sortingOrder;
                        spriteNuevo.flipX = !spriteOrigen.flipX;   // mirando hacia el otro
                    }
                }

                var npc = go.GetComponent<NPC>();
                npc.dialogoId = dialogo;
                npc.dialogueData = null;   // las líneas salen del banco
                npc.dialoguePanel = coipo.dialoguePanel;
                npc.dialogueText = coipo.dialogueText;
                npc.nameText = coipo.nameText;
                npc.portraitImage = coipo.portraitImage;

                // No entrega ninguna misión: sin esto avisaría en consola en cada
                // arranque que no tiene nada que entregar.
                var giver = go.GetComponent<MissionGiver>();
                if (giver != null) Object.DestroyImmediate(giver, allowDestroyingAssets: false);

                EditorUtility.SetDirty(go);
                log.AppendLine($"  OK     {nombre} ({dialogo}) junto a '{nombreLauncher}' en " +
                               $"{go.transform.position}");
            }

            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                log.AppendLine("  ERROR  no se pudo guardar la escena");

            return log.ToString();
        }
    }
}
#endif
