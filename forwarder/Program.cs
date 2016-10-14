using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace forwarder
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            TcpForwarderSlim forwarderSlim = new TcpForwarderSlim();
            forwarderSlim.Start(new IPEndPoint(IPAddress.Any, 8090), new IPEndPoint(IPAddress.Parse("10.30.138.135"), 8090));
            Console.ReadLine();
        }
    }
}