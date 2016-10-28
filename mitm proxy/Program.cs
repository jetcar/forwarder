using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using X509Certificate = System.Security.Cryptography.X509Certificates.X509Certificate;

namespace mitmproxy
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(new IPEndPoint(IPAddress.Any, 8091));
            listener.Listen(1);

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
                        Console.Write(responce);
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
                        if (state.DestinationState == null || state.DestinationState.SSLStream == null)
                        {
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
                                            //state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);

                                            var sslStream = new SslStream(new NetworkStream(state.DestinationSocket));
                                            sslStream.AuthenticateAsClient(url.Split(':')[0]);

                                            var remoteSsl = new State(state.DestinationSocket, state.SourceSocket, state);
                                            remoteSsl.SSLStream = sslStream;
                                            state.DestinationState = remoteSsl;
                                            //state.DestinationState.SSLStream.BeginRead(state.DestinationState.Buffer, 0,
                                            //    state.DestinationState.Buffer.Length, OnDataReceive, state.DestinationState);

                                            state.SourceSocket.Send(Encoding.ASCII.GetBytes("HTTP/1.0 200 Connection established\r\n\r\n"));

                                            var localsslStream = new SslStream(new NetworkStream(state.SourceSocket));

                                            var cert = new X509Certificate("iis.pfx", "qqqqqq");
                                            localsslStream.AuthenticateAsServer(cert);

                                            localsslStream.BeginRead(state.Buffer, 0,
                                                state.Buffer.Length, OnDataReceiveLocal, state);
                                            state.SSLStream = localsslStream;
                                            //sslStream.Write(state.Buffer, 0, bytesRead);

                                            //no encryption
                                            //                                   var remotestate = new State(state.DestinationSocket, state.SourceSocket, state);
                                            //                                   state.SourceSocket.Send(Encoding.ASCII.GetBytes("HTTP/1.0 200 Connection established\r\n\r\n"));

                                            //                                   state.DestinationSocket.BeginReceive(remotestate.Buffer, 0, remotestate.Buffer.Length,
                                            //                       SocketFlags.None, ReadRemote, remotestate);

                                            //                                   state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length,
                                            //SocketFlags.None, ReadLocal, state);

                                            return;
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        //if (state.SSLStream == null)
                        state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);
                        //else
                        //{
                        //    state.SSLStream.Write(state.Buffer, 0, bytesRead);
                        //    state.SSLStream.BeginRead(state.DestinationState.Buffer, 0,
                        //        state.DestinationState.Buffer.Length, OnDataReceive, state.DestinationState);
                        //}

                        state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length,
                             SocketFlags.None, ReadLocal, state);
                    }
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

        private static void OnDataReceive(IAsyncResult ar)
        {
            State state = ar.AsyncState as State;

            try
            {
                int bytesread = state.SSLStream.EndRead(ar);
                if (bytesread > 0)
                {
                    string responce = Encoding.UTF8.GetString(state.Buffer, 0, bytesread);

                    state.DestinationState.SSLStream.Write(state.Buffer, 0, bytesread);
                }
                state.SSLStream.BeginRead(state.Buffer, 0, state.Buffer.Length, OnDataReceive, state);
            }
            catch (Exception e)
            {
            }
        }

        private static void OnDataReceiveLocal(IAsyncResult ar)
        {
            State state = ar.AsyncState as State;

            try
            {
                int bytesread = state.SSLStream.EndRead(ar);
                if (bytesread > 0)
                {
                    string responce = Encoding.UTF8.GetString(state.Buffer, 0, bytesread);

                    state.DestinationState.SSLStream.Write(state.Buffer, 0, bytesread);
                }
                state.SSLStream.BeginRead(state.Buffer, 0, state.Buffer.Length, OnDataReceiveLocal, state);
            }
            catch (Exception e)
            {
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
                    //SocketError.Success indicates that the last operation on the underlying socket succeeded
                    {
                        string responce = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);
                        Console.WriteLine(responce);
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

                        var io = state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length,
                            SocketFlags.None, ReadRemote, state);
                    }
                }
            }
            catch (Exception)
            {
                state.SourceSocket.Close();
                state.DestinationSocket.Close();
            }
        }

        public static Org.BouncyCastle.X509.X509Certificate generateRootCertV2(string certName)
        {
            X509V1CertificateGenerator certGen = new X509V1CertificateGenerator();

            X509Name CN = new X509Name("CN=" + certName);

            RsaKeyPairGenerator keypairgen = new RsaKeyPairGenerator();
            keypairgen.Init(new KeyGenerationParameters(new SecureRandom(new CryptoApiRandomGenerator()), 1024));

            AsymmetricCipherKeyPair keypair = keypairgen.GenerateKeyPair();

            certGen.SetSerialNumber(BigInteger.ProbablePrime(120, new Random()));
            certGen.SetIssuerDN(CN);
            certGen.SetNotAfter(DateTime.MaxValue);
            certGen.SetNotBefore(DateTime.Now.Subtract(new TimeSpan(7, 0, 0, 0)));
            certGen.SetSubjectDN(CN);
            certGen.SetPublicKey(keypair.Public);
            certGen.SetSignatureAlgorithm("MD5WithRSA");

            Org.BouncyCastle.X509.X509Certificate newCert = certGen.Generate(keypair.Private);

            return newCert;
        }

        public static bool addCertToStore(X509Certificate2 cert, StoreName st, StoreLocation sl)
        {
            bool bRet = false;

            try
            {
                X509Store store = new X509Store(st, sl);
                store.Open(OpenFlags.ReadWrite);

                if (cert != null)
                {
                    byte[] pfx = cert.Export(X509ContentType.Pfx);
                    cert = new X509Certificate2(pfx, (string)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet);

                    //if (!certExists(store, cert.SubjectName.Name))
                    //{
                    //    store.Add(cert);
                    //    bRet = true;
                    //}
                }
                store.Close();
            }
            catch
            {
            }

            return bRet;
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