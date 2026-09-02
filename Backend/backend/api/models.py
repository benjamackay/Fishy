from django.conf import settings
from django.db import models
from django.contrib.auth.models import AbstractBaseUser, BaseUserManager


# ─────────────────────────────────────────────────────────────────────────────
# CUENTAS  (control parental)
#
# `AdultoResponsable` es la única entidad con login (AUTH_USER_MODEL): el tutor
# se autentica y gestiona uno o más `UsuarioJugador`, que son los perfiles de
# los menores y NO tienen credenciales propias. Toda la data de juego
# (`Partida` y lo que cuelga de ella) pertenece a un `UsuarioJugador`.
# ─────────────────────────────────────────────────────────────────────────────

class AdultoResponsableManager(BaseUserManager):
    def create_user(self, nombre, email, password=None, **extra_fields):
        if not nombre:
            raise ValueError("El adulto responsable debe tener un nombre")
        if not email:
            raise ValueError("El adulto responsable debe tener un email")
        user = self.model(nombre=nombre, email=self.normalize_email(email), **extra_fields)
        user.set_password(password)
        user.save(using=self._db)
        return user

    def create_superuser(self, nombre, email, password=None, **extra_fields):
        user = self.create_user(nombre, email, password, **extra_fields)
        user.is_admin = True
        user.save(using=self._db)
        return user


class AdultoResponsable(AbstractBaseUser):
    """Tutor/adulto responsable que gestiona uno o más perfiles de menores."""
    nombre           = models.CharField(max_length=150, unique=True)
    apellido         = models.CharField(max_length=150, blank=True)
    email            = models.EmailField(unique=True)
    edad             = models.PositiveSmallIntegerField(null=True, blank=True)
    fecha_nacimiento = models.DateField(null=True, blank=True)
    fecha_creacion   = models.DateTimeField(auto_now_add=True)
    is_admin         = models.BooleanField(default=False)

    objects = AdultoResponsableManager()

    USERNAME_FIELD = "nombre"
    REQUIRED_FIELDS = ["email"]   # lo pide `createsuperuser` además del nombre

    def __str__(self):
        return f"{self.nombre} {self.apellido}".strip()

    # Requerido por Django admin
    def has_perm(self, perm, obj=None): return self.is_admin
    def has_module_perms(self, app_label): return self.is_admin

    @property
    def is_staff(self): return self.is_admin

    class Meta:
        verbose_name = "Adulto Responsable"
        verbose_name_plural = "Adultos Responsables"


class UsuarioJugador(models.Model):
    """Perfil del menor que juega, gestionado por un AdultoResponsable."""
    adulto         = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        on_delete=models.CASCADE,
        related_name="jugadores"
    )
    nombre         = models.CharField(max_length=150)
    edad           = models.PositiveSmallIntegerField(null=True, blank=True)
    fecha_creacion = models.DateTimeField(auto_now_add=True)

    def __str__(self):
        return self.nombre

    class Meta:
        verbose_name = "Usuario Jugador"
        verbose_name_plural = "Usuarios Jugadores"
        constraints = [
            models.UniqueConstraint(
                fields=["adulto", "nombre"],
                name="jugador_unico_por_adulto"
            )
        ]


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
    usuario_jugador = models.ForeignKey(
        UsuarioJugador,
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
        return f"Partida #{self.pk} — {self.usuario_jugador.nombre}"

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
    pregunta_banco_id = models.CharField(
        max_length=70,
        blank=True,
        null=True,
        help_text="ID de la pregunta del banco que originó este mensaje (ej: HDU2_NPC01_F2_Q01)."
    )
    opcion_banco_id   = models.CharField(
        max_length=70,
        blank=True,
        null=True,
        db_index=True,
        help_text=(
            "ID de la opción del banco que eligió el jugador (ej: HDU2_NPC01_F2_Q01_R2). "
            "Es la llave que permite acumular riesgo por zona: se resuelve contra "
            "OpcionBanco para obtener impacto_puntuacion y la zona de su pregunta."
        )
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


# ─────────────────────────────────────────────────────────────────────────────
# BANCO DE PREGUNTAS  (contenido estático cargado desde banco_preguntas.json)
# ─────────────────────────────────────────────────────────────────────────────

class PreguntaBanco(models.Model):
    pregunta_id          = models.CharField(max_length=60, unique=True)
    hdu                  = models.CharField(max_length=10)
    zona                 = models.CharField(max_length=50)

    # HDU-2: conversación con NPC
    npc_id               = models.CharField(max_length=30, blank=True)
    npc_nombre           = models.CharField(max_length=60, blank=True)
    npc_avatar           = models.CharField(max_length=60, blank=True)
    fase                 = models.PositiveSmallIntegerField(null=True, blank=True)
    orden_en_fase        = models.PositiveSmallIntegerField(null=True, blank=True)
    narrativa_continuacion = models.CharField(max_length=60, blank=True, null=True)

    # HDU-8: chat simulado
    escenario_id         = models.CharField(max_length=60, blank=True)
    escenario_nombre     = models.CharField(max_length=120, blank=True)
    historial_previo     = models.JSONField(default=list)

    # Comunes
    categoria            = models.CharField(max_length=60)
    nivel_riesgo         = models.PositiveSmallIntegerField(default=0)
    es_mensaje_riesgo    = models.BooleanField(default=False)
    mensaje_npc          = models.TextField()
    etiquetas_ml         = models.JSONField(default=list)
    es_fin_de_npc        = models.BooleanField(default=False, help_text="True en estados FIN_SEGURO / FIN_INSEGURO")
    es_fin_de_zona       = models.BooleanField(default=False, help_text="True en la pregunta ZONA_FIN")

    def __str__(self):
        return self.pregunta_id

    class Meta:
        verbose_name = "Pregunta Banco"
        verbose_name_plural = "Preguntas Banco"
        ordering = ["zona", "npc_id", "fase", "orden_en_fase", "escenario_id"]


class OpcionBanco(models.Model):
    pregunta             = models.ForeignKey(
        PreguntaBanco, on_delete=models.CASCADE, related_name="opciones"
    )
    opcion_id            = models.CharField(max_length=70, unique=True)
    texto                = models.TextField()
    tipo                 = models.CharField(max_length=20)   # insegura | segura_basica | segura_optima
    consecuencia_narrativa = models.TextField()
    impacto_puntuacion   = models.SmallIntegerField(default=0)
    siguiente_pregunta   = models.CharField(max_length=60, blank=True, null=True)
    orden                = models.PositiveSmallIntegerField(default=0)

    def __str__(self):
        return self.opcion_id

    class Meta:
        verbose_name = "Opción Banco"
        verbose_name_plural = "Opciones Banco"
        ordering = ["orden"]


# ─────────────────────────────────────────────────────────────────────────────
# ZONA
# ─────────────────────────────────────────────────────────────────────────────

class Zona(models.Model):
    """Zona del mapa. Base para el cálculo de riesgo por zona (ver HDU riesgo)."""
    nombre         = models.CharField(max_length=100, unique=True)
    descripcion    = models.TextField(blank=True)
    fecha_creacion = models.DateTimeField(auto_now_add=True)

    def __str__(self):
        return self.nombre

    class Meta:
        verbose_name = "Zona"
        verbose_name_plural = "Zonas"


# ─────────────────────────────────────────────────────────────────────────────
# MODO DETECTIVE  (HDU-10, contenido estático cargado desde detective_cases.json)
#
# El jugador observa una conversación pregrabada entre dos NPCs (sin participar)
# y marca los mensajes que considera señales de riesgo. `es_ambiguo` no cuenta
# ni como acierto ni como error (CA5 de HDU-10).
# ─────────────────────────────────────────────────────────────────────────────

class CasoDetective(models.Model):
    caso_id              = models.CharField(max_length=60, unique=True)
    titulo               = models.CharField(max_length=150)
    zona                 = models.CharField(max_length=50)
    etiquetas_ml         = models.JSONField(default=list)

    # Permiso: el jugador le pide a un NPC observar sus mensajes con otro.
    permiso_player_text  = models.TextField()
    permiso_npc_nombre   = models.CharField(max_length=60)
    permiso_npc_response = models.TextField()

    def __str__(self):
        return self.caso_id

    class Meta:
        verbose_name = "Caso Detective"
        verbose_name_plural = "Casos Detective"
        ordering = ["zona", "caso_id"]


class MensajeDetective(models.Model):
    caso            = models.ForeignKey(
        CasoDetective, on_delete=models.CASCADE, related_name="mensajes"
    )
    mensaje_id      = models.CharField(max_length=70, unique=True)
    npc_sender      = models.CharField(max_length=60)
    texto           = models.TextField()
    es_senal_riesgo = models.BooleanField(default=False)
    es_ambiguo      = models.BooleanField(default=False)
    explicacion     = models.TextField(blank=True, null=True)
    nota_ambiguo    = models.TextField(blank=True, null=True)
    orden           = models.PositiveSmallIntegerField(default=0)

    def __str__(self):
        return self.mensaje_id

    class Meta:
        verbose_name = "Mensaje Detective"
        verbose_name_plural = "Mensajes Detective"
        ordering = ["caso", "orden"]


# ─────────────────────────────────────────────────────────────────────────────
# PROGRESO DETECTIVE  (resultado del jugador por partida+caso)
# ─────────────────────────────────────────────────────────────────────────────

class CasoDetectiveProgreso(models.Model):
    partida           = models.ForeignKey(
        Partida, on_delete=models.CASCADE, related_name="casos_detective"
    )
    caso              = models.ForeignKey(
        CasoDetective, on_delete=models.CASCADE, related_name="progresos"
    )
    mensajes_marcados = models.JSONField(
        default=list,
        help_text="mensaje_id de los MensajeDetective que el jugador marcó como riesgo"
    )
    aciertos          = models.PositiveSmallIntegerField(default=0)
    total_riesgo      = models.PositiveSmallIntegerField(default=0)
    porcentaje        = models.FloatField(default=0.0)
    intentos          = models.PositiveSmallIntegerField(default=1)
    fecha_inicio      = models.DateTimeField(auto_now_add=True)
    fecha_termino     = models.DateTimeField(null=True, blank=True)

    def __str__(self):
        return f"{self.caso.caso_id} — {self.partida}"

    class Meta:
        verbose_name = "Progreso Caso Detective"
        verbose_name_plural = "Progresos Casos Detective"
        constraints = [
            models.UniqueConstraint(
                fields=["partida", "caso"], name="progreso_unico_por_partida_caso"
            )
        ]

# ─────────────────────────────────────────────────────────────────────────────
# MISION  (catalogo — HDU-1 / HDU-11 / HDU-12)
#
# Contenido de catalogo: igual para todos los ninos, sin FK a partida. Ojo con
# no confundirlo con NPC, que si cuelga de una partida y es de runtime.
#
# El id lo manda el banco (`mision_desbloquea`), no Unity. Es a proposito: el
# MissionManager de Unity guarda el estado por `desafioId` en PlayerPrefs, y si
# cada lado inventa el suyo terminamos como el Modo Detective (caso_01 vs
# DC_CASO_01). Los DesafioData del editor deben usar este mismo mision_id.
# ─────────────────────────────────────────────────────────────────────────────

class Mision(models.Model):
    class Tipo(models.TextChoices):
        PRINCIPAL   = "principal",   "Principal"
        SECUNDARIA  = "secundaria",  "Secundaria"
        EXPLORACION = "exploracion", "Exploracion"

    mision_id = models.CharField(max_length=60, unique=True)
    nombre    = models.CharField(
        max_length=120, blank=True,
        help_text="`nombre_mision` del banco. La de exploracion no trae nombre."
    )
    tipo      = models.CharField(max_length=20, choices=Tipo.choices, default=Tipo.SECUNDARIA)
    zona      = models.CharField(max_length=50, blank=True)

    def __str__(self):
        return self.mision_id

    class Meta:
        verbose_name = "Mision"
        verbose_name_plural = "Misiones"
        ordering = ["zona", "mision_id"]


# ─────────────────────────────────────────────────────────────────────────────
# DIALOGO NPC  (catalogo — los `dialogos_npc_neutros` del banco)
#
# NPCs neutros que hablan sin arbol de decisiones: no tienen opciones ni riesgo,
# solo lineas y (a veces) una mision. npc_id/npc_nombre van planos, igual que en
# PreguntaBanco: los dos bloques del banco usan namespaces distintos para el
# mismo animal (Flamenco es NPC_03 en preguntas y NPC_FLAMENCO_SEC aca), asi que
# una tabla de NPCs de catalogo lo duplicaria en vez de unificarlo.
# ─────────────────────────────────────────────────────────────────────────────

class DialogoNPC(models.Model):
    dialogo_id   = models.CharField(max_length=60, unique=True)
    hdu          = models.CharField(max_length=10)
    zona         = models.CharField(max_length=50)
    npc_id       = models.CharField(max_length=30, blank=True)
    npc_nombre   = models.CharField(max_length=60, blank=True)
    npc_avatar   = models.CharField(max_length=60, blank=True)
    tipo         = models.CharField(max_length=20, blank=True, help_text="`neutro` en todos los del banco actual")
    trigger      = models.CharField(max_length=30, blank=True, help_text="Como se dispara en el juego, ej. `boton_E`")
    lineas       = models.JSONField(default=list, help_text="Lista de strings, en orden de aparicion")
    pista_mision = models.TextField(blank=True, null=True)
    mision       = models.ForeignKey(
        Mision, on_delete=models.SET_NULL, null=True, blank=True, related_name="dialogos",
        help_text="Mision que este dialogo desbloquea (`mision_desbloquea`)"
    )

    def __str__(self):
        return self.dialogo_id

    class Meta:
        verbose_name = "Dialogo NPC"
        verbose_name_plural = "Dialogos NPC"
        ordering = ["zona", "dialogo_id"]


# ─────────────────────────────────────────────────────────────────────────────
# RECOMPENSA DE ALBUM  (catalogo — HDU-11 / HDU-12)
#
# Las 12 recompensas vienen de dos origenes distintos y excluyentes:
#   - las 6 de misiones secundarias, desde `pista_mision` del dialogo  -> mision
#   - las 6 de misiones principales, desde `consecuencia_narrativa`    -> opcion
# Por eso los dos origenes son opcionales, con un check que obliga a tener
# exactamente uno. El `recompensa_id` se deriva del origen y no del nombre: el
# nombre es texto que Luis puede reescribir, el id del origen es estable.
# ─────────────────────────────────────────────────────────────────────────────

class RecompensaAlbum(models.Model):
    recompensa_id   = models.CharField(max_length=90, unique=True)
    nombre          = models.CharField(max_length=150)
    tip_educativo   = models.TextField(blank=True, help_text="El tip entre parentesis, cuando el banco lo trae")
    mision          = models.ForeignKey(
        Mision, on_delete=models.CASCADE, null=True, blank=True, related_name="recompensas"
    )
    opcion_banco_id = models.CharField(
        max_length=70, blank=True, default="", db_index=True,
        help_text=(
            "`opcion_id` de la OpcionBanco que la entrega. Es texto y no FK a proposito: "
            "cargar_banco borra y recrea las opciones en cada corrida, asi que una FK real "
            "se llevaria en cascada el album ya obtenido por los ninos. Mismo criterio que "
            "Mensaje.opcion_banco_id."
        ),
    )

    def __str__(self):
        return f"{self.recompensa_id} — {self.nombre}"

    class Meta:
        verbose_name = "Recompensa de Album"
        verbose_name_plural = "Recompensas de Album"
        ordering = ["recompensa_id"]
        constraints = [
            models.CheckConstraint(
                condition=(
                    models.Q(mision__isnull=False, opcion_banco_id="")
                    | (models.Q(mision__isnull=True) & ~models.Q(opcion_banco_id=""))
                ),
                name="recompensa_con_un_solo_origen",
            )
        ]


# ─────────────────────────────────────────────────────────────────────────────
# RECOMPENSA OBTENIDA  (progreso — por partida)
#
# Es la tabla que responde "que desbloqueo este nino", que hoy no se puede
# contestar sin parsear texto libre. No hay `MisionProgreso` a proposito: cada
# mision entrega exactamente una recompensa, asi que completarla se deduce de
# aca, y las 6 principales ni siquiera cuelgan de una mision.
# ─────────────────────────────────────────────────────────────────────────────

class RecompensaObtenida(models.Model):
    partida    = models.ForeignKey(
        Partida, on_delete=models.CASCADE, related_name="recompensas_album"
    )
    recompensa = models.ForeignKey(
        RecompensaAlbum, on_delete=models.CASCADE, related_name="obtenciones"
    )
    fecha      = models.DateTimeField(auto_now_add=True)

    def __str__(self):
        return f"{self.recompensa.recompensa_id} — {self.partida}"

    class Meta:
        verbose_name = "Recompensa Obtenida"
        verbose_name_plural = "Recompensas Obtenidas"
        ordering = ["-fecha"]
        constraints = [
            models.UniqueConstraint(
                fields=["partida", "recompensa"], name="recompensa_unica_por_partida"
            )
        ]
