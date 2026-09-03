using UnityEngine;
using UnityEngine.UI;

public class TabsController : MonoBehaviour
{
    public UnityEngine.UI.Image[] tabImages;
    public GameObject[] pages;

    private void Awake()
    {
        AsegurarControladoresDePaginas();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ActivateTab(0);
    }

    /// <summary>
    /// Las páginas del Tab ya están asignadas en la escena. Conecta sus
    /// controladores automáticamente para que no dependan de que el componente
    /// se haya agregado a mano y guardado en el archivo de escena.
    /// </summary>
    private void AsegurarControladoresDePaginas()
    {
        if (pages == null) return;

        foreach (GameObject page in pages)
        {
            if (page == null) continue;

            if (page.name == "QuestPage" && page.GetComponent<QuestPageUI>() == null)
                page.AddComponent<QuestPageUI>();
        }
    }

    public void ActivateTab(int tabNo)
    {
        for(int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(false);
            tabImages[i].color = Color.grey;
        }
        pages[tabNo].SetActive(true);
        tabImages[tabNo].color = Color.white;
    }
    
}
