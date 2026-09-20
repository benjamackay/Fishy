using System.Linq;
using Newtonsoft.Json.Linq;

namespace Fishy.UI
{
    /// <summary>
    /// Saca el mensaje que hay dentro de la respuesta de error del backend.
    ///
    /// <see cref="ApiManager"/> entrega el body tal cual, y viene en dos formas segun
    /// quien lo genere: <c>{"error": "..."}</c> en los 401, y
    /// <c>{"campo": ["..."]}</c> en los 400 de los serializers de DRF. Desenvolver eso
    /// es identico en todas las pantallas, asi que vive aca.
    ///
    /// Lo que NO vive aca es la frase final: el mismo "ya existe" del backend se le
    /// cuenta distinto a quien esta creando una cuenta ("ese email ya esta
    /// registrado") que a quien esta creando un perfil de menor ("ya tienes un perfil
    /// con ese nombre"). Cada pantalla mapea sus casos sobre este texto.
    /// </summary>
    public static class TextoDeError
    {
        /// <summary>El mensaje de dentro, o el texto crudo si no era JSON (timeout,
        /// DNS, connection refused).</summary>
        public static string Detalle(string error)
        {
            if (string.IsNullOrEmpty(error)) return string.Empty;

            try
            {
                if (JToken.Parse(error) is JObject obj)
                {
                    var primero = obj.Properties().FirstOrDefault();
                    if (primero != null)
                        return primero.Value is JArray arr && arr.Count > 0
                            ? arr[0].ToString()
                            : primero.Value.ToString();
                }
            }
            catch
            {
                // No era JSON: se usa el texto crudo, que igual es legible.
            }

            return error;
        }

        /// <summary>Deja el mensaje en una linea y acotado, para que no reviente el
        /// sitio donde se va a escribir.</summary>
        public static string Recortar(string detalle, int maximo = 140)
        {
            if (string.IsNullOrEmpty(detalle)) return string.Empty;

            string limpio = detalle.Replace('\n', ' ').Trim();
            return limpio.Length > maximo ? limpio.Substring(0, maximo) + "..." : limpio;
        }
    }
}
