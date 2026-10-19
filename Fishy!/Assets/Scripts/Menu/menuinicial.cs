using UnityEngine;
using UnityEngine.SceneManagement;
public class menuinicial : MonoBehaviour
{
    public void Ingresar()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex+1);
    }

    public void Salir()
    {
        Debug.Log("SALIR");
        Application.Quit();
    }
}
