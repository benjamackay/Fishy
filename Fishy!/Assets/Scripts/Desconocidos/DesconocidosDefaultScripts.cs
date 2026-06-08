using System.Collections.Generic;
using UnityEngine;

namespace Fishy.Desconocidos
{
    /// <summary>
    /// Guiones de grooming listos para usar (HDU-2). Permiten tener contenido
    /// funcional sin tener que crear assets a mano. Cada NPC tiene varias fases
    /// que escalan: halago/confianza → temas neutros (discreto) → datos personales
    /// → propuesta de secreto/encuentro a solas.
    ///
    /// Reglas implementadas (criterios de aceptación):
    ///  • Si el niño/a NO comparte datos en cualquier punto → el NPC se aleja y se
    ///    marca el éxito (nodo EndSuccess).
    ///  • Si comparte → el NPC sigue siendo amistoso y pide datos cada vez más
    ///    específicos (nombre → colegio/edad → dirección/rutina → encuentro).
    /// </summary>
    public static class DesconocidosDefaultScripts
    {
        public enum Personaje { Alex, Sam }

        public static GroomingDialogue Create(Personaje personaje)
        {
            return personaje == Personaje.Alex ? CreateAlex() : CreateSam();
        }

        // NPC 1 — "Alex": finge ser un niño de la misma edad para ganar confianza.
        public static GroomingDialogue CreateAlex()
        {
            var d = ScriptableObject.CreateInstance<GroomingDialogue>();
            d.npcName = "Alex";
            d.area = "Bosque de los Desconocidos";
            d.categoriaRiesgo = "grooming";
            d.startNodeId = "a0";
            d.nodes = new List<DialogueNode>
            {
                Node("a0", DialogueNodeKind.Flattery,
                    "¡Hola! 🦦 Qué crack juegas. Soy Alex, tengo 11 igual que tú, seguro 😄.",
                    fakeInfo: true,
                    Choice("Hola 👋", ResponseQuality.Neutral, false, "a1"),
                    Choice("No hablo con desconocidos.", ResponseQuality.Buena, false, "fin_exito")),

                Node("a1", DialogueNodeKind.Neutral,
                    "¿Te gusta venir a este bosque? Yo me conecto siempre al salir del cole.",
                    fakeInfo: false,
                    Choice("Sí, está entretenido.", ResponseQuality.Neutral, false, "a2"),
                    Choice("Prefiero no contarte cosas mías.", ResponseQuality.Buena, false, "fin_exito")),

                Node("a2", DialogueNodeKind.RequestData,
                    "Oye, ¿y cómo te llamas de verdad? Así te agrego 😊",
                    fakeInfo: false,
                    Choice("Te digo mi nombre.", ResponseQuality.Mala, true, "a3"),
                    Choice("Mi nombre no te lo voy a decir.", ResponseQuality.Buena, false, "fin_exito")),

                Node("a3", DialogueNodeKind.RequestData,
                    "¡Bonito nombre! ¿En qué colegio estás y cuántos años tienes? Quizá quedamos cerca 🤔",
                    fakeInfo: false,
                    Choice("Te cuento mi colegio y mi edad.", ResponseQuality.Mala, true, "a4"),
                    Choice("Eso es privado, no te lo digo.", ResponseQuality.Buena, false, "fin_exito")),

                Node("a4", DialogueNodeKind.SecretMeeting,
                    "Genial 😏 ¿Nos juntamos solos un día? No le digas a tus papás, es nuestro secreto 🤫. Pásame tu dirección.",
                    fakeInfo: false,
                    Choice("Te paso mi dirección.", ResponseQuality.Mala, true, "fin_captura"),
                    Choice("¡No! Esto se lo cuento a un adulto.", ResponseQuality.Buena, false, "fin_exito")),

                EndNode("fin_exito", DialogueNodeKind.EndSuccess,
                    "Ah… bueno, da igual. (Alex se aleja molesto.)"),
                EndNode("fin_captura", DialogueNodeKind.EndCaptured,
                    "Perfecto… nos vemos pronto entonces 😈"),
            };
            return d;
        }

        // NPC 2 — "Sam": ofrece regalos (skins) y propone un encuentro a cambio.
        public static GroomingDialogue CreateSam()
        {
            var d = ScriptableObject.CreateInstance<GroomingDialogue>();
            d.npcName = "Sam";
            d.area = "Bosque de los Desconocidos";
            d.categoriaRiesgo = "grooming";
            d.startNodeId = "s0";
            d.nodes = new List<DialogueNode>
            {
                Node("s0", DialogueNodeKind.Flattery,
                    "¡Ey! Eres buenísimo en este juego 🤩. Soy Sam y tengo skins que nadie tiene. Te regalo una si quieres.",
                    fakeInfo: true,
                    Choice("¿En serio? 😮", ResponseQuality.Neutral, false, "s1"),
                    Choice("No, gracias. No hablo con desconocidos.", ResponseQuality.Buena, false, "fin_exito")),

                Node("s1", DialogueNodeKind.Neutral,
                    "Claro, regalo cosas a mis amigos. ¿A qué hora sueles conectarte para coincidir?",
                    fakeInfo: false,
                    Choice("Juego en las tardes.", ResponseQuality.Mala, true, "s2"),
                    Choice("No te digo mis horarios.", ResponseQuality.Buena, false, "fin_exito")),

                Node("s2", DialogueNodeKind.RequestData,
                    "Bacán 😉 ¿Y cómo te llamas y cuántos años tienes?",
                    fakeInfo: false,
                    Choice("Le digo mi nombre y mi edad.", ResponseQuality.Mala, true, "s3"),
                    Choice("Eso es información privada.", ResponseQuality.Buena, false, "fin_exito")),

                Node("s3", DialogueNodeKind.SecretMeeting,
                    "Para darte la skin tenemos que vernos. ¿Vives cerca? Pásame tu dirección y nos juntamos, pero que sea secreto entre los dos 🤫.",
                    fakeInfo: false,
                    Choice("Le paso mi dirección.", ResponseQuality.Mala, true, "fin_captura"),
                    Choice("No. Le voy a contar a mi familia.", ResponseQuality.Buena, false, "fin_exito")),

                EndNode("fin_exito", DialogueNodeKind.EndSuccess,
                    "Bah, qué aburrido… (Sam se va.)"),
                EndNode("fin_captura", DialogueNodeKind.EndCaptured,
                    "Genial… guarda el secreto, ¿ya? 😈"),
            };
            return d;
        }

        // ── Helpers de construcción ─────────────────────────────────────────────
        private static DialogueNode Node(string id, DialogueNodeKind kind, string line, bool fakeInfo,
            params DialogueChoice[] choices)
        {
            return new DialogueNode
            {
                id = id,
                kind = kind,
                npcLine = line,
                npcSharesFakeInfo = fakeInfo,
                choices = new List<DialogueChoice>(choices)
            };
        }

        private static DialogueNode EndNode(string id, DialogueNodeKind kind, string line)
        {
            return new DialogueNode
            {
                id = id,
                kind = kind,
                npcLine = line,
                choices = new List<DialogueChoice>()
            };
        }

        private static DialogueChoice Choice(string text, ResponseQuality q, bool shares, string next)
            => new DialogueChoice(text, q, shares, next);
    }
}
