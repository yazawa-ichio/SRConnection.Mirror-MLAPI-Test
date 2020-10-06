using Mirror;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace SRConnection.Mirror
{

	public class SRConnectionTransport : Transport
	{
		[SerializeField]
		int m_Port = 7898;
		[SerializeField, Header("ƒAƒvƒŠ‚É‚Í”é–§Œ®‚ðŠÜ‚ß‚È‚¢‚Å‚­‚¾‚³‚¢")]
		string m_RsaXmlPath = null;

		Action<Stream, ArraySegment<byte>> m_Write;
		Action<Stream, ArraySegment<byte>> WriteMethod => m_Write ?? (m_Write = WriteMethodImpl);

		Connection m_Server;
		ClientConnection m_ClientConn;
		Task<ClientConnection> m_ClientTask;

		void WriteMethodImpl(Stream writer, ArraySegment<byte> segment)
		{
			writer.Write(segment.Array, segment.Offset, segment.Count);
		}

		public void LateUpdate()
		{
			while (ProcessClientMessage()) { }
			while (ProcessServerMessage()) { }
		}

		private bool ProcessServerMessage()
		{
			if (m_Server == null) return false;

			if (m_Server.Disposed)
			{
				m_Server = null;
				return false;
			}

			if (m_Server.TryReadMessage(out var msg))
			{
				OnServerDataReceived?.Invoke(Mathf.Abs(msg.Peer.ConnectionId), new ArraySegment<byte>(msg.ToArray()), msg.Channel);
				return true;
			}

			return false;
		}

		private bool ProcessClientMessage()
		{
			if (m_ClientConn == null) return false;

			if (m_ClientConn.Disposed || !m_ClientConn.Server.IsConnection)
			{
				DisposeClient();
				OnClientDisconnected?.Invoke();
				return false;
			}

			bool receive = false;
			Message msg = default;
			try
			{
				receive = m_ClientConn.TryReadMessage(out msg);
			}
			catch (Exception ex)
			{
				OnClientError?.Invoke(ex);
			}

			if (receive)
			{
				OnClientDataReceived?.Invoke(new ArraySegment<byte>(msg.ToArray()), msg.Channel);
			}
			return receive;
		}

		public override bool Available()
		{
			return Application.platform != RuntimePlatform.WebGLPlayer;
		}

		void DisposeClient()
		{
			if (m_ClientConn != null)
			{
				m_ClientConn.Dispose();
				m_ClientConn = null;
			}
			m_ClientTask = null;
		}

		public override async void ClientConnect(string address)
		{
			DisposeClient();

			IPAddress[] addresses = Dns.GetHostAddresses(address);
			IPAddress ip = null;
			foreach (var tmp in addresses)
			{
				if (tmp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
				{
					ip = tmp;
				}
			}

			ServerConnectSettings setting;
			if (!string.IsNullOrWhiteSpace(m_RsaXmlPath))
			{
				var xml = Resources.Load<TextAsset>(m_RsaXmlPath);
				setting = ServerConnectSettings.FromXML(xml.text, new IPEndPoint(ip, m_Port));
			}
			else
			{
				setting = new ServerConnectSettings
				{
					EndPoint = new IPEndPoint(ip, m_Port),
				};
			}
			var task = m_ClientTask = Connection.ConnectToServer(setting);
			try
			{
				var conn = await task;
				if (task == m_ClientTask)
				{
					m_ClientConn = conn;
					OnClientConnected?.Invoke();
				}
				else
				{
					conn.Dispose();
				}
			}
			catch (Exception)
			{
				if (task == m_ClientTask)
				{
					OnClientDisconnected?.Invoke();
				}
			}
		}

		public override bool ClientConnected()
		{
			return m_ClientConn != null && !m_ClientConn.Disposed;
		}

		public override void ClientDisconnect()
		{
			m_ClientConn?.SendDisconnect();
			DisposeClient();
		}

		public override bool ClientSend(int channelId, ArraySegment<byte> segment)
		{
			if (m_ClientConn != null)
			{
				m_ClientConn.Server.Send(WriteMethod, in segment);
			}
			return false;
		}

		public override int GetMaxPacketSize(int channelId = 0)
		{
			return Channel.Fragment.Size * 16;
		}

		public override bool ServerActive()
		{
			return m_Server != null && !m_Server.Disposed;
		}

		public override bool ServerDisconnect(int connectionId)
		{
			if (m_Server.TryGetPeer(connectionId, out var peer))
			{
				return peer.Disconnect();
			}
			if (m_Server.TryGetPeer(-connectionId, out peer))
			{
				return peer.Disconnect();
			}
			return false;
		}

		public override string ServerGetClientAddress(int connectionId)
		{
			return "unknown";
		}

		public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment)
		{
			if (!ServerActive())
			{
				return false;
			}
			foreach (var id in connectionIds)
			{
				if (m_Server.TryGetPeer(id, out var peer))
				{
					peer.Send(WriteMethod, segment);
				}
				if (m_Server.TryGetPeer(-id, out peer))
				{
					peer.Send(WriteMethod, segment);
				}
			}
			return true;
		}

		public override void ServerStart()
		{
			m_Server?.Dispose();

			ServerConfig config;
			if (!string.IsNullOrWhiteSpace(m_RsaXmlPath))
			{
				var xml = Resources.Load<TextAsset>(m_RsaXmlPath);
				config = ServerConfig.FromXML(xml.text, m_Port);
			}
			else
			{
				config = ServerConfig.Create(m_Port);
			}
			m_Server = Connection.StartServer(config);
			m_Server.OnPeerEvent += (e) =>
			{
				switch (e.EventType)
				{
					case PeerEvent.Type.Add:
						OnServerConnected?.Invoke(Mathf.Abs(e.Peer.ConnectionId));
						break;
					case PeerEvent.Type.Remove:
						OnServerDisconnected?.Invoke(Mathf.Abs(e.Peer.ConnectionId));
						break;
				}
			};
		}

		public override void ServerStop()
		{
			m_Server?.BroadcastDisconnect();
			m_Server?.Dispose();
			m_Server = null;
		}

		public override Uri ServerUri()
		{
			UriBuilder builder = new UriBuilder();
			builder.Scheme = "srnet";
			builder.Host = Dns.GetHostName();
			builder.Port = m_Port;
			return builder.Uri;
		}

		public override void Shutdown()
		{
			m_Server?.BroadcastDisconnect();
			m_Server?.Dispose();
			m_Server = null;
			DisposeClient();
		}
	}

}
