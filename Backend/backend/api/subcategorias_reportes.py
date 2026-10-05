"""Taxonomía educativa explícita: clasifica contenido del banco, no personas.

Las etiquetas técnicas del flujo (fin_npc, exito, fallo...) se excluyen.
Una pregunta puede trabajar varias habilidades; sus métricas no son sumables.
"""

SUBCATEGORIAS = {
    "desconocidos": (
        ("confianza", "Contacto y confianza", {"grooming_contacto_inicial", "grooming_confianza", "contacto_amistoso", "manipulacion_confianza", "halago"}),
        ("datos_personales", "Protección de datos personales", {"grooming_datos_personales", "privacidad", "solicitud_datos_personales", "solicitud_foto", "solicitud_ubicacion"}),
        ("secretos", "Secretos y aislamiento", {"grooming_secreto", "solicitud_secreto", "aislamiento"}),
        ("encuentros", "Propuestas de encuentro", {"grooming_encuentro", "propuesta_encuentro", "a_solas"}),
        ("amenazas", "Amenazas y manipulación emocional", {"grooming_amenaza", "amenaza", "extorsion", "manipulacion_emocional"}),
    ),
    "ciberacoso": (
        ("rumores", "Rumores y burlas públicas", {"ciberacoso_rumor", "difusion_rumores", "burla_publica", "verificacion_hechos"}),
        ("exclusion", "Exclusión del grupo", {"ciberacoso_exclusion", "exclusion", "exclusion_grupo", "exclusion_social"}),
        ("acoso_grupal", "Acoso grupal y apoyo a otros", {"ciberacoso_grupal", "acoso_grupal", "acoso_tercero", "silencio_permisivo", "ignorar_sin_reportar"}),
    ),
    "retos_virales": (
        ("presion_social", "Presión social y valentía", {"reto_viral_presion", "presion_social", "presion_multiple", "apelacion_valentia"}),
        ("reputacion", "Amenazas a la reputación", {"amenaza_reputacional", "amenaza_exclusion"}),
        ("incentivos", "Promesas e incentivos falsos", {"incentivo_falso", "popularidad_falsa"}),
        ("riesgos", "Retos peligrosos y exposición en vivo", {"reto_peligroso", "transmision_vivo"}),
    ),
}


def clasificar(tema, categoria, etiquetas):
    # Mensajes narrativos neutros no son evidencia de una habilidad de riesgo.
    if categoria == "neutral":
        return ()
    senales = {categoria}
    if isinstance(etiquetas, list):
        senales.update(e for e in etiquetas if isinstance(e, str))
    return tuple(id_ for id_, _, claves in SUBCATEGORIAS[tema] if senales & claves)
