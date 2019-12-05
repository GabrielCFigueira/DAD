using Puppet_PCS;


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace PCS
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpChannel channel = new TcpChannel(10000);
            ChannelServices.RegisterChannel(channel, false);
            PCSImpl pcs = new PCSImpl();
            RemotingServices.Marshal(pcs, "PCS", typeof(PCSImpl));
            Console.WriteLine("<enter> to quit...");

            Console.ReadLine();
        }
    }

    class PCSImpl : MarshalByRefObject, IPCS
    {
        public override object InitializeLifetimeService()
        {
            return null;
        }

        public void createServer(string serverID, string url, string maxFaults, string minDelay, string maxDelay, string masterServer)
        {
            ProcessStartInfo server = new ProcessStartInfo(@"..\..\..\Server\bin\Debug\Server.exe");
            server.Arguments = "Server " + serverID + " " + url + " " + maxFaults + " " + minDelay + " " + maxDelay + " " + masterServer;
            Process.Start(server);
        }

        public void createClient(string username, string url, string serverURL, string pathScriptFile) //or should we pass the contents of the file as argument
        {
            ProcessStartInfo client = new ProcessStartInfo(@"..\..\..\Client\bin\Debug\Client.exe");
            client.Arguments = "Client " + username + " " + url + " " + serverURL + " " + pathScriptFile;
            Process.Start(client);
        }

        public void shutdown()
        {
            Thread thread = new Thread(new ThreadStart(localShutdown));
            thread.Start();
        }

        private void localShutdown()
        {
            Thread.Sleep(2000);
            Environment.Exit(0);
        }
    }


}
