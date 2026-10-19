#if UNITY_EDITOR
using System;
using System.Reflection;
using Fishy.Net;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Fishy.EditorTools
{
    /// <summary>Verificación de configuración y HTTPS sin crear cuentas ni partidas.</summary>
    public static class FishyPruebasConexion
    {
        private static UnityWebRequest solicitud;
        private static double limite;

        [MenuItem("Fishy/Verificar conexión Railway (solo lectura)")]
        public static void Ejecutar()
        {
            if (solicitud != null) return;
            try
            {
                var url = new Uri(ApiManager.BaseUrl);
                Exigir(url.Scheme == "https" && url.IsDefaultPort && url.AbsolutePath == "/api",
                    "La API debe usar HTTPS, sin puerto interno ni slash final.");
                Exigir(url.Host == "fishy-test.up.railway.app", "El destino debe ser Railway test.");
                Exigir(typeof(ApiManager).GetField("baseUrl", BindingFlags.Instance | BindingFlags.NonPublic) == null,
                    "Una URL serializada antigua no debe sobrescribir el destino remoto.");
                var validar = typeof(ApiManager).GetMethod("RespuestaSaludValida", BindingFlags.Static | BindingFlags.NonPublic);
                Exigir((bool)validar.Invoke(null, new object[] { "{\"status\":\"ok\"}" }), "Salud válida rechazada.");
                foreach (var cuerpo in new[] { "", "<html>proxy</html>", "{}", "{\"status\":\"error\"}", "null" })
                    Exigir(!(bool)validar.Invoke(null, new object[] { cuerpo }), "No debe aceptar una falsa respuesta de salud.");

                // Usa el mismo transporte y certificado TLS que el juego.
                solicitud = UnityWebRequest.Get(ApiManager.BaseUrl + "/health/");
                solicitud.timeout = 30;
                limite = EditorApplication.timeSinceStartup + 35;
                solicitud.SendWebRequest();
                EditorApplication.update += Revisar;
            }
            catch (Exception e) { Terminar(false, e.Message); }
        }

        private static void Revisar()
        {
            if (!solicitud.isDone && EditorApplication.timeSinceStartup < limite) return;
            var validar = typeof(ApiManager).GetMethod("RespuestaSaludValida", BindingFlags.Static | BindingFlags.NonPublic);
            bool ok = solicitud.isDone && solicitud.result == UnityWebRequest.Result.Success &&
                      (bool)validar.Invoke(null, new object[] { solicitud.downloadHandler.text });
            Terminar(ok, ok ? "Configuración y conexión HTTPS verificadas. Django respondió status=ok."
                            : "No se pudo verificar Django: " + solicitud.error);
        }

        private static void Exigir(bool condicion, string mensaje)
        {
            if (!condicion) throw new InvalidOperationException(mensaje);
        }

        private static void Terminar(bool ok, string mensaje)
        {
            EditorApplication.update -= Revisar;
            solicitud?.Dispose();
            solicitud = null;
            if (ok) Debug.Log("[Conexion Railway] OK: " + mensaje);
            else Debug.LogError("[Conexion Railway] FALLO: " + mensaje);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
#endif
