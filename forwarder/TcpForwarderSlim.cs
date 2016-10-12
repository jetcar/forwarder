using System;
using System.Collections.Generic;
using System.Linq;
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

            var localSockets = new List<IPEndPoint>();
            foreach (var ipAddress in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                //if (ipAddress.ToString().Contains("10.94.136.231"))
                //    continue;
                if (ipAddress.ToString().Contains(":"))
                    continue;
                localSockets.Add(new IPEndPoint(ipAddress, 0));
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

        private void Connect(EndPoint remoteEndpoint, Socket destination, List<IPEndPoint> local)
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
                    string responce = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);

                    if (responce.Contains("HTTP/1.1 403 URLBlocked"))
                    {
                        state.SourceSocket.Close();
                        return;
                    }
                    //Console.WriteLine(responce);

                    var status = "";
                    if (responce.StartsWith("HTTP"))
                    {
                        status = responce.Split('\r')[0];
                    }
                    Console.WriteLine("from:" + state.SourceSocket.LocalEndPoint + " to " +
                                      state.DestinationSocket.LocalEndPoint + " " + status);
                    state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);
                    try
                    {
                        foreach (var endPoint in state.Local)
                        {
                            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            socket.Bind(endPoint);
                            if (!socket.Connected && (responce.StartsWith("GET") || responce.StartsWith("POST") || responce.StartsWith("CONNECT")))
                            {
                                var lines = responce.Split('\r');
                                foreach (var line in lines)
                                {
                                    if (line.Contains("Host:"))
                                    {
                                        socket.Connect(new DnsEndPoint(line.Replace("\nHost:", "").Replace(" ", ""), 80));
                                        socket.Send(state.Buffer.Take(bytesRead).ToArray());
                                        break;
                                    }
                                    if (line.Contains("CONNECT"))
                                    {
                                        var url = line.Replace("CONNECT", "").Replace(" ", "").Replace("HTTP/1.1", "");
                                        socket.Connect(new DnsEndPoint(url.Split(':')[0], Convert.ToInt32(url.Split(':')[1])));
                                        socket.Send(state.Buffer.Take(bytesRead).ToArray());
                                        break;
                                    }
                                }
                            }
                            if (socket.Connected)
                            {
                                var innerState = new State(socket, state.SourceSocket, new List<IPEndPoint>());
                                socket.Send(innerState.Buffer, bytesRead, SocketFlags.None);
                                socket.BeginReceive(innerState.Buffer, 0, innerState.Buffer.Length, 0, OnDataReceive,
                                    innerState);
                            }
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
                //foreach (var socket in state.Local)
                //{
                //    socket.Close();
                //}
            }
        }

        private class State
        {
            public Socket SourceSocket { get; private set; }
            public Socket DestinationSocket { get; private set; }
            public List<IPEndPoint> Local { get; private set; }
            public byte[] Buffer { get; private set; }

            public State(Socket source, Socket destination, List<IPEndPoint> local)
            {
                SourceSocket = source;
                DestinationSocket = destination;
                Local = local;
                Buffer = new byte[65536];
            }
        }
    }
}