using MLAPI;
using MLAPI.NetworkVariable;
using MLAPI.NetworkVariable.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SRConnection.Examples
{

	public class Chat : NetworkBehaviour
	{
		private NetworkList<string> ChatMessages = new NetworkList<string>(new NetworkVariableSettings()
		{
			ReadPermission = NetworkVariablePermission.Everyone,
			WritePermission = NetworkVariablePermission.Everyone,
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
