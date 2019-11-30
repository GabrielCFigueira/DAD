using Puppet_PCS;
using Puppet_Server;


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Threading;
using System.Runtime.Remoting;

namespace PuppetMaster
{
    public partial class PuppetMaster : Form
    {

        private PuppetMasterImp pmi;


        public PuppetMaster()
        {
            InitializeComponent();
            TcpChannel channel = new TcpChannel(10001);
            ChannelServices.RegisterChannel(channel, false);
            pmi = new PuppetMasterImp("..\\..\\PCShostnames.txt", "..\\..\\Commands.txt");
            RemotingServices.Marshal(pmi, "PuppetMaster", typeof(PuppetMasterImp));

        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        //Send
        private void Button1_Click(object sender, EventArgs e)
        {
            string command = CommandBox.Text;
            CommandBox.Text = "";
            string[] commands = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (commands.Length == 0)
                return;

            PastCommand.Text += pmi.readCommand(command) + "\r\n";
        }

        //AddRoom FIXME por no readCommand
        private void button2_Click(object sender, EventArgs e)
        {
            string command = CommandBox.Text;
            CommandBox.Text = "";
            string[] commands = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (commands.Length == 0)
                return;
            if (commands.Length < 3)
            {
                PastCommand.Text += "Invalid Command: \"" + command + "\"\r\n";
                return;
            }

            int capacity = Int32.Parse(commands[1]);
            string location = commands[0];
            string room_name = commands[2];

            PastCommand.Text += "Added a Room: " + room_name + " in the Location: " + location + " with Capacity: " + capacity + "\r\n";
            
            foreach (string serverID in pmi.getServers().Keys)
            {
                IServerPuppet server = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), pmi.getServers()[serverID].AbsoluteUri);
                server.AddRoom(location, capacity, room_name);
            }
        }

        //TextBox for past commands
        private void PastCommand_TextChanged(object sender, EventArgs e)
        {

        }

        private void PuppetMaster_Load(object sender, EventArgs e)
        {

        }

        private void Status_Click(object sender, EventArgs e)
        {
            PastCommand.Text += "Servers printed their Status\r\n";

            foreach (string serverID in pmi.getServers().Keys)
            {
                IServerPuppet server = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), pmi.getServers()[serverID].AbsoluteUri);
                server.Status();
            }
        }

        private void Crash_Click(object sender, EventArgs e)
        {
            string command = CommandBox.Text;
            CommandBox.Text = "";
            string[] commands = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (commands.Length == 0)
                return;
            if (commands.Length < 0)
            {
                PastCommand.Text += "Invalid Command: \"" + command + "\"\r\n";
                return;
            }

            string serverID = commands[0];

            PastCommand.Text += "Crashing Server: " + serverID;

            string url = pmi.getServers()[serverID].AbsoluteUri;

            IServerPuppet server = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), url);
            server.Crash();

            //Removes from the server dictionary
            pmi.getServers().Remove(serverID);
        }
    }

    public class PuppetMasterImp : MarshalByRefObject, IPuppet
    {
        private List<Uri> pcsList;
        private Dictionary<string, Uri> serverDict;
        private List<Uri> clientList;
        private string masterServer = "1";

        delegate void CreateServerDelegate(string s1, string s2, string s3, string s4, string s5, string s6, string s7);
        delegate void CreateClientDelegate(string s1, string s2, string s3, string s4);
        delegate void CreateAddRoomDelegate(string s1, int i, string s2);
        delegate void CreateStatusDelegate();
        delegate void CreateCrashFreezeUnfreezeDelegate(string s1);


        public PuppetMasterImp(string pcsHostnameFile, string commandsFile)
        {
            pcsList = new List<Uri>();
            clientList = new List<Uri>();
            serverDict = new Dictionary<string, Uri>();

            
            System.IO.StreamReader file = new System.IO.StreamReader(pcsHostnameFile);

            string line;
            while ((line = file.ReadLine()) != null)
            {
                addPCS(line);
            }

            file = new System.IO.StreamReader(commandsFile);
            while ((line = file.ReadLine()) != null)
            {
                readCommand(line);
            }
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        public void addPCS(string url)
        {
            pcsList.Add(new Uri(url));
        }

        public void addServer(string serverID, string url)
        {
            lock (serverDict)
            {
                IServerPuppet newIps = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), url);
                foreach (string serverId in serverDict.Keys)
                {
                    IServerPuppet ips = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), serverDict[serverId].AbsoluteUri);
                    ips.AddServer(url);
                    newIps.AddServer(serverDict[serverId].AbsoluteUri);
                }
                serverDict.Add(serverID, new Uri(url));
            }
        }

        public void addClient(string url)
        {
            clientList.Add(new Uri(url));
        }

        public string readCommand(string command)
        {
            string[] commands = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            switch (commands[0])
            {
                case "Server":
                    CreateServerDelegate serverDelegate = new CreateServerDelegate(createServer);
                    serverDelegate.BeginInvoke(commands[1], commands[2], commands[3], commands[4], commands[5], "tcp://localhost:10001/PuppetMaster", masterServer, null, null);
                    if(masterServer == "1")
                    {
                        masterServer = commands[2];
                    }
                    return command;

                case "Client":
                    CreateClientDelegate clientDelegate = new CreateClientDelegate(createClient);
                    clientDelegate.BeginInvoke(commands[1], commands[2], commands[3], commands[4], null, null);
                    return command;

                case "AddRoom":
                    string location = commands[1];
                    int capacity = Int32.Parse(commands[2]);
                    string room_name = commands[3];

                    CreateAddRoomDelegate addRoomDelegate = new CreateAddRoomDelegate(AddRoom);
                    addRoomDelegate.BeginInvoke(location, capacity, room_name, null, null);

                    return command;


                case "Status":
                    CreateStatusDelegate statusDelegate = new CreateStatusDelegate(Status);
                    statusDelegate.BeginInvoke(null, null);

                    return command;

                case "Crash":
                    string serverID = commands[1];

                    CreateCrashFreezeUnfreezeDelegate crashDelegate = new CreateCrashFreezeUnfreezeDelegate(Crash);
                    crashDelegate.BeginInvoke(serverID, null, null);

                    return command;

                case "Freeze":
                    string serverID2 = commands[1];

                    CreateCrashFreezeUnfreezeDelegate freezeDelegate = new CreateCrashFreezeUnfreezeDelegate(Freeze);
                    freezeDelegate.BeginInvoke(serverID2, null, null);

                    return command;

                case "Unfreeze":
                    string serverID3 = commands[1];

                    CreateCrashFreezeUnfreezeDelegate unfreezeDelegate = new CreateCrashFreezeUnfreezeDelegate(Unfreeze);
                    unfreezeDelegate.BeginInvoke(serverID3, null, null);

                    return command;

                case "Wait":
                    Thread.Sleep(Int32.Parse(commands[1]));
                    return command;

                case "Shutdown":
                    shutdown();
                    return "Shutdown";

                default:
                    return "Wrong Command";
            }
        }

        public void createServer(string serverID, string url, string maxFaults, string minDelay, string maxDelay, string puppetURL, string masterServer)
        {
            foreach (Uri pcsURL in pcsList)
            {
                if (pcsURL.Host == (new Uri(url)).Host)
                {
                    IPCS ipcs = (IPCS)Activator.GetObject(typeof(IPCS), pcsURL.AbsoluteUri);
                    ipcs.createServer(serverID, url, maxFaults, minDelay, maxDelay, puppetURL, masterServer);
                    break;
                }
            }
            addServer(serverID, url);

        }

        public void createClient(string username, string url, string serverURL, string pathScriptFile)
        {
            foreach (Uri pcsURL in pcsList)
            {
                if (pcsURL.Host == (new Uri(url)).Host)
                {
                    IPCS ipcs = (IPCS)Activator.GetObject(typeof(IPCS), pcsURL.AbsoluteUri);
                    ipcs.createClient(username, url, serverURL, pathScriptFile);
                    break;
                }
            }
            addClient(url);
        }

        public void shutdown()
        {
            foreach(Uri url in pcsList)
            {
                IPCS ipcs = (IPCS)Activator.GetObject(typeof(IPCS), url.AbsoluteUri);
                ipcs.shutdown();
            }

            foreach(string serverID in serverDict.Keys)
            {
                IServerPuppet ips = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), serverDict[serverID].AbsoluteUri);
                ips.shutdown();
            }


            Environment.Exit(0);
        }

        public Dictionary<string, Uri> getServers() 
        {
            return serverDict; 
        }

   
        public void AddRoom(String location, int capacity, String room_name)
        {
            foreach (string serverID in serverDict.Keys)
            {
                IServerPuppet server = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), serverDict[serverID].AbsoluteUri);
                server.AddRoom(location, capacity, room_name);
            }
        }
        
        public void Status()
        {
            foreach (string serverID in serverDict.Keys)
            {
                IServerPuppet server = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), serverDict[serverID].AbsoluteUri);
                server.Status();
            }
        }

        public void Crash(string serverID)
        {

            string url = serverDict[serverID].AbsoluteUri;

            IServerPuppet server = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), url);
            server.Crash();

            //Removes from the server dictionary
            serverDict.Remove(serverID);

        }

        public void Freeze(string serverID)
        {
            throw new NotImplementedException();
        }

        public void Unfreeze(string serverID)
        {
            throw new NotImplementedException();
        }
    }
}
