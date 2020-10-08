using MLAPI.Transports;
using MLAPI.Transports.Tasks;
using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace SRConnection.MLAPI
{
	public class SRConnectionMLAPITransport : Transport
	{
		[SerializeField]
		int m_Port = 7898;
		[SerializeField]
		string m_ConnectAddress = "127.0.0.1";
		[SerializeField, Header("アプリには秘密鍵を含めないでください")]
		string m_RsaXmlPath = null;

		Connection m_Server;
		ClientConnection m_ClientConn;
		Queue<PeerEvent> m_PeerEvent = new Queue<PeerEvent>();
		Queue<Message> m_DelayMessage = new Queue<Message>();
		Dictionary<string, short> m_ChannelId = new Dictionary<string, short>();
		Dictionary<short, string> m_ChannelName = new Dictionary<short, string>();


		ulong GetId(int id)
		{
			if (m_ClientConn != null && m_ClientConn.Server.ConnectionId == id)
			{
				return 0;
			}
			return (ulong)(int.MaxValue + id);
		}

		int GetId(ulong id)
		{
			if (id == 0 && m_ClientConn != null)
			{
				return m_ClientConn.Server.ConnectionId;
			}
			return (int)((long)id - int.MaxValue);
		}

		bool TryGetPeer(ulong clientId, out Peer peer)
		{
			var id = GetId(clientId);
			if (m_Server != null)
			{
				return m_Server.TryGetPeer(id, out peer);
			}
			if (m_ClientConn != null)
			{
				return m_ClientConn.TryGetPeer(id, out peer);
			}
			peer = null;
			return false;
		}

		public override ulong ServerClientId => GetId(m_ClientConn?.SelfId ?? 0);

		public override void DisconnectLocalClient()
		{
			m_ClientConn?.BroadcastDisconnect();
		}

		public override void DisconnectRemoteClient(ulong clientId)
		{
			if (TryGetPeer(clientId, out var peer))
			{
				peer.Disconnect();
			}
		}

		public override ulong GetCurrentRtt(ulong clientId)
		{
			return 0;
		}

		public override void Init()
		{
			m_ChannelId.Clear();
			m_ChannelName.Clear();
			m_ClientConn?.Dispose();
			m_ClientConn = null;
			m_Server?.Dispose();
			m_Server = null;
		}

		public override NetEventType PollEvent(out ulong clientId, out string channelName, out ArraySegment<byte> payload, out float receiveTime)
		{
			{
				if (m_PeerEvent.Count > 0)
				{
					var e = m_PeerEvent.Dequeue();
					clientId = GetId(e.Peer.ConnectionId);
					channelName = null;
					payload = default;
					receiveTime = Time.realtimeSinceStartup;
					return e.EventType == PeerEvent.Type.Add ? NetEventType.Connect : NetEventType.Disconnect;
				}
			}
			{
				if (m_DelayMessage.Count > 0)
				{
					var message = m_DelayMessage.Dequeue();
					clientId = GetId(message.Peer.ConnectionId);
					m_ChannelName.TryGetValue(message.ChannelId, out channelName);
					payload = new ArraySegment<byte>(message.ToArray());
					receiveTime = Time.realtimeSinceStartup;
					return NetEventType.Data;
				}
			}
			{
				var conn = m_Server ?? m_ClientConn;
				if (conn != null && conn.TryReadMessage(out var message))
				{
					if (m_PeerEvent.Count > 0)
					{
						var e = m_PeerEvent.Dequeue();
						m_DelayMessage.Enqueue(message.Copy());
						clientId = GetId(e.Peer.ConnectionId);
						channelName = null;
						payload = default;
						receiveTime = Time.realtimeSinceStartup;
						return e.EventType == PeerEvent.Type.Add ? NetEventType.Connect : NetEventType.Disconnect;
					}
					else
					{
						clientId = GetId(message.Peer.ConnectionId);
						m_ChannelName.TryGetValue(message.ChannelId, out channelName);
						payload = new ArraySegment<byte>(message.ToArray());
						receiveTime = Time.realtimeSinceStartup;
						return NetEventType.Data;
					}
				}
			}
			clientId = default;
			channelName = default;
			payload = default;
			receiveTime = default;
			return NetEventType.Nothing;
		}

		public override void Send(ulong clientId, ArraySegment<byte> data, string channelName)
		{
			if (m_ChannelId.TryGetValue(channelName, out var id))
			{
				var conn = m_Server ?? m_ClientConn;
				conn?.ChannelSend(id, GetId(clientId), data.Array, data.Offset, data.Count);
			}
		}

		public override void Shutdown()
		{
			m_Server?.Dispose();
			m_ClientConn?.Dispose();
		}

		public override SocketTasks StartClient()
		{
			var task = SocketTask.Working;
			StartClient(task);
			return task.AsTasks();
		}

		async void StartClient(SocketTask task)
		{
			try
			{
				IPAddress[] addresses = Dns.GetHostAddresses(m_ConnectAddress);
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
				m_ClientConn = await Connection.ConnectToServer(setting);
				Setup(m_ClientConn);
				task.Success = true;
				task.IsDone = true;
			}
			catch (Exception ex)
			{
				task.Success = false;
				task.TransportException = ex;
				task.IsDone = true;
			}
		}

		public override SocketTasks StartServer()
		{
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
			Setup(m_Server);
			return SocketTask.Done.AsTasks();
		}


		void Setup(Connection conn)
		{
			conn.OnPeerEvent += m_PeerEvent.Enqueue;
			for (byte i = 0; i < MLAPI_CHANNELS.Length; i++)
			{
				var info = MLAPI_CHANNELS[i];
				var channel = (short)(i + 101);
				var config = GetConfig(info.Type);

				conn.Channel.Bind(channel, config);
				m_ChannelName[channel] = info.Name;
				m_ChannelId[info.Name] = channel;
			}
		}

		private Channel.IConfig GetConfig(ChannelType type)
		{
			switch (type)
			{
				case ChannelType.Unreliable:
					return new Channel.UnreliableChannelConfig()
					{
						Encrypt = true,
					};
				case ChannelType.UnreliableSequenced:
					return new Channel.UnreliableChannelConfig()
					{
						Encrypt = true,
						Ordered = true,
					};
				case ChannelType.Reliable:
					return new Channel.ReliableChannelConfig()
					{
						Encrypt = true,
						Ordered = false,
					};
				case ChannelType.ReliableFragmentedSequenced:
				case ChannelType.ReliableSequenced:
				default:
					return new Channel.ReliableChannelConfig()
					{
						Encrypt = true,
					};
			}
		}

	}
}