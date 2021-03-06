﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
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

            listener.Bind(new IPEndPoint(IPAddress.Any, 8091));
            listener.Listen(10);

            while (true)
            {
                try
                {
                    var client = listener.Accept();
                    var destination = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    var state = new State(client, destination, null);

                    client.BeginReceive(state.Buffer, 0, state.Buffer.Length,
                            SocketFlags.None, ReadLocal, state);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private static void ReadLocal(IAsyncResult ar)
        {
            State state = ar.AsyncState as State;
            try
            {
                int bytesRead = state.SourceSocket.EndReceive(ar);

                if (bytesRead > 0)
                {
                    //SocketError.Success indicates that the last operation on the underlying socket succeeded
                    {
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
                        //Console.WriteLine("from:" + state.SourceSocket.LocalEndPoint + " to " +
                        //                  state.DestinationSocket.LocalEndPoint + " " + status);

                        state.ByteRequest = state.Buffer;
                        state.BytesRead = bytesRead;
                        state.StringRequest = responce;
                        if (!state.DestinationSocket.Connected || status.Contains("GET ") || status.Contains("CONNECT "))
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

                                        state.DestinationSocket.BeginReceive(remotestate.Buffer, 0, remotestate.Buffer.Length,
                            SocketFlags.None, ReadRemote, remotestate);
                                    }
                                    break;
                                }
                                if (line.Contains("CONNECT")) //not really needed now
                                {
                                    var url = line.Replace("CONNECT", "").Replace(" ", "").Replace("HTTP/1.1", "");
                                    state.DestinationSocket.Connect(new DnsEndPoint(url.Split(':')[0],
                                        Convert.ToInt32(url.Split(':')[1])));

                                    if (state.DestinationSocket.Connected)
                                    {
                                        //no encryption
                                        var remotestate = new State(state.DestinationSocket, state.SourceSocket, state);
                                        state.SourceSocket.Send(Encoding.ASCII.GetBytes("HTTP/1.0 200 Connection established\r\n\r\n"));

                                        state.DestinationSocket.BeginReceive(remotestate.Buffer, 0, remotestate.Buffer.Length,
                            SocketFlags.None, ReadRemote, remotestate);

                                        state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length,
     SocketFlags.None, ReadLocal, state);

                                        return;
                                    }
                                    break;
                                }
                            }
                        }

                        state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);
                        state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length,
                             SocketFlags.None, ReadLocal, state);
                    }
                }
                else
                {
                    state.SourceSocket.Close();
                    //state.DestinationSocket.Close();
                }
            }
            catch (Exception e)
            {
                state.SourceSocket.Close();
            }
        }

        private static void ReadRemote(IAsyncResult ar)
        {
            State state = ar.AsyncState as State;
            try
            {
                int bytesRead = state.SourceSocket.EndReceive(ar);

                if (bytesRead > 0)
                {
                    state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);

                    var io = state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length,
                        SocketFlags.None, ReadRemote, state);
                }
                else
                {
                    state.SourceSocket.Close();
                    //state.DestinationSocket.Close();
                }
            }
            catch (Exception)
            {
                state.SourceSocket.Close();
                //state.DestinationSocket.Close();
            }
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