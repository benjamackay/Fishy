# Arranque del juego: cuenta del adulto + perfil de menor + Pantalla de carga

Flujo previo al juego, en **dos pasos** (modelo de control parental):

1. **Cuenta del adulto responsable** — inicia sesión o crea una cuenta. Es la única
   que tiene login.
2. **Perfil de menor** — el adulto elige cuál de sus hijos va a jugar (o crea el
   primero). Cada perfil conserva su propio avance: se retoma su última partida y
   solo se crea una nueva si ese menor nunca ha jugado.

Recién ahí se carga el mundo con una barra de progreso real.

## Scripts

| Script | Rol |
|--------|-----|
| `AuthScreen.cs` | Pantalla de acceso en dos pasos (cuenta + perfil), conectada al `ApiManager`. |
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
     - `Crear Partida Al Ingresar` = ✔ (recomendado: activa el paso de elegir
       perfil de menor y prepara su partida para HDU-2 / HDU-8). Si lo desactivas,
       se entra al juego **sin partida** y nada se registra en el backend.
   - `AuthScreen` encuentra solo a `LoadingScreen` (están en el mismo objeto).

2. **Build Settings** (¡importante!)
   - File → Build Settings → **Add Open Scenes**.
   - Agrega **`Boot`** (primera, índice 0) y tu escena de juego (`SampleScene`).
   - `LoadSceneAsync` sólo puede cargar escenas que estén en esta lista.

3. **Backend** (opcional)
   - En el `ApiManager` (se crea solo si no existe) ajusta `baseUrl` a tu servidor
     Django, ej. `http://127.0.0.1:8000/api`.
   - Si el backend no está corriendo, `CheckHealth` activa solo el **modo local**
     (PlayerPrefs) y el badge de la esquina lo indica. Cuentas, perfiles de menor y
     partidas se simulan en disco, así que el flujo de dos pasos —incluido retomar
     el avance de cada menor— se puede demostrar sin servidor.

## Cómo funciona

1. Al abrir `Boot`, `AuthScreen` muestra el formulario de la cuenta del adulto
   (Login por defecto; botón para cambiar a Registro).
2. Al enviar:
   - En **Entrar**: valida nombre y contraseña, y llama a `ApiManager.Login`.
   - En **Crear cuenta**: pide además un **email** (obligatorio y único; el backend
     rechaza el registro sin él) y llama a `ApiManager.Registro`.
   - Si falla, muestra un mensaje amigable.
3. Autenticado, aparece el panel **"¿Quién va a jugar?"**: lista los perfiles de menor
   de esa cuenta y permite agregar uno nuevo (nombre + edad opcional). Al tocar un
   perfil se llama a `ApiManager.ContinuarOCrearPartida`, que **retoma** la última
   partida de ese menor o le crea una si nunca jugó.
4. Luego `LoadingScreen` se muestra y carga la escena del juego de forma asíncrona;
   la barra refleja el progreso real (0–100 %) con un tiempo mínimo para que no
   parpadee.
5. El `ApiManager` usa `DontDestroyOnLoad`: el **token**, el **perfil de menor activo**
   y la **partida** persisten en la escena del juego (necesario para HDU-2/HDU-8).

> Nota: antes existía un "login inteligente" que registraba solo si el login fallaba.
> Se eliminó al pasar a control parental: el registro exige email, y adivinarlo no es
> posible. Ahora los dos modos están separados.

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
