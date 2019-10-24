using Puppet_PCS;


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading.Tasks;

namespace PCS
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpChannel channel = new TcpChannel(10000);
            ChannelServices.RegisterChannel(channel, false);
            RemotingConfiguration.RegisterWellKnownServiceType(typeof(PCSImpl), "PCS", WellKnownObjectMode.Singleton);
            System.Console.WriteLine("<enter> para sair...");
            System.Console.ReadLine();
        }
    }

    class PCSImpl : MarshalByRefObject, IPCS
    {
        public void createServer(string serverID, int port, int maxFaults, int minDelay, int maxDelay)
        {
            ProcessStartInfo server = new ProcessStartInfo("..\\..\\..\\Server\\bin\\Debug\\Server.exe");
            server.Arguments = serverID + " " + port + " " + maxFaults + " " + minDelay + " " + maxDelay;
            Process.Start(server);
        }

        public void createClient(string username, int port, string serverURL, string pathScriptFile) //or should we pass the contents of the file as argument
        {
            ProcessStartInfo client = new ProcessStartInfo("..\\..\\..\\Client\\bin\\Debug\\Client.exe");
            client.Arguments = username + " " + port + " " + serverURL + " " + pathScriptFile;
            Process.Start(client);
        }
    }


}
