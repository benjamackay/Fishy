using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Fishy.Net
{
    /// <summary>
    /// Inicio de sesion con Google para builds de escritorio (Windows/Mac/Linux),
    /// usando el flujo OAuth 2.0 "Authorization Code + PKCE" para apps instaladas:
    ///
    ///   1. Se abre el navegador del sistema en la pantalla de login de Google.
    ///   2. Un servidor HTTP local (loopback, puerto aleatorio) recibe el
    ///      redirect con el "code" cuando el usuario acepta.
    ///   3. Se intercambia ese code por un id_token directamente con Google.
    ///   4. El id_token se envia al backend (ApiManager.GoogleLogin) para
    ///      verificarlo y crear/loguear al Usuario.
    ///
    /// No requiere client secret (PKCE reemplaza esa necesidad para apps que no
    /// pueden guardar un secreto de forma segura, como un ejecutable de Unity).
    /// </summary>
    public static class GoogleAuthClient
    {
        /// <summary>Client ID de OAuth (tipo "Aplicacion de escritorio") de Google Cloud Console.</summary>
        public static string ClientId = "";

        private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const float TimeoutSeconds = 120f;

        public static void SignIn(MonoBehaviour runner, Action<string> onIdToken, Action<string> onError)
        {
            if (string.IsNullOrEmpty(ClientId))
            {
                onError?.Invoke("Falta configurar el Google Client Id en el ApiManager.");
                return;
            }
            runner.StartCoroutine(SignInRoutine(onIdToken, onError));
        }

        private static IEnumerator SignInRoutine(Action<string> onIdToken, Action<string> onError)
        {
            TcpListener listener;
            int port;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                port = ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            catch (Exception e)
            {
                onError?.Invoke("No se pudo abrir el puerto local para el login de Google: " + e.Message);
                yield break;
            }

            string redirectUri = $"http://127.0.0.1:{port}/";
            string state = RandomUrlSafeToken(16);
            string codeVerifier = RandomUrlSafeToken(48);
            string codeChallenge = CodeChallengeFrom(codeVerifier);

            string authUrl =
                AuthEndpoint +
                "?client_id=" + UnityWebRequest.EscapeURL(ClientId) +
                "&redirect_uri=" + UnityWebRequest.EscapeURL(redirectUri) +
                "&response_type=code" +
                "&scope=" + UnityWebRequest.EscapeURL("openid email profile") +
                "&state=" + state +
                "&code_challenge=" + codeChallenge +
                "&code_challenge_method=S256" +
                "&prompt=select_account";

            var result = new CallbackResult();
            var thread = new Thread(() => ListenForRedirect(listener, result));
            thread.IsBackground = true;
            thread.Start();

            Application.OpenURL(authUrl);

            float elapsed = 0f;
            while (!result.Done && elapsed < TimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            try { listener.Stop(); } catch { /* ya pudo haberse cerrado solo */ }

            if (!result.Done)
            {
                onError?.Invoke("Se agoto el tiempo de espera del login con Google.");
                yield break;
            }
            if (!string.IsNullOrEmpty(result.Error))
            {
                onError?.Invoke("Google rechazo el inicio de sesion: " + result.Error);
                yield break;
            }
            if (string.IsNullOrEmpty(result.Code))
            {
                onError?.Invoke("Inicio de sesion con Google cancelado.");
                yield break;
            }
            if (result.State != state)
            {
                onError?.Invoke("Respuesta de Google invalida (state no coincide).");
                yield break;
            }

            yield return ExchangeCodeForIdToken(result.Code, redirectUri, codeVerifier, onIdToken, onError);
        }

        // ── Servidor loopback (hilo aparte, para no bloquear el hilo principal de Unity) ──
        private class CallbackResult
        {
            public volatile bool Done;
            public string Code;
            public string State;
            public string Error;
        }

        private static void ListenForRedirect(TcpListener listener, CallbackResult result)
        {
            try
            {
                using TcpClient client = listener.AcceptTcpClient();
                using NetworkStream stream = client.GetStream();

                var buffer = new byte[8192];
                int read = stream.Read(buffer, 0, buffer.Length);
                string request = Encoding.UTF8.GetString(buffer, 0, read);
                string requestLine = request.Split('\n')[0];
                string[] parts = requestLine.Split(' ');
                string pathAndQuery = parts.Length > 1 ? parts[1] : "/";

                var query = ParseQueryString(pathAndQuery);
                query.TryGetValue("code", out result.Code);
                query.TryGetValue("state", out result.State);
                query.TryGetValue("error", out result.Error);

                bool ok = !string.IsNullOrEmpty(result.Code);
                string title = ok ? "Sesion iniciada" : "No se pudo iniciar sesion";
                string body = ok
                    ? "Ya puedes volver a Fishy! y continuar."
                    : "Vuelve a intentarlo desde el juego.";
                string html =
                    "<html><head><meta charset='utf-8'></head><body style=\"font-family:sans-serif;" +
                    "text-align:center;margin-top:15%;background:#0b1220;color:#eef2f7\">" +
                    $"<h2>{title}</h2><p>{body}</p></body></html>";

                byte[] bodyBytes = Encoding.UTF8.GetBytes(html);
                byte[] header = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: text/html; charset=utf-8\r\n" +
                    $"Content-Length: {bodyBytes.Length}\r\n" +
                    "Connection: close\r\n\r\n");

                stream.Write(header, 0, header.Length);
                stream.Write(bodyBytes, 0, bodyBytes.Length);
                stream.Flush();
            }
            catch (Exception e)
            {
                result.Error = e.Message;
            }
            finally
            {
                result.Done = true;
            }
        }

        private static Dictionary<string, string> ParseQueryString(string pathAndQuery)
        {
            var dict = new Dictionary<string, string>();
            int qIndex = pathAndQuery.IndexOf('?');
            if (qIndex < 0) return dict;

            string query = pathAndQuery.Substring(qIndex + 1);
            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq < 0) continue;
                string key = Uri.UnescapeDataString(pair.Substring(0, eq));
                string value = Uri.UnescapeDataString(pair.Substring(eq + 1));
                dict[key] = value;
            }
            return dict;
        }

        // ── Intercambio del code por tokens ─────────────────────────────────────
        private static IEnumerator ExchangeCodeForIdToken(string code, string redirectUri, string codeVerifier,
            Action<string> onIdToken, Action<string> onError)
        {
            var form = new WWWForm();
            form.AddField("client_id", ClientId);
            form.AddField("code", code);
            form.AddField("code_verifier", codeVerifier);
            form.AddField("grant_type", "authorization_code");
            form.AddField("redirect_uri", redirectUri);

            using var req = UnityWebRequest.Post(TokenEndpoint, form);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke("No se pudo validar el login con Google: " + req.downloadHandler.text);
                yield break;
            }

            TokenResponse token;
            try
            {
                token = JsonConvert.DeserializeObject<TokenResponse>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                onError?.Invoke("Respuesta de Google invalida: " + e.Message);
                yield break;
            }

            if (token == null || string.IsNullOrEmpty(token.id_token))
            {
                onError?.Invoke("Google no devolvio un id_token.");
                yield break;
            }

            onIdToken?.Invoke(token.id_token);
        }

        [Serializable]
        private class TokenResponse
        {
            public string access_token;
            public string id_token;
            public string token_type;
            public int expires_in;
            public string scope;
        }

        // ── PKCE / utilidades ────────────────────────────────────────────────────
        private static string RandomUrlSafeToken(int byteLength)
        {
            var bytes = new byte[byteLength];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string CodeChallengeFrom(string verifier)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        /// <summary>
        /// Decodifica (sin verificar firma) el payload de un JWT para leer email/nombre.
        /// Solo se usa en modo local/offline, donde no hay backend que valide el token.
        /// </summary>
        public static bool TryDecodePayload(string idToken, out string email, out string name, out string sub)
        {
            email = null; name = null; sub = null;
            try
            {
                string[] parts = idToken.Split('.');
                if (parts.Length < 2) return false;
                string payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                var payload = JsonConvert.DeserializeObject<Dictionary<string, object>>(payloadJson);
                if (payload == null) return false;
                if (payload.TryGetValue("email", out var e)) email = e?.ToString();
                if (payload.TryGetValue("name", out var n)) name = n?.ToString();
                if (payload.TryGetValue("sub", out var s)) sub = s?.ToString();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] Base64UrlDecode(string input)
        {
            string s = input.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }
    }
}
