from datetime import datetime, timezone

from django.test import TestCase, override_settings
from rest_framework.authtoken.models import Token
from rest_framework.test import APIClient

from api.models import AdultoResponsable, Chat, GrupoTutor, Mensaje, MiembroGrupo, NPC, OpcionBanco, Partida, PreguntaBanco, UsuarioJugador, ZonaProgreso


@override_settings(PASSWORD_HASHERS=['django.contrib.auth.hashers.MD5PasswordHasher'])
class CuentaPortalTests(TestCase):
    def setUp(self):
        self.adulto = AdultoResponsable.objects.create_user('familia', 'familia@example.com', 'ClaveAnterior-9482')
        self.client = APIClient()
        self.client.force_authenticate(self.adulto)

    def test_editar_perfil_y_borrar_nacimiento(self):
        r = self.client.patch('/api/auth/perfil/', {'apellido': 'Rojas', 'fecha_nacimiento': '1990-05-17'}, format='json')
        self.assertEqual(r.status_code, 200)
        self.assertEqual(r.data['apellido'], 'Rojas')
        self.assertIsNotNone(r.data['edad'])
        r = self.client.patch('/api/auth/perfil/', {'fecha_nacimiento': None}, format='json')
        self.assertIsNone(r.data['edad'])
        self.assertNotIn('password', r.data)

    def test_datos_invalidos_no_cambian_cuenta(self):
        AdultoResponsable.objects.create_user('otra', 'otra@example.com', 'clave')
        for datos in [{'rol': 'admin', 'apellido': 'Cambiar'}, {'email': 'OTRA@example.com', 'apellido': 'Cambiar'}, {'fecha_nacimiento': '2999-01-01'}, {'nombre': ''}]:
            self.assertEqual(self.client.patch('/api/auth/perfil/', datos, format='json').status_code, 400)
        self.adulto.refresh_from_db()
        self.assertEqual(self.adulto.apellido, '')
        self.assertEqual(self.adulto.rol, 'padre')

    def test_cambio_clave_verifica_actual_y_revoca_token(self):
        token = Token.objects.create(user=self.adulto)
        datos = {'password_actual': 'incorrecta', 'password_nueva': 'NuevaClave-6824', 'password_confirmacion': 'NuevaClave-6824'}
        self.assertEqual(self.client.post('/api/auth/cambiar-contrasena/', datos).status_code, 400)
        self.assertTrue(Token.objects.filter(pk=token.pk).exists())
        datos['password_actual'] = 'ClaveAnterior-9482'
        self.assertEqual(self.client.post('/api/auth/cambiar-contrasena/', datos).status_code, 200)
        self.adulto.refresh_from_db()
        self.assertTrue(self.adulto.check_password('NuevaClave-6824'))
        self.assertFalse(Token.objects.filter(pk=token.pk).exists())

    def test_requiere_sesion(self):
        self.client.force_authenticate(None)
        self.assertEqual(self.client.patch('/api/auth/perfil/', {}).status_code, 401)
        self.assertEqual(self.client.post('/api/auth/cambiar-contrasena/', {}).status_code, 401)


@override_settings(PASSWORD_HASHERS=['django.contrib.auth.hashers.MD5PasswordHasher'])
class PeriodoPortalTests(TestCase):
    def setUp(self):
        self.padre = AdultoResponsable.objects.create_user('familia', 'familia@example.com', 'clave')
        self.profesor = AdultoResponsable.objects.create_user('profe', 'profe@example.com', 'clave', rol='profesor')
        self.grupo = GrupoTutor.objects.create(nombre='Curso', tutor=self.profesor)
        self.ninos = [UsuarioJugador.objects.create(adulto=self.padre, nombre=f'Nino{i}') for i in range(3)]
        pregunta = PreguntaBanco.objects.create(pregunta_id='P', zona='desconocidos', mensaje_npc='privado')
        OpcionBanco.objects.create(pregunta=pregunta, opcion_id='O', tipo='segura_optima', texto='privado', consecuencia_narrativa='privado')
        for i, nino in enumerate(self.ninos):
            MiembroGrupo.objects.create(grupo=self.grupo, jugador=nino)
            partida = Partida.objects.create(usuario_jugador=nino)
            npc = NPC.objects.create(partida=partida, nombre='NPC', area='zona', tipo='neutral')
            chat = Chat.objects.create(partida=partida, npc=npc)
            for hora in ([2, 3] if i == 2 else [2]):
                mensaje = Mensaje.objects.create(chat=chat, tipo='chain', respuesta='privado', opcion_banco_id='O')
                # 02:59 UTC = 23:59 del 5 en Chile; 03:59 UTC ya es dia 6.
                Mensaje.objects.filter(pk=mensaje.pk).update(timestamp=datetime(2026, 10, 6, hora, 59, tzinfo=timezone.utc))
            ZonaProgreso.objects.create(partida=partida, zona='desconocidos', fecha_completada=datetime(2026, 10, 7, tzinfo=timezone.utc))
        self.client = APIClient()
        self.client.force_authenticate(self.profesor)
        self.url = f'/api/grupos/{self.grupo.pk}/reporte/'

    def test_limites_locales_y_completitud_historica(self):
        r = self.client.get(self.url, {'inicio': '2026-10-05', 'fin': '2026-10-05'})
        self.assertEqual(r.status_code, 200)
        self.assertEqual(r.data['tematicas'][0]['metricas'], {'decisiones_seguras': 3, 'decisiones_evaluadas': 3, 'completada': False})
        self.assertEqual(r.data['periodo']['zona_horaria'], 'America/Santiago')
        self.assertNotIn('privado', str(r.data))

    def test_minimo_se_recalcula_en_cada_rango(self):
        r = self.client.get(self.url, {'inicio': '2026-10-06', 'fin': '2026-10-06'})
        self.assertEqual(r.data['participantes_con_resultados'], 1)
        self.assertEqual(r.data['tematicas'][0], {'tematica': 'desconocidos', 'metricas': None, 'motivo': 'muestra_insuficiente'})

    def test_rangos_invalidos_y_acumulado(self):
        for datos in [{'inicio': '2026-10-05'}, {'inicio': '2026-02-30', 'fin': '2026-10-05'}, {'inicio': '2026-10-06', 'fin': '2026-10-05'}]:
            self.assertEqual(self.client.get(self.url, datos).status_code, 400)
        r = self.client.get(self.url)
        self.assertIsNone(r.data['periodo'])
        self.assertEqual(r.data['tematicas'][0]['metricas']['decisiones_evaluadas'], 4)
