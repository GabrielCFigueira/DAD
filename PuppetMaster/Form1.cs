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

        private List<string> pcsList = new List<string>();
        private PuppetMasterImp pmi;
        
        public PuppetMaster()
        {
            InitializeComponent();
            pmi = new PuppetMasterImp();
            System.IO.StreamReader file = new System.IO.StreamReader("..\\..\\PCShostnames.txt");

            string line;
            while ((line = file.ReadLine()) != null)
            {
                pcsList.Add(line);
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
            if (commands.Length < 4)
            {
                PastCommand.Text += "Invalid Command: \"" + command + "\"\r\n";
                return;
            }
            PastCommand.Text += command + "\r\n";
            string url = commands[2];

            //Add server
            pmi.addServer(url);

            switch (commands[0])
            {
                case "Server":
                case "Client":
                    foreach (string pcsURL in pcsList)
                    {
                        if (pcsURL.Equals(url.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries)[0]))
                        {
                            IPCS ipcs = (IPCS)Activator.GetObject(typeof(IPCS), "tcp://" + pcsURL + ":10000/PCS");
                            if (commands[0].Equals("Server"))
                            {
                                ipcs.createServer(commands[1], commands[2], commands[3], commands[4], commands[5]);
                            }
                            else
                            {
                                ipcs.createClient(commands[1], commands[2], commands[3], commands[4]);
                            }
                        }
                    }
                    break;
                default:
                    PastCommand.Text += "What are you doing noob\r\n";
                    break;
            }
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
            }
        }

        private void PastCommand_TextChanged(object sender, EventArgs e)
        {

        }

        private void PuppetMaster_Load(object sender, EventArgs e)
        {

        }
    }

    public class PuppetMasterImp : IPS
    {
        private List<String> serverList;
        public PuppetMasterImp()
        {
            serverList = new List<String>();
        }

        public void addServer(string serverURL)
        {
            serverList.Add(serverURL);
        }

        public List<string> getServers()
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
