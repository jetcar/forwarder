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
        public static Action SyncSocket;
        public static Action<IntPtr, string> UpdateRequest;

        public void Start(IPEndPoint local, IPEndPoint remote)
        {
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(local);
            listener.Listen(100);

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
                try
                {
                    var client = listener.Accept();
                    var destination = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    if (SyncSocket != null)
                        SyncSocket.Invoke();
                    var state = new State(client, destination, null);

                    SocketAsyncEventArgs readsocket = new SocketAsyncEventArgs();
                    readsocket.Completed += new EventHandler<SocketAsyncEventArgs>(ReadLocal);
                    readsocket.SetBuffer(state.Buffer, 0, state.Buffer.Length);
                    readsocket.UserToken = state;
                    client.ReceiveAsync(readsocket);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private void ReadLocal(object sender, SocketAsyncEventArgs readSocket)
        {
            State state = readSocket.UserToken as State;

            if (readSocket.BytesTransferred > 0)
            {
                try
                {
                    //SocketError.Success indicates that the last operation on the underlying socket succeeded
                    if (readSocket.SocketError == SocketError.Success)
                    {
                        int bytesRead = readSocket.BytesTransferred;

                        string responce = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);

                        if (responce.Contains("HTTP/1.1 403")
                            ||
                            responce.Contains("HTTP/1.1 502")
                            )
                        {
                            state.SourceSocket.Close();
                            Console.WriteLine("HTTP/1.1 403");
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
                        if (UpdateRequest != null)
                            UpdateRequest.Invoke(state.SourceSocket.Handle, status);

                        Console.WriteLine("from:" + state.SourceSocket.LocalEndPoint + " to " +
                                          state.DestinationSocket.LocalEndPoint + " " + status);

                        state.ByteRequest = state.Buffer;
                        state.BytesRead = bytesRead;
                        state.StringRequest = responce;
                        if (!state.DestinationSocket.Connected)
                        {
                            state.DestinationSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
                                ProtocolType.Tcp);
                            state.DestinationSocket.Bind(new IPEndPoint(IPAddress.Parse("10.94.136.231"), 0));
                            state.DestinationSocket.Connect(new IPEndPoint(IPAddress.Parse("10.30.138.135"), 8090));

                            var remotestate = new State(state.DestinationSocket, state.SourceSocket, state);

                            SocketAsyncEventArgs readsocket = new SocketAsyncEventArgs();
                            readsocket.Completed += new EventHandler<SocketAsyncEventArgs>(ReadRemote);
                            readsocket.SetBuffer(remotestate.Buffer, 0, remotestate.Buffer.Length);
                            readsocket.UserToken = remotestate;
                            state.DestinationSocket.ReceiveAsync(readsocket);
                        }

                        state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);
                        var io = state.SourceSocket.ReceiveAsync(readSocket);
                    }
                    else
                    {
                        //state.SourceSocket.Close();
                    }
                }
                catch (Exception e)
                {
                    state.SourceSocket.Close();
                }
            }
            else
            {
                //state.SourceSocket.Close();
            }
        }

        private void ReadRemote(object sender, SocketAsyncEventArgs readSocket)
        {
            State state = readSocket.UserToken as State;

            if (readSocket.BytesTransferred > 0)
            {
                try
                {
                    //SocketError.Success indicates that the last operation on the underlying socket succeeded
                    if (readSocket.SocketError == SocketError.Success)
                    {
                        int bytesRead = readSocket.BytesTransferred;

                        string responce = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);

                        if (responce.Contains("HTTP/1.1 403")
                            ||
                            responce.Contains("HTTP/1.1 502")
                        )
                        {
                            state.SourceSocket.Close();
                            Console.WriteLine("HTTP/1.1 403");
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
                        if (UpdateRequest != null)
                            UpdateRequest.Invoke(state.SourceSocket.Handle, status);

                        state.ByteRequest = state.Buffer;
                        state.BytesRead = bytesRead;
                        state.StringRequest = responce;

                        state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);

                        Console.WriteLine("from:" + state.SourceSocket.LocalEndPoint + " to " +
                                          state.DestinationSocket.LocalEndPoint + " " + status);

                        var io = state.SourceSocket.ReceiveAsync(readSocket);
                    }
                    else
                    {
                        state.SourceSocket.Close();
                        state.DestinationSocket.Close();
                    }
                }
                catch (Exception)
                {
                    state.SourceSocket.Close();
                    state.DestinationSocket.Close();
                }
            }
            else
            {
                state.SourceSocket.Close();
                state.DestinationSocket.Close();
            }
        }

        private static void OnDataReceiveSendBack(IAsyncResult result)
        {
            var state = (State)result.AsyncState;
            try
            {
                int bytesRead;
                SocketError err;
                bytesRead = state.SourceSocket.EndReceive(result, out err);

                if (bytesRead > 0)
                {
                    string responce = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);

                    if (responce.Contains("HTTP/1.1 403")
                        ||
                        responce.Contains("HTTP/1.1 502")
                        )
                    {
                        state.SourceSocket.Close();
                        Console.WriteLine("HTTP/1.1 403");

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
                    if (UpdateRequest != null)
                        UpdateRequest.Invoke(state.SourceSocket.Handle, status);

                    Console.WriteLine("from:" + state.SourceSocket.LocalEndPoint + " to " +
                                      state.DestinationSocket.LocalEndPoint + " " + status);
                    //state.DestinationSocket.BeginSend(state.Buffer, 0, bytesRead, SocketFlags.None, BeginSend, state);
                    state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);
                    state.ByteRequest = state.Buffer;
                    state.BytesRead = bytesRead;
                    state.StringRequest = responce;

                    state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0, OnDataReceiveSendBack,
                        state);
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.Message);

                state.DestinationSocket.Close();
                state.SourceSocket.Close();
                if (SyncSocket != null)
                    SyncSocket.Invoke();

                //foreach (var socket in state.Local)
                //{
                //    socket.Close();
                //}
            }
        }

        public class State
        {
            public Socket SourceSocket { get; private set; }
            public Socket DestinationSocket { get; set; }
            public byte[] Buffer { get; private set; }
            public byte[] ByteRequest { get; set; }
            public string StringRequest { get; set; }
            public int BytesRead { get; set; }

            public State DestinationState { get; set; }

            public State(Socket source, Socket destination, State state)
            {
                SourceSocket = source;
                DestinationSocket = destination;
                Buffer = new byte[65536];
                DestinationState = state;
            }
        }
    }
}