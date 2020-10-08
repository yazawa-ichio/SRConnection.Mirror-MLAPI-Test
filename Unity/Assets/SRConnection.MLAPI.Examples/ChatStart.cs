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
			if (!NetworkingManager.Singleton.IsClient && !NetworkingManager.Singleton.IsHost)
			{
				if (GUILayout.Button("StartHost"))
				{
					NetworkingManager.Singleton.StartHost();
					GameObject.Instantiate(m_Prefab).GetComponent<NetworkedObject>().Spawn();
				}
				if (GUILayout.Button("StartClient"))
				{
					NetworkingManager.Singleton.StartClient();
				}
			}
		}
	}
}