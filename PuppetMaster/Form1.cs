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
            pmi = new PuppetMasterImp(@"..\..\PCShostnames.txt", @"..\..\Commands.txt");
            //TcpChannel channel = new TcpChannel(10001);
            //ChannelServices.RegisterChannel(channel, false);
            //RemotingServices.Marshal(pmi, "PuppetMaster");

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
            }
        }

        private void PastCommand_TextChanged(object sender, EventArgs e)
        {

        }

        private void PuppetMaster_Load(object sender, EventArgs e)
        {

        }
    }

    public class PuppetMasterImp : MarshalByRefObject, IPS
    {
        private List<Uri> pcsList;
        private List<Uri> serverList;
        private List<Uri> clientList;

        delegate void CreateServerDelegate(string s1, string s2, string s3, string s4, string s5);
        delegate void CreateClientDelegate(string s1, string s2, string s3, string s4);



        public PuppetMasterImp(string pcsHostnameFile, string commandsFile)
        {
            serverList = new List<Uri>();
            pcsList = new List<Uri>();
            clientList = new List<Uri>();

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
            serverList.Add(new Uri(url));
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
                    serverDelegate.BeginInvoke(commands[1], commands[2], commands[3], commands[4], commands[5], null, null);
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

        public void createServer(string serverID, string url, string maxFaults, string minDelay, string maxDelay)
        {
            foreach (Uri pcsURL in pcsList)
            {
                if (pcsURL.Host == (new Uri(url)).Host)
                {
                    IPCS ipcs = (IPCS)Activator.GetObject(typeof(IPCS), pcsURL.AbsoluteUri);
                    ipcs.createServer(serverID, url, maxFaults, minDelay, maxDelay);
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
                IPS ips = (IPS)Activator.GetObject(typeof(IPS), url.AbsoluteUri);
                ips.shutdown();
            }


            Environment.Exit(0);
        }

        public List<Uri> getServers()
        {
            return serverList;
        }

        public void AddRoom(string location, int capacity, string room_name)
        {
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

        public void Status()
        {
            throw new NotImplementedException();
        }

        public void Unfreeze(string server_id)
        {
            throw new NotImplementedException();
        }
    }
}
