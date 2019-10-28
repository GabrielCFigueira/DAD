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

namespace PuppetMaster
{
    public partial class PuppetMaster : Form
    {

        private PuppetMasterImp pmi;
        
        public PuppetMaster()
        {
            InitializeComponent();
            pmi = new PuppetMasterImp();
            System.IO.StreamReader file = new System.IO.StreamReader("..\\..\\PCShostnames.txt");

            string line;
            while ((line = file.ReadLine()) != null)
            {
                pmi.addPCS(line);
            }

            file = new System.IO.StreamReader("..\\..\\Commands.txt");
            while ((line = file.ReadLine()) != null)
            {
                pmi.readCommand(line);
            }
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

            foreach (String serverURL in pmi.getServers())
            {
                int capacity = Int32.Parse(commands[1]);
                pmi.AddRoom(commands[0], capacity, commands[2], serverURL);
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
            foreach (String serverURL in pmi.getServers())
            {
                pmi.Status(serverURL);
            }
        }
    }

    public class PuppetMasterImp : IPuppet
    {
        private List<string> pcsList;
        private List<string> serverList;

        public PuppetMasterImp()
        {
            serverList = new List<string>();
            pcsList = new List<string>();
        }

        public void addPCS(string url)
        {
            pcsList.Add(url);
        }

        public List<string> getServers()
        {
            return serverList;
        }

        public string readCommand(string command)
        {
            string[] commands = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            switch (commands[0])
            {
                case "Server":
                    createServer(commands[1], commands[2], commands[3], commands[4], commands[5]);
                    return command;
                case "Client":
                    createClient(commands[1], commands[2], commands[3], commands[4]);
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
            foreach (string pcsURL in pcsList)
            {
                if (pcsURL.Equals(url.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries)[0]))
                {
                    IPCS ipcs = (IPCS)Activator.GetObject(typeof(IPCS), "tcp://" + pcsURL + ":10000/PCS");
                    PastCommand.Text += serverID + url + maxFaults + minDelay + maxDelay;
                    ipcs.createServer(serverID, url, maxFaults, minDelay, maxDelay);
                }
            }
            serverList.Add(url);
        }

        public void createClient(string username, string url, string serverURL, string pathScriptFile)
        {
            foreach (string pcsURL in pcsList)
            {
                if (pcsURL.Equals(url.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries)[0]))
                {
                    IPCS ipcs = (IPCS)Activator.GetObject(typeof(IPCS), "tcp://" + pcsURL + ":10000/PCS");
                    ipcs.createClient(username, url, serverURL, pathScriptFile);
                }
            }
        }

        public void shutdown()
        {
            foreach(string url in pcsList)
            {
                IPCS ipcs = (IPCS)Activator.GetObject(typeof(IPCS), "tcp://" + url + ":10000/PCS");
                ipcs.shutdown();
            }

            foreach(string url in serverList)
            {

            }

            Environment.Exit(0);
        }

        public void AddRoom(string location, int capacity, string room_name, string serverURL)
        {
            IServerPuppet server = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), serverURL + "/MeetingServer");
            server.AddRoom(location, capacity, room_name);
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

        public void Status(string serverURL)
        {
            IServerPuppet server = (IServerPuppet)Activator.GetObject(typeof(IServerPuppet), serverURL + "/MeetingServer");
            server.Status();
            throw new NotImplementedException();
        }

        public void Unfreeze(string server_id)
        {
            throw new NotImplementedException();
        }
    }
}
