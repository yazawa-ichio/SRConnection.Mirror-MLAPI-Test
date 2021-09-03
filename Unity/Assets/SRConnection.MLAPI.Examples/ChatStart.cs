using MLAPI;
using UnityEngine;

namespace SRConnection.Examples
{
	public class ChatStart : MonoBehaviour
	{
		[SerializeField]
		GameObject m_Prefab = null;

		private void OnGUI()
		{
			if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
			{
				if (GUILayout.Button("StartHost"))
				{
					NetworkManager.Singleton.StartHost();
					GameObject.Instantiate(m_Prefab).GetComponent<NetworkObject>().Spawn();
				}
				if (GUILayout.Button("StartClient"))
				{
					NetworkManager.Singleton.StartClient();
				}
			}
		}
	}
}
