using System.Collections.Generic;
using UnityEngine;

namespace Fishy.Mision
{
    /// <summary>
    /// Encuentra un <see cref="DesafioData"/> por su <c>desafioId</c>.
    ///
    /// Hace falta para restaurar el panel de misión activa: del backend vuelve un id
    /// de texto (<c>MISION_NPC_01</c>) y para pintar la fila se necesita la ficha,
    /// que es la que trae el título y el ícono.
    ///
    /// <b>Los desafíos viven en <c>Resources/Misiones/</c></b> por lo mismo que los
    /// ítems: es la única forma de cargar un asset por código sin que alguien lo haya
    /// arrastrado antes a la escena, y aquí el desafío viene de la base de datos, no
    /// del mapa. Mismo mecanismo que el banco, los casos del Detective y los fondos.
    ///
    /// Vive dentro del assembly <c>Fishy.Mision</c> a propósito: solo necesita
    /// <c>Resources</c> y <c>DesafioData</c>, así que no cruza ninguna barrera de
    /// assemblies y <c>MissionManager</c> puede usarlo directamente.
    /// </summary>
    public static class CatalogoDesafios
    {
        /// <summary>Carpeta dentro de Resources donde viven los DesafioData.</summary>
        public const string CarpetaResources = "Misiones";

        private static Dictionary<string, DesafioData> _porId;

        /// <summary>DesafioData con ese id, o null si no existe ninguno.</summary>
        public static DesafioData Buscar(string desafioId)
        {
            if (string.IsNullOrWhiteSpace(desafioId)) return null;
            Asegurar();
            return _porId.TryGetValue(desafioId.Trim(), out var data) ? data : null;
        }

        /// <summary>Todos los desafíos con id válido, por id.</summary>
        public static IReadOnlyDictionary<string, DesafioData> Todos
        {
            get { Asegurar(); return _porId; }
        }

        /// <summary>Vuelve a leer la carpeta. Solo hace falta al crear fichas en caliente.</summary>
        public static void Recargar()
        {
            _porId = null;
            Asegurar();
        }

        private static void Asegurar()
        {
            if (_porId != null) return;

            _porId = new Dictionary<string, DesafioData>();

            foreach (var data in Resources.LoadAll<DesafioData>(CarpetaResources))
            {
                if (data == null) continue;

                if (string.IsNullOrWhiteSpace(data.desafioId))
                {
                    Debug.LogWarning(
                        $"[CatalogoDesafios] '{data.name}' no tiene desafioId: no se va a poder " +
                        "restaurar en el panel de misión activa.");
                    continue;
                }

                string id = data.desafioId.Trim();
                if (_porId.TryGetValue(id, out var previo))
                {
                    // Dos fichas con el mismo id no solo rompen la restauración: rompen el
                    // juego. `MissionManager` usa el id como llave de diccionario, así que
                    // la segunda misión que se registre devuelve la primera, y completar
                    // una marca la otra. En el backend comparten fila, porque la unicidad
                    // es (partida, mision_id).
                    Debug.LogError(
                        $"[CatalogoDesafios] El desafioId '{id}' está repetido: '{previo.name}' " +
                        $"y '{data.name}'. Se queda el primero, pero hay que darle un id propio " +
                        "a uno de los dos: mientras tanto las dos misiones se pisan entre sí.");
                    continue;
                }

                _porId[id] = data;
            }

            Debug.Log($"[CatalogoDesafios] {_porId.Count} desafío(s) en el catálogo.");
        }
    }
}
