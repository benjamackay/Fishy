using UnityEngine;
using UnityEngine.SceneManagement;

public class Segundomenu : MonoBehaviour
{
    public void Jugar()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex+1);
    }

    public void Salir()
    {
        Debug.Log("SALIR");
        Application.Quit();
    }
}
