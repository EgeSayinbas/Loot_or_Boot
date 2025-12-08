using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;   

public class MainMenuNetworkUI : MonoBehaviour
{
    public string lobbySceneName = "Lobby";

    public void OnClickHost()
    {
        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log("Host started.");

            // NGO SceneManager ile Lobby'ye geç
            NetworkManager.Singleton.SceneManager.LoadScene(
                lobbySceneName,
                LoadSceneMode.Single
            );
        }
        else
        {
            Debug.LogError("Failed to start host.");
        }
    }

    public void OnClickClient()
    {
        if (NetworkManager.Singleton.StartClient())
        {
            Debug.Log("Client started. Waiting for host to change scene...");
            // Host Lobby'yi yüklediðinde client'lar otomatik çekilecek
        }
        else
        {
            Debug.LogError("Failed to start client.");
        }
    }
}
