using Boo.Lang;
using MLAPI;
using MLAPI.NetworkedVar;
using MLAPI.NetworkedVar.Collections;
using UnityEngine;

namespace SRConnection.Examples
{

	public class Chat : NetworkedBehaviour
	{
		private NetworkedList<string> ChatMessages = new NetworkedList<string>(new NetworkedVarSettings()
		{
			ReadPermission = NetworkedVarPermission.Everyone,
			WritePermission = NetworkedVarPermission.Everyone,
			SendTickrate = 5
		}, new List<string>());

		private string textField = "";

		private void OnGUI()
		{
			if (IsClient || IsHost)
			{
				textField = GUILayout.TextField(textField, GUILayout.Width(200));

				if (GUILayout.Button("Send") && !string.IsNullOrWhiteSpace(textField))
				{
					ChatMessages.Add(textField);
					textField = "";
				}

				for (int i = ChatMessages.Count - 1; i >= 0; i--)
				{
					GUILayout.Label(ChatMessages[i]);
				}
			}
		}
	}
}