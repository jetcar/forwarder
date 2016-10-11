using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace forwarder
{
    public class TcpForwarderSlim
    {
        private readonly Socket _mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public void Start(IPEndPoint local, IPEndPoint remote)
        {
            _mainSocket.Bind(local);
            _mainSocket.Listen(10);

            var localSockets = new List<Socket>();
            foreach (var ipAddress in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ipAddress.ToString().Contains("10.94.136.231"))
                    continue;
                if (ipAddress.ToString().Contains(":"))
                    continue;
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(ipAddress, 8091));
                socket.Listen(10);
                localSockets.Add(socket);
            }
            while (true)
            {
                var source = _mainSocket.Accept();
                var destination = new TcpForwarderSlim();
                var state = new State(source, destination._mainSocket, localSockets);
                destination._mainSocket.Bind(new IPEndPoint(IPAddress.Parse("10.94.136.231"), 0));
                destination.Connect(remote, source, localSockets);
                source.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0, OnDataReceive, state);
            }
        }

        private void Connect(EndPoint remoteEndpoint, Socket destination, List<Socket> local)
        {
            var state = new State(_mainSocket, destination, local);
            _mainSocket.Connect(remoteEndpoint);
            _mainSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, OnDataReceive, state);
        }

        private static void OnDataReceive(IAsyncResult result)
        {
            var state = (State)result.AsyncState;
            try
            {
                var bytesRead = state.SourceSocket.EndReceive(result);
                if (bytesRead > 0)
                {
                    Console.WriteLine("from:" + state.SourceSocket.LocalEndPoint + " to " +
                                      state.DestinationSocket.LocalEndPoint);
                    state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);
                    try
                    {
                        foreach (var socket in state.Local)
                        {
                            socket.Send(state.Buffer, bytesRead, SocketFlags.None);
                            socket.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0, OnDataReceive, state);
                        }
                    }
                    catch (Exception e)
                    {
                    }

                    state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0, OnDataReceive, state);
                }
            }
            catch (Exception Ex)
            {
                state.DestinationSocket.Close();
                state.SourceSocket.Close();
                foreach (var socket in state.Local)
                {
                    socket.Close();
                }
            }
        }

        private class State
        {
            public Socket SourceSocket { get; private set; }
            public Socket DestinationSocket { get; private set; }
            public List<Socket> Local { get; private set; }
            public byte[] Buffer { get; private set; }

            public State(Socket source, Socket destination, List<Socket> local)
            {
                SourceSocket = source;
                DestinationSocket = destination;
                Local = local;
                Buffer = new byte[65536];
            }
        }
    }
}