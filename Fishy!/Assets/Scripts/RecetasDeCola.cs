using System;
using System.Collections.Generic;

namespace Fishy.Net
{
    /// <summary>
    /// Nombre de receta → la llamada al backend que la ejecuta, y los argumentos que
    /// necesita.
    ///
    /// <b>Por qué existe.</b> Un cambio de la cola llevaba un <c>Action</c>: un closure,
    /// que no se puede escribir en disco. Sin poder escribirlo, la cola muere con el
    /// proceso, y eso es exactamente lo que hacía que parar el Play (o un cierre sucio)
    /// perdiera el avance. Partiendo el cambio en <b>(nombre, argumentos)</b> se puede
    /// anotar, y al volver a entrar se rehace la llamada.
    ///
    /// El patrón no es nuevo: <c>EncolarZona</c> ya guardaba un valor y una función para
    /// rehacer el envío. Lo único que cambia es que la función pasa a tener nombre.
    ///
    /// <b>Todo en un solo archivo, a propósito.</b> Si cada sincronizador registrara su
    /// receta al despertar, reproducir el diario dependería de qué componentes se
    /// crearon ya, y una receta sin registrar dejaría su cambio muerto en silencio. Aquí
    /// están siempre las ocho.
    ///
    /// <b>Las recetas no escriben en consola.</b> De los fallos informa la cola, que es
    /// quien sabe si un cambio se va a reintentar o está esperando su turno. Antes cada
    /// sincronizador tenía su propio <c>LogWarning</c> casi idéntico.
    /// </summary>
    public static class RecetasDeCola
    {
        public const string Mision    = "mision";
        public const string Objetivo  = "objetivo";
        public const string Zona      = "zona";
        public const string Objeto    = "objeto";
        public const string Npc       = "npc";
        public const string Detective = "detective";
        public const string Chat      = "chat";
        public const string Progreso  = "progreso";

        // ── Argumentos ────────────────────────────────────────────────────────
        //
        // Una clase por receta en vez de un saco de campos opcionales: el nombre de la
        // receta hace de etiqueta de tipo al deserializar, así que no hace falta nada
        // más para saber qué se está leyendo.

        [Serializable] public class ArgsMision
        {
            public string MisionId;
            public bool Completada;
        }

        [Serializable] public class ArgsObjetivo
        {
            public string MisionId;
            public int Orden;
        }

        [Serializable] public class ArgsZona
        {
            public string Zona;
            public bool Completada;
        }

        [Serializable] public class ArgsObjeto
        {
            public string ObjetoId;
        }

        [Serializable] public class ArgsNpc
        {
            public string NpcId;
            public bool Exito;
        }

        [Serializable] public class ArgsDetective
        {
            public string CasoId;
            public List<string> Marcados;
            public int Aciertos;
            public int TotalRiesgo;
            public float Porcentaje;
        }

        /// <summary>
        /// Una línea de la conversación.
        ///
        /// Se guarda con campos concretos y no ya convertida al cuerpo del endpoint
        /// (<c>Dictionary&lt;string, object&gt;</c>) por dos razones: un diccionario de
        /// objetos vuelve del JSON como <c>JObject</c> y no como lo que era, y el
        /// respaldo a la cadena antigua necesita los campos por separado. Con esto los
        /// dos caminos de subida salen del mismo dato.
        /// </summary>
        [Serializable] public class MensajeDeChat
        {
            public string Tipo;               // start | request | chain
            public string Texto;
            public string Calidad;
            public List<OpcionRespuesta> Opciones;
            public string PreguntaBancoId;
            public string OpcionBancoId;
        }

        [Serializable] public class ArgsChat
        {
            public string Contacto;
            public string Zona;
            public string Categoria;
            public string Cierre;
            public List<MensajeDeChat> Mensajes;
        }

        [Serializable] public class ArgsProgreso
        {
            public float Progreso;
        }

        /// <summary>
        /// Qué tipo hay que deserializar para esa receta. Devuelve null si el nombre no
        /// se reconoce, que es lo que pasa al leer un diario escrito por una versión más
        /// nueva del juego: esa entrada se descarta con un aviso en vez de tirar la
        /// lectura entera.
        /// </summary>
        public static Type TipoDeArgs(string receta)
        {
            switch (receta)
            {
                case Mision:    return typeof(ArgsMision);
                case Objetivo:  return typeof(ArgsObjetivo);
                case Zona:      return typeof(ArgsZona);
                case Objeto:    return typeof(ArgsObjeto);
                case Npc:       return typeof(ArgsNpc);
                case Detective: return typeof(ArgsDetective);
                case Chat:      return typeof(ArgsChat);
                case Progreso:  return typeof(ArgsProgreso);
                default:        return null;
            }
        }

        /// <summary>
        /// La llamada lista para ejecutar, con la forma que espera la cola:
        /// recibe (cuandoSalgaBien, cuandoFalle).
        ///
        /// <paramref name="partida"/> es la partida en la que ocurrió el cambio, no la
        /// que se está jugando. Todos los métodos de escritura del
        /// <see cref="ApiManager"/> aceptan la partida como argumento y los endpoints
        /// son por partida, así que un cambio anotado en la partida 3 se puede subir
        /// mientras se juega la 4. Es lo que permite que el diario no tenga que
        /// descartar nada.
        /// </summary>
        public static Action<Action, Action<string>> Hacer(string receta, object args, int? partida)
        {
            return (ok, error) =>
            {
                var api = ApiManager.Instance;
                if (api == null) { error("No hay ApiManager."); return; }

                switch (receta)
                {
                    case Mision when args is ArgsMision m:
                        api.RegistrarProgresoMision(m.MisionId, m.Completada, partida,
                            onSuccess: _ => ok(), onError: error);
                        return;

                    case Objetivo when args is ArgsObjetivo o:
                        api.RegistrarProgresoObjetivo(o.MisionId, o.Orden, true, partida,
                            onSuccess: _ => ok(), onError: error);
                        return;

                    case Zona when args is ArgsZona z:
                        api.RegistrarProgresoZona(z.Zona, z.Completada, partida,
                            onSuccess: _ => ok(), onError: error);
                        return;

                    case Objeto when args is ArgsObjeto j:
                        api.MarcarObjetoRecogido(j.ObjetoId, partida,
                            onSuccess: _ => ok(), onError: error);
                        return;

                    case Npc when args is ArgsNpc n:
                        api.MarcarNpcTerminado(n.NpcId, n.Exito, partida,
                            onSuccess: _ => ok(), onError: error);
                        return;

                    case Detective when args is ArgsDetective d:
                        api.RegistrarProgresoDetective(d.CasoId, d.Marcados, d.Aciertos,
                            d.TotalRiesgo, d.Porcentaje, partida,
                            onSuccess: _ => ok(), onError: error);
                        return;

                    case Chat when args is ArgsChat c:
                        // Lo delega en el chat porque ahí vive el respaldo a la cadena
                        // antigua, para un servidor que todavía no tenga chats/completo.
                        Fishy.Chat.ChatBackendLogger.SubirConversacion(api, c, partida, ok, error);
                        return;

                    case Progreso when args is ArgsProgreso p:
                        api.ActualizarPartida(p.Progreso, null, partida,
                            onSuccess: _ => ok(), onError: error);
                        return;

                    default:
                        // Receta desconocida, o argumentos del tipo equivocado. Es un
                        // error de programación, no de red: se reporta como fallo para
                        // que quede en el log y no se reintente en silencio para siempre.
                        error($"Receta '{receta}' desconocida o con argumentos de tipo " +
                              $"{(args == null ? "null" : args.GetType().Name)}.");
                        return;
                }
            };
        }

        /// <summary>
        /// Cómo se combinan dos valores de la misma clave, para las recetas que fusionan
        /// en vez de pisar. Null = gana el último, que es lo normal.
        ///
        /// Zona fusiona por OR y progreso por máximo: hay dos escritores de zona con
        /// significados opuestos (desbloqueada / completada) y completar es un camino de
        /// ida, así que un desbloqueo que llegara después degradaría la zona en el
        /// reporte del adulto. El progreso, por lo mismo, no puede retroceder.
        /// </summary>
        public static Func<object, object, object> FusionDe(string receta)
        {
            switch (receta)
            {
                case Zona:
                    return (viejo, nuevo) => new ArgsZona
                    {
                        Zona = (nuevo as ArgsZona)?.Zona ?? (viejo as ArgsZona)?.Zona,
                        Completada = (viejo is ArgsZona a && a.Completada) ||
                                     (nuevo is ArgsZona b && b.Completada),
                    };

                case Progreso:
                    return (viejo, nuevo) => new ArgsProgreso
                    {
                        Progreso = Math.Max(
                            viejo is ArgsProgreso c ? c.Progreso : float.MinValue,
                            nuevo is ArgsProgreso d ? d.Progreso : float.MinValue),
                    };

                default:
                    return null;
            }
        }
    }
}
