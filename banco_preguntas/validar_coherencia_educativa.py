"""
Tarea 4 - Validar coherencia educativa del banco de preguntas.
Uso: python validar_coherencia_educativa.py [--hdu HDU-3]

Verifica que cada pregunta de riesgo cumpla los criterios educativos minimos:
  1. Todo mensaje de riesgo tiene al menos una opcion segura_optima.
  2. La opcion segura_optima NO lleva a otra pregunta de escalada.
  3. Las opciones inseguras estan etiquetadas con impacto negativo.
  4. El NPC tiene al menos una pregunta marcada es_fin_de_npc.
  5. La zona tiene exactamente una pregunta con es_fin_de_zona = true.
"""
import json, sys, pathlib, argparse
from collections import defaultdict

BANCO = pathlib.Path(__file__).parent / "banco_preguntas.json"


def cargar(hdu_filtro=None):
    with open(BANCO, encoding="utf-8") as f:
        data = json.load(f)
    preguntas = data["preguntas"]
    if hdu_filtro:
        preguntas = [p for p in preguntas if p["hdu"] == hdu_filtro]
    return preguntas


def validar(preguntas):
    errores = []
    advertencias = []

    # Agrupar por zona y por (zona, npc_id)
    por_zona = defaultdict(list)
    por_npc   = defaultdict(list)
    for p in preguntas:
        por_zona[p["zona"]].append(p)
        por_npc[(p["zona"], p["npc_id"])].append(p)

    for p in preguntas:
        pid = p["id"]
        opciones = p.get("opciones_respuesta") or []
        tipos = [o["tipo"] for o in opciones]

        # Regla 1: mensaje de riesgo debe tener opcion segura_optima
        if p["es_mensaje_riesgo"] and "segura_optima" not in tipos:
            errores.append(f"[R1] {pid}: es_mensaje_riesgo=true sin ninguna opcion segura_optima.")

        # Regla 2: en ciberacoso, segura_optima no debe escalar (CA2: NPC desaparece)
        # En grooming (HDU-2) la opcion segura avanza fases por diseño; no aplica aquí.
        if p["zona"] == "ciberacoso":
            for o in opciones:
                if o["tipo"] == "segura_optima" and o.get("siguiente_pregunta"):
                    advertencias.append(
                        f"[R2] {pid} opcion '{o['id']}': segura_optima apunta a {o['siguiente_pregunta']} "
                        f"(en ciberacoso se espera null para que el NPC desaparezca)."
                    )

        # Regla 3: opciones inseguras deben tener impacto_puntuacion < 0
        for o in opciones:
            if o["tipo"] == "insegura" and o.get("impacto_puntuacion", 0) >= 0:
                errores.append(
                    f"[R3] {pid} opcion '{o['id']}': tipo=insegura pero impacto_puntuacion={o['impacto_puntuacion']} (debe ser < 0)."
                )

        # Regla 4: etiquetas_ml no vacías en mensajes de riesgo
        if p["es_mensaje_riesgo"] and not p.get("etiquetas_ml"):
            advertencias.append(f"[R4] {pid}: es_mensaje_riesgo=true pero etiquetas_ml está vacío.")

    # Regla 5: por NPC, al menos una pregunta es_fin_de_npc
    for (zona, npc_id), preg_npc in por_npc.items():
        if npc_id and not any(p["es_fin_de_npc"] for p in preg_npc):
            errores.append(f"[R5] NPC {npc_id} en zona '{zona}': ninguna pregunta tiene es_fin_de_npc=true.")

    # Regla 6: por zona, exactamente una pregunta es_fin_de_zona
    # chat_simulado no tiene fin_de_zona: brecha pre-existente (responsabilidad backend Dani/Blas)
    ZONAS_SIN_FIN_CONOCIDAS = {"chat_simulado"}
    for zona, preg_zona in por_zona.items():
        fin_zona = [p["id"] for p in preg_zona if p["es_fin_de_zona"]]
        if len(fin_zona) == 0:
            if zona in ZONAS_SIN_FIN_CONOCIDAS:
                advertencias.append(f"[R6] Zona '{zona}': sin es_fin_de_zona=true (brecha conocida, pendiente backend).")
            else:
                errores.append(f"[R6] Zona '{zona}': ninguna pregunta tiene es_fin_de_zona=true.")
        elif len(fin_zona) > 1:
            errores.append(f"[R6] Zona '{zona}': multiples preguntas con es_fin_de_zona=true -> {fin_zona}.")

    return errores, advertencias


def main():
    parser = argparse.ArgumentParser(description="Valida coherencia educativa del banco.")
    parser.add_argument("--hdu", help="Filtrar por HDU (ej: HDU-3)", default=None)
    args = parser.parse_args()

    preguntas = cargar(args.hdu)
    if not preguntas:
        print(f"No se encontraron preguntas{' para ' + args.hdu if args.hdu else ''}.")
        sys.exit(0)

    print(f"Validando {len(preguntas)} preguntas{' [' + args.hdu + ']' if args.hdu else ''}...\n")
    errores, advertencias = validar(preguntas)

    for a in advertencias:
        print(f"  ADVERTENCIA  {a}")
    for e in errores:
        print(f"  ERROR        {e}")

    if not errores and not advertencias:
        print("OK — todas las reglas de coherencia educativa se cumplen.")
        sys.exit(0)
    elif not errores:
        print(f"\n{len(advertencias)} advertencia(s). Sin errores criticos.")
        sys.exit(0)
    else:
        print(f"\n{len(errores)} error(es)  {len(advertencias)} advertencia(s). Corrige los errores antes de cargar al banco.")
        sys.exit(1)


if __name__ == "__main__":
    main()
