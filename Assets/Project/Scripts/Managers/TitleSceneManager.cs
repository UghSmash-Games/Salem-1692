/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Salem.Managers
{
   public class TitleSceneManager : MonoBehaviour
    {
        public void SinglePlayer()
        {
            SceneManager.LoadScene("Sandbox_Testing");
        }

        public void Multiplayer()
        {
            throw new NotImplementedException("Multiplayer flow not implemented yet.");
        }

        public void Options()
        {
            throw new NotImplementedException("Option flow not implemented yet.");
        }

        public void Help()
        {
            throw new NotImplementedException("Help flow not implemented yet.");
        }
        
        public void Quit()
        {
            Debug.Log("Quitting game...");
            Application.Quit();

            // For editor testing only:
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
    } 
}

