using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Project
{
    class Client
    {

        static void Main(string[] args)
        {
            String userName = args[1];
            String clientUrl = args[2];
            String serverUrl = args[3];
            String scriptFileName = args[4];

            Uri clientUri = new Uri(clientUrl);
            //string[] clientURL_parts = clientUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            //string remoteObjectName = clientURL_parts[clientURL_parts.Length - 1];  

            TcpChannel channel = new TcpChannel(clientUri.Port);
            ChannelServices.RegisterChannel(channel, false);

            ClientImpl MeetingClient = new ClientImpl(userName);
            RemotingServices.Marshal(MeetingClient, clientUri.Segments[1], typeof(ClientImpl));
            ServerInterface server = (ServerInterface)Activator.GetObject(typeof(ServerInterface), serverUrl);
            server.Connect(clientUrl,userName);

            string command = "";
            StreamReader file = new StreamReader(scriptFileName);
            while ((command = file.ReadLine()) != null)
            {
                MeetingClient.ReadCommands(command);
            }
            file.Close();

            while (true)
            {
                command = Console.ReadLine();
                MeetingClient.ReadCommands(command);
            }

            //System.Console.ReadLine();

        }
    }

    class ClientImpl : MarshalByRefObject, ClientInterface
    {
        String UserName;
        ServerInterface Server;

        public ClientImpl(String userName)
        {
            this.UserName = userName;
        }

        public void ReadCommands(String command)
        {
            string[] commandParams = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            switch (commandParams[0])
            {
                case "list":
                    this.ListMeetings();
                    break;
                case "create":
                    String meetingTopic = commandParams[1];
                    int min_attendees = Int32.Parse(commandParams[2]);
                    int n_slots = Int32.Parse(commandParams[3]);
                    int n_invitees = Int32.Parse(commandParams[4]);
                    List<String> meeting_slots = new List<String>();
                    List<String> invitees = new List<String>();
                    for (int i = 5; i < n_slots + 5; i++)
                    {
                        string slot = commandParams[i];
                        meeting_slots.Add(slot);
                    }
                    if (n_invitees != 0) { 
                        for (int i = 5 + n_slots; i < n_invitees + 5 + n_slots; i++)
                        {
                            string invitee = commandParams[i];
                            invitees.Add(invitee);
                        }
                    }
                    this.CreateProposal(meetingTopic, min_attendees, n_slots, n_invitees, meeting_slots, invitees);
                    break;
                case "join":
                    String topic = commandParams[1];
                    int slot_count = Int32.Parse(commandParams[2]);
                    List<String> slots = new List<String>();
                    for (int i = 3; i < slot_count + 3; i++)
                    {
                        string slot = commandParams[i];
                        slots.Add(slot);
                    }
                    this.JoinMeeting(topic, slots);
                    break;
                case "close":
                    String meeting_topic = commandParams[1];
                    this.CloseMeeting(meeting_topic);
                    break;
                case "wait":
                    int interval = Int32.Parse(commandParams[1]);
                    Thread.Sleep(interval);
                    break;
                default:
                    Console.WriteLine("What are you doing noob\r\n");
                    break;
            }
        }

        public void CloseMeeting(String topic)
        {
            throw new NotImplementedException();
        }

        public void CreateProposal(String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
        {
            Server.CreateProposal(this.UserName, topic, min_attendees, n_slots, n_invitees, slots, invitees);
        }

        public void JoinMeeting(String topic, List<String> slots)
        {

            Server.JoinMeeting(topic, this.UserName, slots);
        }

        public void ListMeetings()
        {
            Server.ListMeetings();
        }

        public void Connect(String server_URL)
        {
            Server = (ServerInterface)Activator.GetObject(
                typeof(ServerInterface),
                server_URL);
            Console.WriteLine("Registei o servidor");
        }

        public void PrintAllMeetings(string meetings)
        {
            Console.WriteLine(meetings);
        }
    }
}
