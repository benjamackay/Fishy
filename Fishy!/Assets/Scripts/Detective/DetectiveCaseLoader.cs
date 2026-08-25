using UnityEngine;

namespace Fishy.Detective
{
    public static class DetectiveCaseLoader
    {
        public static DetectiveCase Load(string resourcePath)
        {
            TextAsset json = Resources.Load<TextAsset>(resourcePath);
            if (json == null)
            {
                Debug.LogError($"[Detective] No se encontró el archivo en Resources: {resourcePath}");
                return null;
            }

            DetectiveCase caso = JsonUtility.FromJson<DetectiveCase>(json.text);

            if (caso == null)
                Debug.LogError($"[Detective] Error al parsear el JSON: {resourcePath}");
            else
                Debug.Log($"[Detective] Caso cargado: {caso.caseId} ({caso.mensajes?.Count} mensajes)");

            return caso;
        }
    }
}