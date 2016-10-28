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
            listener.Listen(10);

            while (true)
            {
                try
                {
                    var client = listener.Accept();
                    var destination = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    if (SyncSocket != null)
                        SyncSocket.Invoke();
                    var state = new State(client, destination, null);

                    state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ReadLocal, state);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private void ReadLocal(IAsyncResult readSocket)
        {
            State state = readSocket.AsyncState as State;

            try
            {
                var BytesTransferred = state.SourceSocket.EndReceive(readSocket);

                if (BytesTransferred == 0)
                {
                    state.SourceSocket.Close();
                    state.DestinationSocket.Close();
                }
                else
                {
                    int bytesRead = BytesTransferred;

                    string responce = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);

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
                        state.DestinationState = remotestate;
                        state.DestinationSocket.BeginReceive(remotestate.Buffer, 0, remotestate.Buffer.Length,
                            SocketFlags.None, ReadRemote, remotestate);
                    }

                    state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);
                    state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None,
                        ReadLocal, state);
                }
            }
            catch (Exception e)
            {
                state.SourceSocket.Close();
                state.DestinationSocket.Close();
            }
        }

        private void ReadRemote(IAsyncResult readSocket)
        {
            State state = readSocket.AsyncState as State;

            try
            {
                var BytesTransferred = state.SourceSocket.EndReceive(readSocket);
                if (BytesTransferred == 0)
                {
                    state.SourceSocket.Close();
                    state.DestinationSocket.Close();
                }
                else
                {
                    int bytesRead = BytesTransferred;

                    string responce = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);

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

                    state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ReadRemote,
                        state);
                }
            }
            catch (Exception)
            {
                state.SourceSocket.Close();
                state.DestinationSocket.Close();
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