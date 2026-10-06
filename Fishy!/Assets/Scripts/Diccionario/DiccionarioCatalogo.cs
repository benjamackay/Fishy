using System;
using System.Collections.Generic;
using UnityEngine;
using Fishy.Net;

namespace Fishy.Diccionario
{
    /// <summary>Un término y su definición, tal como salen de diccionario.json.</summary>
    [Serializable]
    public class TerminoDiccionario
    {
        public string termino;
        public string definicion;
    }

    /// <summary>
    /// El diccionario de una zona: el banner con su color y la lista de términos.
    ///
    /// <see cref="colorTexto"/> existe porque el color del banner lo elige el diseño
    /// de cada zona y no todos aguantan texto blanco: sobre el celeste del arrecife
    /// (#69C4D4) el blanco queda en 1.9:1, por debajo del 3:1 que necesita un texto
    /// grande para leerse. Esa zona pide negro; las demás lo dejan vacío.
    /// </summary>
    [Serializable]
    public class ZonaDiccionario
    {
        public string id;
        public string titulo;
        public string color;
        public string colorTexto;
        public List<TerminoDiccionario> terminos = new List<TerminoDiccionario>();
    }

    /// <summary>Envoltorio: JsonUtility no sabe leer un arreglo suelto en la raíz.</summary>
    [Serializable]
    public class DiccionarioRaiz
    {
        public List<ZonaDiccionario> zonas = new List<ZonaDiccionario>();
    }

    /// <summary>
    /// Lee <c>Resources/diccionario.json</c> una vez y lo deja en memoria.
    ///
    /// Es el mismo patrón que el banco de preguntas y los casos del Modo Detective
    /// (<c>BancoPreguntasLoader</c>, <c>DetectiveCaseLoader</c>): contenido en JSON
    /// dentro de Resources, leído con JsonUtility. Va empaquetado con el juego, así
    /// que está disponible desde el primer frame y no depende del backend.
    /// </summary>
    public static class DiccionarioCatalogo
    {
        public const string Ruta = "diccionario";

        private static DiccionarioRaiz _cache;

        /// <summary>Todas las zonas con diccionario. Vacío si el archivo no está.</summary>
        public static IReadOnlyList<ZonaDiccionario> Zonas => Cargar().zonas;

        /// <summary>
        /// El diccionario de esa zona, o null si no tiene. Null no es un error: una
        /// zona sin entrada sencillamente no muestra la pantalla.
        /// </summary>
        public static ZonaDiccionario De(string zonaId)
        {
            if (string.IsNullOrWhiteSpace(zonaId)) return null;

            string buscado = zonaId.Trim();
            foreach (var zona in Cargar().zonas)
            {
                if (zona != null && string.Equals(zona.id, buscado, StringComparison.OrdinalIgnoreCase))
                    return zona;
            }
            return null;
        }

        /// <summary>Vuelve a leer el archivo. Sólo hace falta al editarlo en caliente.</summary>
        public static void Recargar() => _cache = null;

        private static DiccionarioRaiz Cargar()
        {
            if (_cache != null) return _cache;

            var asset = Resources.Load<TextAsset>(Ruta);
            if (asset == null)
            {
                Debug.LogWarning($"[Diccionario] No se encontró Resources/{Ruta}.json. " +
                                 "El diccionario de zona no se va a mostrar.");
                _cache = new DiccionarioRaiz();
                return _cache;
            }

            try
            {
                _cache = JsonUtility.FromJson<DiccionarioRaiz>(asset.text) ?? new DiccionarioRaiz();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Diccionario] Resources/{Ruta}.json está mal formado: {e.Message}");
                _cache = new DiccionarioRaiz();
            }

            if (_cache.zonas == null) _cache.zonas = new List<ZonaDiccionario>();
            return _cache;
        }
    }

    /// <summary>
    /// Qué diccionarios ya vio esta partida, para no volver a interrumpir al niño/a
    /// cada vez que cruza un borde.
    ///
    /// <b>Por qué PlayerPrefs y no el backend.</b> El progreso de zona que guarda el
    /// servidor (<c>ZonaProgresoDto</c>) sólo tiene <c>desbloqueada</c> y
    /// <c>completada</c>; anotar esto allá pide un campo nuevo en la base. Se guarda
    /// local, igual que ya hace ApiManager con el personaje en modo sin servidor.
    ///
    /// El costo: vive en este computador, no en la cuenta. Si la misma partida se
    /// abre en otra máquina el diccionario vuelve a salir una vez — que es una
    /// molestia menor y no un dato perdido, porque la pantalla no guarda nada del
    /// jugador. El día que el backend tenga el campo, sólo cambia esta clase.
    /// </summary>
    public static class DiccionarioVistos
    {
        /// <summary>Marca al terminar, no al mostrar: si el juego se cierra con la
        /// pantalla abierta, tiene que volver a salir.</summary>
        public static void Marcar(string zonaId)
        {
            if (string.IsNullOrWhiteSpace(zonaId)) return;
            PlayerPrefs.SetInt(Clave(zonaId), 1);
            PlayerPrefs.Save();
        }

        public static bool YaVisto(string zonaId)
        {
            if (string.IsNullOrWhiteSpace(zonaId)) return false;
            return PlayerPrefs.GetInt(Clave(zonaId), 0) == 1;
        }

        /// <summary>Olvida lo visto de la partida en curso. Para probar sin borrar
        /// la partida entera.</summary>
        public static void Olvidar()
        {
            foreach (var zona in DiccionarioCatalogo.Zonas)
                if (zona != null) PlayerPrefs.DeleteKey(Clave(zona.id));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// La clave lleva el id de partida para que dos partidas del mismo computador
        /// no se pisen. Sin partida en curso cae a 0, que es un cajón compartido: pasa
        /// sólo en escenas de prueba, donde da igual.
        /// </summary>
        private static string Clave(string zonaId)
        {
            int partida = 0;
            var api = ApiManager.Instance;
            if (api != null && api.PartidaId.HasValue) partida = api.PartidaId.Value;

            return $"fishy.diccionario.{partida}.{zonaId.Trim()}";
        }
    }
}
