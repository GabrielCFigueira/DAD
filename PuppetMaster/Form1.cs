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
            pmi = new PuppetMasterImp("..\\..\\PCShostnames.txt", "..\\..\\Commands.txt", PastCommand);
            RemotingServices.Marshal(pmi, "PuppetMaster", typeof(PuppetMasterImp));

        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void Button1_Click(object sender, EventArgs e)
        {
            string command = CommandBox.Text;
            CommandBox.Text = "";
            string[] commands = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (commands.Length == 0)
                return;

            PastCommand.Text += pmi.readCommand(command) + "\r\n";


     
        }

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

            foreach (Uri serverURL in pmi.getServers())
            {
                // Testar isto outra vez
                int capacity = Int32.Parse(commands[1]);
                string location = commands[0];
                string room_name = commands[2];

                IServerPuppet server = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), serverURL.AbsoluteUri);
                server.AddRoom(location, capacity, room_name);
            }
        }

        private void PastCommand_TextChanged(object sender, EventArgs e)
        {

        }

        private void PuppetMaster_Load(object sender, EventArgs e)
        {

        }

        private void Status_Click(object sender, EventArgs e)
        {
            foreach (Uri serverURL in pmi.getServers())
            {
                IServerPuppet server = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), serverURL.AbsoluteUri);
                server.Status();
            }
        }
    }

    public class PuppetMasterImp : MarshalByRefObject, IPuppet
    {
        private List<Uri> pcsList;
        private List<Uri> serverList;
        private List<Uri> clientList;

        private TextBox pastCommand;

        delegate void CreateServerDelegate(string s1, string s2, string s3, string s4, string s5, string s6);
        delegate void CreateClientDelegate(string s1, string s2, string s3, string s4);



        public PuppetMasterImp(string pcsHostnameFile, string commandsFile, TextBox PastCommand)
        {
            serverList = new List<Uri>();
            pcsList = new List<Uri>();
            clientList = new List<Uri>();

            pastCommand = PastCommand;

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

        public void addPCS(string url)
        {
            pcsList.Add(new Uri(url));
        }

        public void addServer(string url)
        {
            lock (serverList)
            {
                IServerPuppet newIps = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), url);
                foreach (Uri uri in serverList)
                {
                    IServerPuppet ips = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), uri.AbsoluteUri);
                    ips.AddServer(url);
                    newIps.AddServer(uri.AbsoluteUri);
                }
                serverList.Add(new Uri(url));
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
                    serverDelegate.BeginInvoke(commands[1], commands[2], commands[3], commands[4], commands[5], "tcp://localhost:10001/PuppetMaster", null, null);
                    return command;
                case "Client":
                    CreateClientDelegate clientDelegate = new CreateClientDelegate(createClient);
                    clientDelegate.BeginInvoke(commands[1], commands[2], commands[3], commands[4], null, null);
                    return command;
                case "Wait":
                    Thread.Sleep(Int32.Parse(commands[1]));
                    return command;
                case "Shutdown":
                    shutdown();
                    return "Shutdown";
                default:
                    return "What are you doing noob";
            }
        }

        public void createServer(string serverID, string url, string maxFaults, string minDelay, string maxDelay, string puppetURL)
        {
            foreach (Uri pcsURL in pcsList)
            {
                if (pcsURL.Host == (new Uri(url)).Host)
                {
                    IPCS ipcs = (IPCS)Activator.GetObject(typeof(IPCS), pcsURL.AbsoluteUri);
                    ipcs.createServer(serverID, url, maxFaults, minDelay, maxDelay, puppetURL);
                    break;
                }
            }
            addServer(url);

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

            foreach(Uri url in serverList)
            {
                IServerPuppet ips = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), url.AbsoluteUri);
                ips.shutdown();
            }


            Environment.Exit(0);
        }

        public List<Uri> getServers()
        {
            
            return serverList;
        }

        //Se calhar nao preciso disto para a interface do Puppet
        public void AddRoom(String location, int capacity, String room_name, Uri uri)
        {
            throw new NotImplementedException();
        }
        public void Status(string status)
        {
            pastCommand.Text += status + "\"\r\n";
            throw new NotImplementedException();
        }

        public void Crash(string server_id)
        {
            throw new NotImplementedException();
        }

        public void Freeze(string server_id)
        {
            throw new NotImplementedException();
        }

        public void Unfreeze(string server_id)
        {
            throw new NotImplementedException();
        }
    }
}
