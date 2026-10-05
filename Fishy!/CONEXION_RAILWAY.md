# Unity y Django en Railway

En la rama `test`, Unity usa `https://fishy-test.up.railway.app/api`.
No se necesita ejecutar Django localmente. Se requiere conexión a internet.

`Assets/Scripts/ApiManager.cs` define `BaseUrl` para todas las operaciones:
salud, cuentas, perfiles, partidas, chats, inventario, misiones y progreso.
La URL no es un campo del Inspector: las escenas y prefabs antiguos no pueden
reemplazarla por localhost. No se usa el puerto interno 8080.

El contrato de la API no cambia. Las peticiones autenticadas mantienen
`Authorization: Token <token>`; no se reemplaza por JWT ni se incrustan claves.
Unity habla con Django y Django accede a Supabase.

Un fallo de conexión informa un error y permite reintentar; nunca activa
automáticamente el modo local. La simulación explícita del Editor se conserva
para las pruebas existentes, pero se desactiva en los builds. El chequeo de
salud espera hasta 30 segundos y exige JSON con `status: ok`.

## Verificación

En Unity: **Fishy > Verificar conexión Railway (solo lectura)**. Comprueba la
configuración y realiza un GET de salud con UnityWebRequest y TLS normal.
No crea cuentas ni modifica partidas. Para automatizarlo:

```powershell
Unity.exe -batchmode -nographics -projectPath "<ruta>/Fishy!" -executeMethod Fishy.EditorTools.FishyPruebasConexion.Ejecutar -logFile "<ruta>/conexion-unity.log"
```

No agregar `-quit`: la comprobación asíncrona cierra el proceso al terminar.
Para verificar guardado de extremo a extremo, iniciar sesión con una cuenta
autorizada, elegir un perfil, avanzar, cerrar normalmente y volver a entrar.
El progreso debe recuperarse en otro dispositivo usando la misma cuenta.

En una publicación WebGL, el backend también debe autorizar el origen HTTPS
exacto que aloje el juego en `CORS_ALLOWED_ORIGINS`. Esto se configura en Railway;
no hace falta para el Editor ni para un ejecutable de escritorio.

El catálogo empaquetado del juego y la cola de cambios pendientes se conservan:
no son un servidor local ni una confirmación de que el progreso llegó a Django.
