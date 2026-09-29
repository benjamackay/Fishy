using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Fishy.Net
{
    /// <summary>
    /// Lo que la cola todavía no ha conseguido subir, escrito en disco.
    ///
    /// <b>Por qué existe.</b> La cola se diseñó solo en memoria, dando por aceptable
    /// perder lo de la zona actual en un cierre sucio. Medido sobre los logs reales, eso
    /// no era "lo de la zona actual": cuatro paradas del Play en una tarde se llevaron 19
    /// cambios, y entre ellos objetivos de misión que <b>no se pueden recuperar</b>,
    /// porque el chat que los marcaría no se vuelve a disparar. Las misiones sí se
    /// recuperaban solas —<c>MissionManager</c> las escribe en PlayerPrefs— y los
    /// objetivos no, así que la base quedaba afirmando que una misión estaba completa
    /// con 1 de 3 objetivos hechos.
    ///
    /// Con el diario, un cambio deja de depender de que el proceso siga vivo: se anota al
    /// encolarlo y se borra recién cuando el servidor confirma.
    ///
    /// <b>Dónde.</b> Una sola clave de PlayerPrefs con todo dentro. Es lo que este
    /// proyecto ya usa para cuanto persiste —incluido un DTO serializado, ver
    /// <c>ApiManager.GuardarPersonajeLocal</c>—; y una sola clave no tiene índice que se
    /// desincronice ni deja a medias más que la última escritura. Los cuerpos son de unos
    /// pocos KB.
    ///
    /// <b>Qué NO se anota.</b> La mochila y la posición de Otto. Esos dos leen el estado
    /// vivo al subir, así que el siguiente vaciado los manda completos de todas formas:
    /// anotarlos sería guardar una foto vieja de algo que se puede volver a mirar.
    /// </summary>
    public static class DiarioDeCambios
    {
        private const string Clave = "Fishy.Cola.Diario";

        /// <summary>
        /// Un cambio anotado. Es deliberadamente plano y sin enums:
        ///
        /// - <see cref="Familia"/> va como texto para que añadir una familia mañana no
        ///   corrompa un diario escrito hoy.
        /// - <see cref="Args"/> va como JSON anidado, no como objeto, para no tener que
        ///   resolver el tipo al deserializar el sobre: primero se lee el sobre, y el
        ///   nombre de la receta dice qué tipo tienen los argumentos de dentro.
        /// </summary>
        [Serializable]
        public class Entrada
        {
            public string Clave;
            public string Familia;
            public string Receta;
            public string Args;
            public int? Partida;
            public int Orden;
            public int Intentos;
            public string Descripcion;
        }

        /// <summary>Lo anotado ahora mismo, por clave. Es la copia viva; el disco es su reflejo.</summary>
        private static readonly Dictionary<string, Entrada> _entradas = new Dictionary<string, Entrada>();

        private static bool _leido;

        public static int Cuenta => _entradas.Count;

        /// <summary>
        /// Anota o actualiza un cambio. Se llama DESPUÉS de meterlo en el almacén, para
        /// que lo que se escriba sea el valor ya fusionado (una zona que pasó a
        /// completada, un progreso que subió) y no el que llegó suelto.
        /// </summary>
        public static void Anotar(string clave, string familia, string receta, object args,
            int? partida, int orden, int intentos, string descripcion,
            Func<object, object, object> fusion = null)
        {
            if (string.IsNullOrEmpty(clave) || string.IsNullOrEmpty(receta)) return;

            // Las claves que fusionan (zona por OR, progreso por máximo) tienen que
            // hacerlo TAMBIÉN contra lo que ya estaba anotado, no solo contra lo que
            // hubiera en memoria.
            //
            // El caso que lo pide: la zona quedó completada ayer y no alcanzó a subirse;
            // hoy, al recalcular, se encola como "desbloqueada". En memoria no hay nada
            // —la cola arrancó vacía—, así que sin esto se escribiría "desbloqueada"
            // encima de la "completada" del diario y el reporte del adulto pasaría a
            // mostrar como pendiente una zona ya terminada.
            //
            // Fusionar dos veces no molesta: OR y máximo dan lo mismo repetidos.
            if (fusion != null && _entradas.TryGetValue(clave, out Entrada previa))
            {
                object anterior = ArgsDe(previa);
                if (anterior != null) args = fusion(anterior, args);
            }

            try
            {
                _entradas[clave] = new Entrada
                {
                    Clave = clave,
                    Familia = familia,
                    Receta = receta,
                    Args = JsonConvert.SerializeObject(args),
                    Partida = partida,
                    Orden = orden,
                    Intentos = intentos,
                    Descripcion = descripcion,
                };
            }
            catch (Exception e)
            {
                // Un argumento que no se puede serializar es un error de programación, y
                // hay que verlo: significa que ese cambio no va a sobrevivir al cierre.
                Debug.LogError($"[Diario] No se pudo anotar '{clave}': {e.Message}");
                return;
            }

            Escribir();
        }

        /// <summary>El servidor lo confirmó: ya no hace falta guardarlo.</summary>
        public static void Confirmar(string clave)
        {
            if (string.IsNullOrEmpty(clave)) return;
            if (_entradas.Remove(clave)) Escribir();
        }

        /// <summary>
        /// Lo que quedó de sesiones anteriores, para volver a ponerlo en la cola.
        ///
        /// Una entrada con una receta que este juego no conoce —un diario escrito por una
        /// versión más nueva— se descarta avisando, en vez de tirar la lectura entera y
        /// perder también las que sí se entienden.
        /// </summary>
        public static List<Entrada> Leer()
        {
            var lista = new List<Entrada>();
            _leido = true;

            string json = PlayerPrefs.GetString(Clave, "");
            if (string.IsNullOrEmpty(json)) return lista;

            List<Entrada> crudas;
            try
            {
                crudas = JsonConvert.DeserializeObject<List<Entrada>>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Diario] El diario guardado no se pudo leer y se descarta: {e.Message}");
                PlayerPrefs.DeleteKey(Clave);
                PlayerPrefs.Save();
                return lista;
            }

            if (crudas == null) return lista;

            foreach (Entrada entrada in crudas)
            {
                if (entrada == null || string.IsNullOrEmpty(entrada.Clave)) continue;

                if (RecetasDeCola.TipoDeArgs(entrada.Receta) == null)
                {
                    Debug.LogWarning($"[Diario] '{entrada.Clave}' usa la receta " +
                                     $"'{entrada.Receta}', que este juego no conoce. Se descarta.");
                    continue;
                }

                _entradas[entrada.Clave] = entrada;
                lista.Add(entrada);
            }

            return lista;
        }

        /// <summary>Los argumentos ya deserializados al tipo que dice su receta, o null.</summary>
        public static object ArgsDe(Entrada entrada)
        {
            if (entrada == null) return null;

            Type tipo = RecetasDeCola.TipoDeArgs(entrada.Receta);
            if (tipo == null) return null;

            try
            {
                return JsonConvert.DeserializeObject(entrada.Args ?? "", tipo);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Diario] Los argumentos de '{entrada.Clave}' no se " +
                                 $"pudieron leer: {e.Message}");
                return null;
            }
        }

        /// <summary>Se tira todo. Solo para las pruebas y para un borrón deliberado.</summary>
        public static void Limpiar()
        {
            _entradas.Clear();
            PlayerPrefs.DeleteKey(Clave);
            PlayerPrefs.Save();
            _leido = false;
        }

        /// <summary>Descripciones de lo anotado, para poder decírselo al jugador.</summary>
        public static List<string> Descripciones()
        {
            var lista = new List<string>(_entradas.Count);
            foreach (var entrada in _entradas.Values)
                lista.Add(string.IsNullOrEmpty(entrada.Descripcion) ? entrada.Clave : entrada.Descripcion);
            return lista;
        }

        private static void Escribir()
        {
            // Si no se ha leído todavía, escribir pisaría lo que dejó la sesión anterior
            // antes de haberlo recuperado. Pasa si algo encola antes de que la cola
            // despierte, y perdería exactamente lo que este archivo existe para salvar.
            if (!_leido) Leer();

            var lista = new List<Entrada>(_entradas.Values);
            try
            {
                PlayerPrefs.SetString(Clave, JsonConvert.SerializeObject(lista));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Diario] No se pudo escribir el diario: {e.Message}");
            }
        }
    }
}
