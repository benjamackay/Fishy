#if UNITY_EDITOR
using System.Linq;
using Fishy.Phone;
using Fishy.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Deja jugable la secundaria «El megáfono de Flamenco» en MainScene.
    ///
    ///  - <b>Flamenco</b> con el diálogo HDU1_SEC_FLAMENCO_MEGAFONO y un MissionGiver de
    ///    MISION_SEC_MEGAFONO_FLAMENCO. Es un NPC aparte del que abre los chats, creado
    ///    desde Neutral_NPC junto al launcher "Flama".
    ///  - <b>El megáfono</b> en el pantano, como objeto recogible (ITEM_MEGAFONO). Es una
    ///    copia de otro objeto del mapa que ya funciona —capa, collider y WorldItem
    ///    incluidos— con el dibujo y los datos del megáfono.
    ///  - <b>El ícono</b> del megáfono en la mochila, que no tenía.
    ///
    /// El objetivo de la misión (recogerlo) está en Resources/misiones.json.
    ///
    /// Es idempotente: si Flamenco ya dice ese diálogo o el megáfono ya está en el mapa,
    /// no se crean de nuevo. <b>Las posiciones son un punto de partida</b>: hay que
    /// mirarlas en el editor y moverlas a donde tenga sentido.
    ///
    /// Menú: Fishy ▸ Configurar misión del megáfono.
    /// </summary>
    public static class FishyConfigurarMegafono
    {
        private const string Escena = "Assets/Scenes/MainScene.unity";
        private const string PrefabNpc = "Assets/Prefabs/Neutral_NPC.prefab";
        private const string SpriteFlamenco = "Assets/Sprites/NPCs/flamenco_idle.png";
        private const string SpriteMegafono = "Assets/Sprites/Objetos/Zona2/megafono.png";
        private const string ItemMegafono = "Assets/Resources/Items/megafono.asset";

        private const string Dialogo = "HDU1_SEC_FLAMENCO_MEGAFONO";
        private const string Mision = "MISION_SEC_MEGAFONO_FLAMENCO";
        private const string ObjetoId = "MAINSCENE_MEGAFONO";
        private const string LauncherJunto = "Flama";
        private const string ObjetoModelo = "silbato";
        private const string Zona = "zona_2";

        private static readonly Vector3 SeparacionNpc = new Vector3(1.5f, 0f, 0f);

        /// <summary>Dónde se prueba a dejar el megáfono, respecto de Flamenco: el primero
        /// que caiga dentro del pantano.</summary>
        private static readonly Vector3[] SitiosMegafono =
        {
            new Vector3(-5f, -3f, 0f), new Vector3(5f, -3f, 0f),
            new Vector3(-5f, 3f, 0f),  new Vector3(5f, 3f, 0f), new Vector3(0f, -4f, 0f),
        };

        [MenuItem("Fishy/Configurar misión del megáfono")]
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
            var log = new System.Text.StringBuilder("MISIÓN DEL MEGÁFONO\n");

            var item = AssetDatabase.LoadAssetAtPath<ItemData>(ItemMegafono);
            // La hoja está importada en modo múltiple: el Sprite es un sub-asset.
            var dibujo = AssetDatabase.LoadAllAssetsAtPath(SpriteMegafono).OfType<Sprite>().FirstOrDefault();
            if (item == null) { log.AppendLine($"  ERROR  no existe {ItemMegafono}"); return log.ToString(); }
            if (dibujo == null) log.AppendLine($"  AVISO  no encontré un sprite en {SpriteMegafono}");

            // ── El ícono ──────────────────────────────────────────────────────
            if (item.itemIcon == null && dibujo != null)
            {
                Undo.RecordObject(item, "Ícono del megáfono");
                item.itemIcon = dibujo;
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssetIfDirty(item);
                log.AppendLine("  OK     el megáfono ya tiene ícono en la mochila");
            }
            else log.AppendLine("  (ya estaba) ícono del megáfono");

            // ── Flamenco ──────────────────────────────────────────────────────
            var npcs = Object.FindObjectsByType<DialogoNeutroNPC>(FindObjectsInactive.Include);
            DialogoNeutroNPC flamenco = npcs.FirstOrDefault(n => n.DiceDialogo(Dialogo));
            if (flamenco != null)
            {
                log.AppendLine($"  (ya estaba) {flamenco.name} dice {Dialogo}");
            }
            else
            {
                flamenco = CrearFlamenco(escena, npcs, log);
                if (flamenco == null) return log.ToString();
            }

            var giver = flamenco.GetComponent<MissionGiver>();
            if (giver == null) giver = Undo.AddComponent<MissionGiver>(flamenco.gameObject);
            if (giver.misionId != Mision)
            {
                Undo.RecordObject(giver, "Misión del megáfono");
                giver.desafio = null;
                giver.misionId = Mision;
                giver.objetivos.Clear();   // del catálogo
                PrefabUtility.RecordPrefabInstancePropertyModifications(giver);
                EditorUtility.SetDirty(giver);
                log.AppendLine($"  OK     {flamenco.name} entrega {Mision}");
            }

            // ── El megáfono en el mapa ────────────────────────────────────────
            var objetos = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include);
            WorldItem megafono = objetos.FirstOrDefault(o => o.itemData == item || o.objetoId == ObjetoId);
            if (megafono != null)
            {
                log.AppendLine($"  (ya estaba) '{megafono.name}' en {megafono.transform.position}");
            }
            else
            {
                WorldItem modelo = objetos.FirstOrDefault(o => o.name == ObjetoModelo) ?? objetos.FirstOrDefault();
                if (modelo == null)
                {
                    log.AppendLine("  ERROR  no hay ningún objeto recogible en la escena para copiar");
                    return log.ToString();
                }

                var go = Object.Instantiate(modelo.gameObject, modelo.transform.parent);
                Undo.RegisterCreatedObjectUndo(go, "Crear megáfono");
                go.name = "megafono";
                go.SetActive(true);
                go.transform.position = SitioEnElPantano(flamenco.transform.position, log);

                megafono = go.GetComponent<WorldItem>();
                megafono.itemData = item;
                megafono.objetoId = ObjetoId;
                megafono.quantity = 1;

                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null && dibujo != null) sr.sprite = dibujo;
                var choque = go.GetComponent<BoxCollider2D>();
                if (choque != null && dibujo != null)
                {
                    choque.size = dibujo.bounds.size;
                    choque.offset = dibujo.bounds.center;
                }

                EditorUtility.SetDirty(go);
                log.AppendLine($"  OK     megáfono (copia de '{modelo.name}') en {go.transform.position}");
            }

            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                log.AppendLine("  ERROR  no se pudo guardar la escena");

            return log.ToString();
        }

        private static DialogoNeutroNPC CrearFlamenco(UnityEngine.SceneManagement.Scene escena,
            DialogoNeutroNPC[] npcs, System.Text.StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabNpc);
            if (prefab == null) { log.AppendLine($"  ERROR  no existe {PrefabNpc}"); return null; }

            // Las referencias al panel de diálogo, de cualquier NPC que ya las tenga.
            DialogoNeutroNPC conPanel = npcs.FirstOrDefault(n => n.dialoguePanel != null && n.dialogueText != null);
            if (conPanel == null) { log.AppendLine("  ERROR  ningún NPC tiene el panel de diálogo asignado"); return null; }

            PhoneChatLauncher launcher = Object.FindObjectsByType<PhoneChatLauncher>(FindObjectsInactive.Include)
                .FirstOrDefault(l => l.name == LauncherJunto);
            if (launcher == null) { log.AppendLine($"  ERROR  no encontré el launcher '{LauncherJunto}'"); return null; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
            Undo.RegisterCreatedObjectUndo(go, "Crear Flamenco del megáfono");
            go.name = "Flamenco (megáfono)";
            go.transform.SetParent(launcher.transform.parent, worldPositionStays: false);
            go.transform.position = launcher.transform.position + SeparacionNpc;
            go.transform.localScale = launcher.transform.localScale;

            var spriteOrigen = launcher.GetComponentInChildren<SpriteRenderer>(true);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                var dibujo = AssetDatabase.LoadAllAssetsAtPath(SpriteFlamenco).OfType<Sprite>().FirstOrDefault();
                sr.sprite = dibujo != null ? dibujo : spriteOrigen?.sprite;
                if (spriteOrigen != null)
                {
                    sr.sortingLayerID = spriteOrigen.sortingLayerID;
                    sr.sortingOrder = spriteOrigen.sortingOrder;
                }
            }

            var npc = go.GetComponent<DialogoNeutroNPC>();
            npc.dialogoId = Dialogo;
            npc.dialogueData = null;   // las líneas salen del banco
            npc.dialogosSiguientes.Clear();
            npc.dialoguePanel = conPanel.dialoguePanel;
            npc.dialogueText = conPanel.dialogueText;
            npc.nameText = conPanel.nameText;
            npc.portraitImage = conPanel.portraitImage;

            EditorUtility.SetDirty(go);
            log.AppendLine($"  OK     Flamenco (megáfono) junto a '{LauncherJunto}' en {go.transform.position}");
            return npc;
        }

        /// <summary>El primer sitio de <see cref="SitiosMegafono"/> que caiga dentro de la
        /// zona del pantano. Si ninguno, el primero, con aviso.</summary>
        private static Vector3 SitioEnElPantano(Vector3 flamenco, System.Text.StringBuilder log)
        {
            ZonaMundo pantano = Object.FindObjectsByType<ZonaMundo>(FindObjectsInactive.Include)
                .FirstOrDefault(z => z.Id == Zona);
            if (pantano != null)
            {
                // En el editor no corre Awake, que es quien llena las áreas. Se llenan
                // solo para medir y se dejan como estaban, para no guardarlas en la escena.
                PolygonCollider2D[] antes = pantano.areas;
                if (antes == null || antes.Length == 0)
                    pantano.areas = pantano.GetComponents<PolygonCollider2D>();
                try
                {
                    foreach (Vector3 sitio in SitiosMegafono)
                        if (pantano.Contiene(flamenco + sitio)) return flamenco + sitio;
                }
                finally { pantano.areas = antes; }
            }

            log.AppendLine($"  AVISO  ningún sitio de prueba cae dentro de {Zona}: muévelo a mano");
            return flamenco + SitiosMegafono[0];
        }
    }
}
#endif
