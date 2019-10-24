using Puppet_PCS;


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

        public PuppetMaster()
        {
            InitializeComponent();
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

            foreach(string pcsURL in pcsList)
            {
                if(pcsURL.Equals(url.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries)[0]))
                {
                    IPCS ipcs = (IPCS)Activator.GetObject(typeof(IPCS), "tcp://" + pcsURL + ":10000/PCS");
                    switch(commands[0])
                    {
                        case "Server":
                            ipcs.createServer(commands[1], commands[2], commands[3], commands[4], commands[5]);
                            break;
                        case "Client":
                            ipcs.createClient(commands[1], commands[2], commands[3], commands[4]);
                            break;
                        default:
                            Console.WriteLine("What are doing noob");
                            break;
                    }
                }
            }
        }

        private void PastCommand_TextChanged(object sender, EventArgs e)
        {

        }

    }
}
