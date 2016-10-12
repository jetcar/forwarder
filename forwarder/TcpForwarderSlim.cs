using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
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
                if (ipAddress.ToString().Contains("10.94.136.231"))
                    continue;
                if (ipAddress.ToString().Contains(":"))
                    continue;
                localSockets.Add(new IPEndPoint(ipAddress, 0));
            }
            while (true)
            {
                var source = _mainSocket.Accept();
                var destination = new TcpForwarderSlim();
                var state = new State(source, destination._mainSocket, localSockets, null);
                destination._mainSocket.Bind(new IPEndPoint(IPAddress.Parse("10.94.136.231"), 0));
                destination.Connect(remote, source, localSockets, state);
                source.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0, OnDataReceive, state);
            }
        }

        private void Connect(EndPoint remoteEndpoint, Socket destination, List<IPEndPoint> local, State destinationstate)
        {
            var state = new State(_mainSocket, destination, local, destinationstate);
            destinationstate.DestinationState = destinationstate;
            _mainSocket.Connect(remoteEndpoint);
            _mainSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, OnDataReceive, state);
        }

        private static void OnDataReceive(IAsyncResult result)
        {
            var state = (State)result.AsyncState;
            try
            {
                int bytesRead;
                bytesRead = state.SourceSocket.EndReceive(result);

                if (bytesRead > 0)
                {
                    string responce = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);

                    if (responce.Contains("HTTP/1.1 403 URLBlocked"))
                    {
                        //state.SourceSocket.Close();
                        Console.WriteLine("HTTP/1.1 403 URLBlocked");
                        try
                        {
                            {
                                foreach (var endPoint in state.Local)
                                {
                                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                                    socket.Bind(endPoint);
                                    if (!socket.Connected)
                                    {
                                        var lines = state.DestinationState.StringRequest.Split('\r');
                                        foreach (var line in lines)
                                        {
                                            if (line.Contains("Host:"))
                                            {
                                                socket.Connect(new DnsEndPoint(line.Replace("\nHost:", "").Replace(" ", ""), 80));

                                                if (socket.Connected)
                                                {
                                                    var innerState = new State(socket, state.SourceSocket, new List<IPEndPoint>(), state);
                                                    socket.Send(state.DestinationState.ByteRequest, state.DestinationState.BytesRead, SocketFlags.None);
                                                    socket.BeginReceive(innerState.Buffer, 0, innerState.Buffer.Length, 0, OnDataReceive,
                                                        innerState);
                                                }
                                                break;
                                            }
                                            //if (line.Contains("CONNECT")) //not really needed now
                                            //{
                                            //    var url = line.Replace("CONNECT", "").Replace(" ", "").Replace("HTTP/1.1", "");
                                            //    socket.Connect(new DnsEndPoint(url.Split(':')[0], Convert.ToInt32(url.Split(':')[1])));
                                            //    var sslStream = new SslStream(new NetworkStream(socket));
                                            //    sslStream.AuthenticateAsClient(url.Split(':')[0]);

                                            //    if (socket.Connected && sslStream.IsAuthenticated)
                                            //    {
                                            //        var localToSsl = new State(socket, state.SourceSocket, new List<IPEndPoint>());
                                            //        localToSsl.SourceStream = sslStream;
                                            //        //sslStream.BeginRead(localToSsl.Buffer, 0, localToSsl.Buffer.Length, OnDataReceive, localToSsl);

                                            //        state.SourceSocket.Send(Encoding.UTF8.GetBytes("HTTP/1.0 200 Connection established"));

                                            //        var sslToLocal = new State(state.SourceSocket, socket, new List<IPEndPoint>());
                                            //        sslToLocal.DestinationStream = sslStream;

                                            //        state.SourceSocket.Send(Encoding.UTF8.GetBytes("HTTP/1.0 200 Connection established"));
                                            //        //state.SourceSocket.BeginReceive(sslToLocal.Buffer, 0, sslToLocal.Buffer.Length, 0, OnDataReceive, sslToLocal);
                                            //    }
                                            //    break;
                                            //}
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }

                        return;
                    }

                    var status = "";
                    if (responce.StartsWith("HTTP"))
                    {
                        status = responce.Split('\r')[0];
                    }
                    if (responce.StartsWith("GET"))
                    {
                        status = responce.Split('\r')[0];
                    }
                    if (responce.StartsWith("POST"))
                    {
                        status = responce.Split('\r')[0];
                    }
                    if (responce.StartsWith("CONNECT"))
                    {
                        status = responce.Split('\r')[0];
                    }
                    Console.WriteLine("from:" + state.SourceSocket.LocalEndPoint + " to " +
                                      state.DestinationSocket.LocalEndPoint + " " + status);
                    state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);

                    state.DestinationState.ByteRequest = state.Buffer;
                    state.DestinationState.BytesRead = bytesRead;
                    state.DestinationState.StringRequest = responce;

                    state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0, OnDataReceive, state);
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.Message);

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
            public byte[] ByteRequest { get; set; }
            public string StringRequest { get; set; }
            public int BytesRead { get; set; }
            public State DestinationState { get; set; }

            public State(Socket source, Socket destination, List<IPEndPoint> local, State state)
            {
                SourceSocket = source;
                DestinationSocket = destination;
                Local = local;
                Buffer = new byte[65536];
                DestinationState = state;
            }
        }
    }
}