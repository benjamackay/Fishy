using System;
using Fishy.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TarjetaPerfilUI : MonoBehaviour
{
    [SerializeField] private Image fondo;
    [SerializeField] private TMP_Text nombre;

    public UsuarioJugadorDto Perfil { get; private set; }

    private Button boton;
    private Action<TarjetaPerfilUI> alSeleccionar;

    private void Awake()
    {
        boton = GetComponent<Button>();
        boton.onClick.AddListener(NotificarClic);
    }

    public void Configurar(
        UsuarioJugadorDto perfil,
        Action<TarjetaPerfilUI> seleccion)
    {
        Perfil = perfil;
        alSeleccionar = seleccion;

        nombre.text = perfil.nombre;
        MostrarSeleccion(false);
    }

    private void NotificarClic()
    {
        if (Perfil != null)
            alSeleccionar?.Invoke(this);
    }

    public void MostrarSeleccion(bool seleccionada)
    {
        if (fondo == null || nombre == null)
            return;

        fondo.color = seleccionada
            ? ColorHex("#2D211F")
            : ColorHex("#503124");

        nombre.color = seleccionada
            ? ColorHex("#FFB941")
            : ColorHex("#C5876C");
    }

    private static Color ColorHex(string hexadecimal)
    {
        ColorUtility.TryParseHtmlString(hexadecimal, out Color color);
        return color;
    }

    private void OnDestroy()
    {
        if (boton != null)
            boton.onClick.RemoveListener(NotificarClic);
    }
}