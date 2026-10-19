Fondos candidatos para el chat del Modo Detective.

Deja aquí las imágenes que quieras probar. Al abrir el modo se toma una al azar
y con la tecla F (DetectiveUITheme.Fondo.TeclaSiguiente) se salta a otra, sin
salir del juego. Abajo a la derecha aparece el nombre de la que estás viendo,
para poder anotar cuál gustó.

Las imágenes se importan solas como Sprite entero: de eso se encarga
Assets/Editor/FishyFondosImporter.cs. No hace falta tocar el Inspector.


Cuando esté elegido el fondo, en DetectiveUITheme.Fondo:
  Rotar = false
  FijoPorNombre = "nombre-del-archivo-sin-extension"

Y para ajustar cuánto se ve la ilustración detrás de los mensajes, baja o sube
el alfa de Fondo.Tinte (por defecto 0.30).
