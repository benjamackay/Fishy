# Arranque del juego: Login/Registro + Pantalla de carga

Flujo previo al juego: el niño/a inicia sesión o crea una cuenta y luego se carga
el mundo con una barra de progreso real.

## Scripts

| Script | Rol |
|--------|-----|
| `AuthScreen.cs` | Pantalla de Login/Registro conectada al `ApiManager`. |
| `LoadingScreen.cs` | Pantalla de carga con barra funcional (carga asíncrona de la escena). |
| `UiBootstrap.cs` | Crea un EventSystem con el módulo correcto (nuevo Input System). |

Ambas pantallas **se autogeneran** si no asignas referencias, así que funcionan sin
montar UI a mano (puedes rediseñarlas luego y arrastrar tus propias referencias).

## Configuración en 1 clic (recomendado)

Menú **Fishy → Configurar arranque (Boot + Juego)**:
- Crea `Assets/Scenes/Boot.unity` con un `Bootstrap` (AuthScreen + LoadingScreen).
- Registra en Build Settings `Boot` (índice 0) y la escena del juego (`SampleScene`).

Luego abre la escena **Boot** y pulsa **Play**: tras la carga, se cambia solo a la
escena del juego. (En el Editor, Play usa la escena ABIERTA; por eso hay que abrir
Boot. En una build, arranca sola desde el índice 0.)

## Montaje manual

1. **Escena de arranque**
   - Crea una escena nueva, p.ej. `Boot` (File → New Scene → guardar como `Boot`).
   - Crea un GameObject vacío llamado `Bootstrap`.
   - Añádele los componentes **`AuthScreen`** y **`LoadingScreen`**.
   - En `AuthScreen`:
     - `Game Scene Name` = nombre de tu escena de juego (ej. `SampleScene`).
     - `Crear Partida Al Ingresar` = ✔ (recomendado: habilita el registro en el
       backend para HDU-2 / HDU-8).
   - `AuthScreen` encuentra solo a `LoadingScreen` (están en el mismo objeto).

2. **Build Settings** (¡importante!)
   - File → Build Settings → **Add Open Scenes**.
   - Agrega **`Boot`** (primera, índice 0) y tu escena de juego (`SampleScene`).
   - `LoadSceneAsync` sólo puede cargar escenas que estén en esta lista.

3. **Backend** (opcional)
   - En el `ApiManager` (se crea solo si no existe) ajusta `baseUrl` a tu servidor
     Django, ej. `http://127.0.0.1:8000/api`.
   - Si el backend no está corriendo, el login mostrará un error claro; el resto del
     juego funciona igual (las funciones de chat tienen registro "best-effort").

## Cómo funciona

1. Al abrir `Boot`, `AuthScreen` muestra el formulario (Login por defecto; botón para
   cambiar a Registro).
2. Al enviar:
   - Valida que haya usuario y contraseña.
   - Llama a `ApiManager.Login` o `ApiManager.Registro`.
   - Si falla, muestra un mensaje amigable; si funciona, (opcional) crea una partida.
3. Luego `LoadingScreen` se muestra y carga la escena del juego de forma asíncrona;
   la barra refleja el progreso real (0–100 %) con un tiempo mínimo para que no
   parpadee.
4. El `ApiManager` usa `DontDestroyOnLoad`: el **token** y la **partida** persisten en
   la escena del juego (necesario para HDU-2/HDU-8).

## Personalización
- **Velocidad/tiempo de carga:** `minDisplayTime` y `fillLerpSpeed` en `LoadingScreen`.
- **Consejos durante la carga:** lista `tips` en `LoadingScreen`.
- **Diseño propio:** crea tu Canvas/elementos y arrástralos a las referencias de cada
  script (panel, campos, barra `fillRect` o `progressBar`/`fillImage`, etc.). Si las
  asignas, no se generan automáticamente.

## Notas técnicas
- Usa UI legacy (`InputField`, `Button`, `Text`) — siempre disponible con `com.unity.ugui`.
- `UiBootstrap.EnsureEventSystem()` crea un EventSystem con `InputSystemUIInputModule`
  (porque el proyecto usa el nuevo Input System). Las pantallas in-game de diálogo y
  chat también lo invocan, por lo que sus botones funcionan sin montar nada extra.
