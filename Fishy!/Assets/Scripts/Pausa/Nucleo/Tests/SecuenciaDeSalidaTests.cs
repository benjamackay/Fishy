using System;
using System.Collections;
using System.Collections.Generic;
using Fishy.Pausa;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// HDU-17 CA5 — «Salir al menú principal» dispara el guardado automático ANTES de
/// cargar la escena del menú.
///
/// El doble anota el orden en que le hablaron. Es la única forma de afirmar un
/// orden: con el <c>SaveManager</c> de verdad lo que se vería es que la partida
/// quedó guardada, y eso también pasa si se guardó después de pedir la carga —el
/// cambio de escena tarda un frame y casi siempre llega a tiempo—. Casi.
/// </summary>
public class SecuenciaDeSalidaTests
{
    /// <summary>Un destino de salida que apunta cada paso en <see cref="Pasos"/>.</summary>
    private class SalidaFalsa : ISalidaAlMenu
    {
        public readonly List<string> Pasos = new List<string>();

        /// <summary>Frames que tarda el guardado. Con 0 termina en el acto; con más,
        /// la carga tiene tiempo de adelantarse si el orden estuviera mal.</summary>
        public int FramesDeGuardado = 2;

        /// <summary>Hace reventar el guardado, para comprobar que igual se vuelve
        /// al menú.</summary>
        public bool ElGuardadoRevienta;

        /// <summary>Lo que valía el timeScale cuando se pidió cargar la escena.</summary>
        public float TimeScaleAlCargar = float.NaN;

        public IEnumerator GuardarTodo()
        {
            if (ElGuardadoRevienta) throw new InvalidOperationException("no hay conexión");

            Pasos.Add("guardar:empieza");
            for (int i = 0; i < FramesDeGuardado; i++) yield return null;
            Pasos.Add("guardar:termina");
        }

        public void CargarMenu()
        {
            TimeScaleAlCargar = Time.timeScale;
            Pasos.Add("cargar");
        }
    }

    [SetUp]
    public void SetUp() => EstadoDePausa.Restablecer();

    [TearDown]
    public void TearDown()
    {
        EstadoDePausa.Restablecer();
        Time.timeScale = 1f;
    }

    [UnityTest]
    public IEnumerator Salir_GuardaAntesDeCargarElMenu()
    {
        var salida = new SalidaFalsa();

        yield return SecuenciaDeSalida.Ejecutar(salida);

        CollectionAssert.AreEqual(
            new[] { "guardar:empieza", "guardar:termina", "cargar" }, salida.Pasos,
            "CA5: el guardado automático termina antes de que se pida la escena del menú");
    }

    [UnityTest]
    public IEnumerator Salir_EsperaAQueElGuardadoTermine_YNoSoloAQueArranque()
    {
        var salida = new SalidaFalsa { FramesDeGuardado = 5 };

        yield return SecuenciaDeSalida.Ejecutar(salida);

        int termina = salida.Pasos.IndexOf("guardar:termina");
        int carga = salida.Pasos.IndexOf("cargar");

        Assert.AreNotEqual(-1, termina, "el guardado tiene que haber terminado");
        Assert.Less(termina, carga,
            "un guardado que tarda varios frames no puede quedarse a medias al cambiar de escena");
    }

    [UnityTest]
    public IEnumerator Salir_DescongelaElJuegoAntesDeCargar()
    {
        var salida = new SalidaFalsa();

        EstadoDePausa.Pausar();
        Assert.AreEqual(0f, Time.timeScale);

        yield return SecuenciaDeSalida.Ejecutar(salida);

        Assert.AreEqual(1f, salida.TimeScaleAlCargar,
            "el menú heredaría el timeScale en 0 y se quedaría muerto");
        Assert.IsFalse(EstadoDePausa.Pausado);
    }

    [UnityTest]
    public IEnumerator Salir_ConElGuardadoRoto_VuelveAlMenuIgual()
    {
        var salida = new SalidaFalsa { ElGuardadoRevienta = true };

        // `GuardarTodo` es un iterador: su cuerpo no corre al llamarlo, así que la
        // excepción sale en el primer MoveNext y no en la llamada.
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
            @"^\[Pausa\] El guardado de salida falló y se vuelve al menú igual"));

        yield return SecuenciaDeSalida.Ejecutar(salida);

        CollectionAssert.AreEqual(new[] { "cargar" }, salida.Pasos,
            "dejar al niño/a encerrado en la pausa es peor que volver al menú");
    }

    /// <summary>Un destino cuyo guardado ni siquiera llega a ser un iterador: el
    /// otro camino de fallo, el que sí revienta en la llamada.</summary>
    private class SalidaQueNiArranca : ISalidaAlMenu
    {
        public bool CargoElMenu;

        public IEnumerator GuardarTodo() => throw new NullReferenceException("no hay SaveManager");
        public void CargarMenu() => CargoElMenu = true;
    }

    [UnityTest]
    public IEnumerator Salir_ConUnGuardadoQueNiArranca_VuelveAlMenuIgual()
    {
        var salida = new SalidaQueNiArranca();

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
            @"^\[Pausa\] El guardado de salida falló al arrancar"));

        yield return SecuenciaDeSalida.Ejecutar(salida);

        Assert.IsTrue(salida.CargoElMenu);
    }

    [UnityTest]
    public IEnumerator Salir_DosVecesSeguidas_NoCargaElMenuDosVeces()
    {
        var salida = new SalidaFalsa { FramesDeGuardado = 3 };

        // Dos pulsaciones del botón en frames distintos, como pasa de verdad.
        IEnumerator primera = SecuenciaDeSalida.Ejecutar(salida);
        primera.MoveNext();

        yield return SecuenciaDeSalida.Ejecutar(salida);
        while (primera.MoveNext()) yield return primera.Current;

        CollectionAssert.AreEqual(
            new[] { "guardar:empieza", "guardar:termina", "cargar" }, salida.Pasos);
    }

    [UnityTest]
    public IEnumerator Salir_SinDestino_SeQueja_YNoRevienta()
    {
        LogAssert.Expect(LogType.Error, "[Pausa] Se pidió salir al menú sin decir a dónde.");

        yield return SecuenciaDeSalida.Ejecutar(null);
    }
}
