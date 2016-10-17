using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace proxy
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Socket _mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _mainSocket.Bind(new IPEndPoint(IPAddress.Any, 8091));
            _mainSocket.Listen(100);

            while (true)
            {
                try
                {
                    var source = _mainSocket.Accept();
                    var remote = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    var state = new State(source, remote, null);

                    remote.Bind(new IPEndPoint(IPAddress.Parse("10.94.136.231"), 0));

                    var remotestate = new State(_mainSocket, remote, state);

                    SocketAsyncEventArgs readsocket2 = new SocketAsyncEventArgs();
                    readsocket2.Completed += new EventHandler<SocketAsyncEventArgs>(ReadRemote);
                    readsocket2.SetBuffer(state.Buffer, 0, state.Buffer.Length);
                    readsocket2.UserToken = remotestate;
                    _mainSocket.ReceiveAsync(readsocket2);

                    SocketAsyncEventArgs readsocket = new SocketAsyncEventArgs();
                    readsocket.Completed += new EventHandler<SocketAsyncEventArgs>(ReadLocal);
                    readsocket.SetBuffer(state.Buffer, 0, state.Buffer.Length);
                    readsocket.UserToken = state;
                    source.ReceiveAsync(readsocket);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

                Console.ReadLine();
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

                        state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);
                        var io = state.SourceSocket.ReceiveAsync(readSocket);
                    }
                    else
                    {
                        //state.SourceSocket.Close();
                    }
                }
                catch (Exception)
                {
                    state.SourceSocket.Close();
                }
            }
            else
            {
                //state.SourceSocket.Close();
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
        public Socket SourceSocket { get; private set; }
        public Socket DestinationSocket { get; private set; }
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