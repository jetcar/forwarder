using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace proxy
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(new IPEndPoint(IPAddress.Any, 8090));
            listener.Listen(100);

            while (true)
            {
                try
                {
                    var client = listener.Accept();
                    var destination = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
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

        private static void ReadLocal(object sender, SocketAsyncEventArgs readSocket)
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
                        Console.WriteLine("from:" + state.SourceSocket.LocalEndPoint + " to " +
                                          state.DestinationSocket.LocalEndPoint + " " + status);

                        state.ByteRequest = state.Buffer;
                        state.BytesRead = bytesRead;
                        state.StringRequest = responce;
                        if (!state.DestinationSocket.Connected)
                        {
                            state.DestinationSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
                                ProtocolType.Tcp);
                            state.DestinationSocket.Bind(new IPEndPoint(IPAddress.Parse("172.20.10.2"), 0));

                            var lines = state.StringRequest.Split('\r');
                            foreach (var line in lines)
                            {
                                if (line.Contains("Host:"))
                                {
                                    state.DestinationSocket.Connect(
                                        new DnsEndPoint(line.Replace("\nHost:", "").Replace(" ", ""), 80));

                                    if (state.DestinationSocket.Connected)
                                    {
                                        var remotestate = new State(state.DestinationSocket, state.SourceSocket, state);

                                        SocketAsyncEventArgs readsocket = new SocketAsyncEventArgs();
                                        readsocket.Completed += new EventHandler<SocketAsyncEventArgs>(ReadRemote);
                                        readsocket.SetBuffer(remotestate.Buffer, 0, remotestate.Buffer.Length);
                                        readsocket.UserToken = remotestate;
                                        state.DestinationSocket.ReceiveAsync(readsocket);
                                    }
                                    break;
                                }
                                if (line.Contains("CONNECT")) //not really needed now
                                {
                                    var url = line.Replace("CONNECT", "").Replace(" ", "").Replace("HTTP/1.1", "");
                                    state.DestinationSocket.Connect(new DnsEndPoint(url.Split(':')[0],
                                        Convert.ToInt32(url.Split(':')[1])));
                                    var sslStream = new SslStream(new NetworkStream(state.DestinationSocket));
                                    sslStream.AuthenticateAsClient(url.Split(':')[0]);

                                    if (state.DestinationSocket.Connected && sslStream.IsAuthenticated)
                                    {
                                        state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);

                                        //var remoteSsl = new State(state.DestinationSocket, state.SourceSocket, state);
                                        //remoteSsl.SSLStream = sslStream;
                                        //state.DestinationState = remoteSsl;
                                        //state.DestinationState.SSLStream.BeginRead(state.DestinationState.Buffer, 0,
                                        //    state.DestinationState.Buffer.Length, OnDataReceive, state.DestinationState);

                                        var remotestate = new State(state.DestinationSocket, state.SourceSocket, state);

                                        SocketAsyncEventArgs readsocket = new SocketAsyncEventArgs();
                                        readsocket.Completed += new EventHandler<SocketAsyncEventArgs>(ReadRemote);
                                        readsocket.SetBuffer(remotestate.Buffer, 0, remotestate.Buffer.Length);
                                        readsocket.UserToken = remotestate;
                                        state.DestinationSocket.ReceiveAsync(readsocket);

                                        //state.SourceSocket.Send(Encoding.UTF8.GetBytes("HTTP/1.1 200 Connection established"));
                                        //state.SourceSocket.ReceiveAsync(readSocket);
                                        return;
                                        //state.SourceSocket.BeginReceive(sslToLocal.Buffer, 0, sslToLocal.Buffer.Length, 0, OnDataReceive, sslToLocal);
                                    }
                                    break;
                                }
                            }
                        }
                        if (state.SSLStream == null)
                            state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);
                        else
                        {
                            state.SSLStream.Write(state.Buffer, 0, bytesRead);
                            state.DestinationState.SSLStream.BeginRead(state.DestinationState.Buffer, 0,
                                state.DestinationState.Buffer.Length, OnDataReceive, state.DestinationState);
                        }

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

        private static void OnDataReceive(IAsyncResult ar)
        {
            State state = ar.AsyncState as State;

            try
            {
                int bytesread = state.SSLStream.EndRead(ar);
                if (bytesread > 0)
                {
                    state.DestinationSocket.Send(state.Buffer, bytesread, SocketFlags.None);
                }
                state.SSLStream.BeginRead(state.Buffer, 0, state.Buffer.Length, OnDataReceive, state);
            }
            catch (Exception e)
            {
            }
        }

        private static void ReadRemote(object sender, SocketAsyncEventArgs readSocket)
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
    }

    public class State
    {
        public SslStream SSLStream { get; set; }
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