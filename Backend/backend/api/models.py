from django.db import models
from django.contrib.auth.models import AbstractBaseUser, BaseUserManager


# ─────────────────────────────────────────────────────────────────────────────
# USUARIO
# ─────────────────────────────────────────────────────────────────────────────

class UsuarioManager(BaseUserManager):
    def create_user(self, nombre, password=None):
        if not nombre:
            raise ValueError("El usuario debe tener un nombre")
        user = self.model(nombre=nombre)
        user.set_password(password)
        user.save(using=self._db)
        return user

    def create_superuser(self, nombre, password=None):
        user = self.create_user(nombre, password)
        user.is_admin = True
        user.save(using=self._db)
        return user


class Usuario(AbstractBaseUser):
    nombre   = models.CharField(max_length=150, unique=True)
    is_admin = models.BooleanField(default=False)

    objects = UsuarioManager()

    USERNAME_FIELD = "nombre"

    def __str__(self):
        return self.nombre

    # Requerido por Django admin
    def has_perm(self, perm, obj=None): return self.is_admin
    def has_module_perms(self, app_label): return self.is_admin

    @property
    def is_staff(self): return self.is_admin

    class Meta:
        verbose_name = "Usuario"
        verbose_name_plural = "Usuarios"


# ─────────────────────────────────────────────────────────────────────────────
# NIVEL DE RIESGO  (catálogo global, referenciado por Partida)
# ─────────────────────────────────────────────────────────────────────────────

class NivelRiesgo(models.Model):
    nombre      = models.CharField(max_length=100, unique=True)
    descripcion = models.TextField(blank=True)
    puntaje     = models.FloatField(
        default=0.0,
        help_text="Puntaje calculado en base a las respuestas del jugador"
    )

    def __str__(self):
        return self.nombre

    class Meta:
        verbose_name = "Nivel de Riesgo"
        verbose_name_plural = "Niveles de Riesgo"


# ─────────────────────────────────────────────────────────────────────────────
# PARTIDA
# ─────────────────────────────────────────────────────────────────────────────

class Partida(models.Model):
    usuario      = models.ForeignKey(
        Usuario,
        on_delete=models.CASCADE,
        related_name="partidas"
    )
    nivel_riesgo = models.ForeignKey(
        NivelRiesgo,
        on_delete=models.SET_NULL,
        null=True, blank=True,
        related_name="partidas"
    )
    progreso     = models.FloatField(default=0.0, help_text="Porcentaje de progreso (0-100)")
    fecha_inicio = models.DateTimeField(auto_now_add=True)
    fecha_update = models.DateTimeField(auto_now=True)

    def __str__(self):
        return f"Partida #{self.pk} — {self.usuario.nombre}"

    class Meta:
        verbose_name = "Partida"
        verbose_name_plural = "Partidas"


# ─────────────────────────────────────────────────────────────────────────────
# PERSONAJE JUGADOR  (1 a 1 con Partida)
# ─────────────────────────────────────────────────────────────────────────────

class PersonajeJugador(models.Model):
    partida = models.OneToOneField(
        Partida,
        on_delete=models.CASCADE,
        related_name="personaje"
    )
    # Agrega aquí los atributos del personaje (nombre, stats, apariencia, etc.)

    def __str__(self):
        return f"Personaje de {self.partida}"

    class Meta:
        verbose_name = "Personaje Jugador"
        verbose_name_plural = "Personajes Jugador"


# ─────────────────────────────────────────────────────────────────────────────
# NPC
# ─────────────────────────────────────────────────────────────────────────────

class NPC(models.Model):
    class Tipo(models.TextChoices):
        # Ajusta los tipos según tu juego
        ALIADO   = "aliado",   "Aliado"
        NEUTRAL  = "neutral",  "Neutral"
        ENEMIGO  = "enemigo",  "Enemigo"

    partida   = models.ForeignKey(
        Partida,
        on_delete=models.CASCADE,
        related_name="npcs"
    )
    nombre    = models.CharField(max_length=150)
    area      = models.CharField(max_length=150)
    tipo      = models.CharField(max_length=20, choices=Tipo.choices, default=Tipo.NEUTRAL)
    confianza = models.IntegerField(
        default=0,
        help_text="Nivel de confianza del NPC hacia el jugador (ej: 0-100)"
    )

    def __str__(self):
        return f"{self.nombre} ({self.tipo}) — {self.partida}"

    class Meta:
        verbose_name = "NPC"
        verbose_name_plural = "NPCs"


# ─────────────────────────────────────────────────────────────────────────────
# CHAT  (muchos por NPC dentro de una partida)
# ─────────────────────────────────────────────────────────────────────────────

class Chat(models.Model):
    partida           = models.ForeignKey(
        Partida,
        on_delete=models.CASCADE,
        related_name="chats"
    )
    npc               = models.ForeignKey(
        NPC,
        on_delete=models.CASCADE,
        related_name="chats"
    )
    categoria_riesgo  = models.CharField(max_length=100, blank=True)
    fecha_inicio      = models.DateTimeField(auto_now_add=True)
    fecha_termino     = models.DateTimeField(
        null=True, blank=True,
        help_text="Se rellena automáticamente al guardar el mensaje de tipo 'end'"
    )

    def __str__(self):
        return f"Chat #{self.pk} con {self.npc.nombre} — {self.partida}"

    class Meta:
        verbose_name = "Chat"
        verbose_name_plural = "Chats"


# ─────────────────────────────────────────────────────────────────────────────
# MENSAJE
# ─────────────────────────────────────────────────────────────────────────────

class Mensaje(models.Model):
    class Tipo(models.TextChoices):
        START   = "start",   "Start"
        CHAIN   = "chain",   "Chain"
        REQUEST = "request", "Request"
        END     = "end",     "End"

    class CalidadRespuesta(models.TextChoices):
        BUENA   = "buena",   "Buena"
        NEUTRAL = "neutral", "Neutral"
        MALA    = "mala",    "Mala"

    chat              = models.ForeignKey(
        Chat,
        on_delete=models.CASCADE,
        related_name="mensajes"
    )
    tipo              = models.CharField(max_length=10, choices=Tipo.choices)
    respuesta         = models.TextField()
    calidad_respuesta = models.CharField(
        max_length=10,
        choices=CalidadRespuesta.choices,
        blank=True
    )
    timestamp         = models.DateTimeField(auto_now_add=True)

    def save(self, *args, **kwargs):
        super().save(*args, **kwargs)
        # Al guardar un mensaje "end", cierra el chat con su timestamp
        if self.tipo == self.Tipo.END:
            Chat.objects.filter(pk=self.chat_id).update(fecha_termino=self.timestamp)

    def __str__(self):
        return f"Mensaje {self.tipo} — Chat #{self.chat.pk}"

    class Meta:
        verbose_name = "Mensaje"
        verbose_name_plural = "Mensajes"
        ordering = ["timestamp"]


# ─────────────────────────────────────────────────────────────────────────────
# POSIBLES RESPUESTAS  (generadas dinámicamente por Mensaje)
# ─────────────────────────────────────────────────────────────────────────────

class PosibleRespuesta(models.Model):
    mensaje = models.ForeignKey(
        Mensaje,
        on_delete=models.CASCADE,
        related_name="posibles_respuestas"
    )
    texto   = models.TextField()
    orden   = models.PositiveSmallIntegerField(
        default=0,
        help_text="Orden de presentación al jugador"
    )
    calidad_respuesta = models.CharField(
        max_length=10,
        choices=Mensaje.CalidadRespuesta.choices,
        blank=True,
        help_text="Calidad asociada a esta opción de respuesta"
    )

    def __str__(self):
        return f"Opción {self.orden} — Mensaje #{self.mensaje.pk}"

    class Meta:
        verbose_name = "Posible Respuesta"
        verbose_name_plural = "Posibles Respuestas"
        ordering = ["orden"]