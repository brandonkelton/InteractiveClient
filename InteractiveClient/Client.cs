using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveClient
{
    public class Client
    {
        public bool IsActive { get; private set; }
        public IPEndPoint Endpoint { get; private set; }
        public Socket Socket { get; private set; }

        public void Connect(string hostNameOrIpAddress, int port, Messenger messenger)
        {
            IPAddress ipAddress;
            IPHostEntry host;

            if (!IPAddress.TryParse(hostNameOrIpAddress, out ipAddress))
            {
                host = Dns.GetHostEntry(hostNameOrIpAddress);
                ipAddress = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            }

            Endpoint = new IPEndPoint(ipAddress, port);
            Socket = new Socket(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.IP);

            try
            {
                Socket.Connect(Endpoint);
                IsActive = true;
                messenger.Message.Append($"SUCCESSFULLY CONNECTED TO: {Socket.RemoteEndPoint.ToString()}");
            }
            catch (SocketException se)
            {
                messenger.IsError = true;

                if (se.ErrorCode == 10054)
                {
                    messenger.Message.Append("REMOTE HOST FORCIBLY CLOSED CONNECTION");
                }
                else
                {
                    messenger.Message.Append(se.ToString());
                }
            }
            catch (Exception e)
            {
                messenger.Message.Append(e.ToString());
                messenger.IsError = true;
            }
        }

        public void Send(string command, Messenger messenger)
        {
            byte[] data = Encoding.Unicode.GetBytes($"{command}<STOP>");

            try
            {
                int bytesSent = Socket.Send(data, 0, data.Length, SocketFlags.None);
            }
            catch (SocketException se)
            {
                messenger.IsError = true;

                if (se.ErrorCode == 10054)
                {
                    messenger.Message.Append("REMOTE HOST FORCIBLY CLOSED CONNECTION");
                }
                else
                {
                    messenger.Message.Append(se.ToString());
                }

                IsActive = false;
            }
            catch (Exception e)
            {
                messenger.Message.Append(e.ToString());
                messenger.IsError = true;
            }
        }

        public void Receive(Messenger messenger)
        {
            int bytesReceived = 0;

            try
            {
                bytesReceived = Socket.Receive(messenger.Buffer, 0, Messenger.BufferSize, SocketFlags.None);
            }
            catch (SocketException se)
            {
                messenger.IsError = true;

                if (se.ErrorCode == 10054)
                {
                    messenger.Message.Append("REMOTE HOST FORCIBLY CLOSED CONNECTION");
                }
                else
                {
                    messenger.Message.Append(se.ToString());
                }
            }
            catch (Exception e)
            {
                messenger.Message.Append(e.ToString());
                messenger.IsError = true;
            }

            if (bytesReceived > 0)
            {
                var message = Encoding.Unicode.GetString(messenger.Buffer, 0, bytesReceived);

                if (message.EndsWith("<STOP>"))
                {
                    messenger.Message.Append(message.Substring(0, message.IndexOf("<STOP>")));
                }
                else
                {
                    messenger.Message.Append(message);
                    messenger.ClearBuffer();
                    Receive(messenger);
                }
            }
        }

        public void Kill(Messenger messenger)
        {
            if (IsActive)
            {
                try
                {
                    Send("disconnect", messenger);
                    Socket.Close();
                }
                catch (Exception e)
                {
                    messenger.Message.Append(e.ToString());
                    messenger.IsError = true;
                }
            }
            
            IsActive = false;
        }
    }
}
