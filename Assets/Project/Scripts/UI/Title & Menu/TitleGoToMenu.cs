using Salem.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleGoToMenu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.anyKeyDown)
        {
            SceneLoader.Instance.LoadScene("Main_Menu");
        }        
    }
}
