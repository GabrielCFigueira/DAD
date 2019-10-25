using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
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

            TcpChannel channel = new TcpChannel(clientUri.Port);
            ChannelServices.RegisterChannel(channel, false);

            ClientImpl MeetingClient = new ClientImpl("Ze");
            RemotingServices.Marshal(MeetingClient, "MeetingClient", typeof(ClientImpl));
            ServerInterface server = (ServerInterface)Activator.GetObject(
                typeof(ServerInterface),
                serverUrl);
            server.Connect(clientUrl + "/MeetingClient");

            string command = "";
            StreamReader file = new StreamReader(scriptFileName);
            while ((command = file.ReadLine()) != null)
            {
                string[] commandParams = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                switch (commandParams[0])
                {
                    case "list":
                        MeetingClient.ListMeetings();
                        break;
                    case "create":
                        //TODO
                        break;
                    case "join":
                        //TODO
                        break;
                    case "close":
                        //TODO
                        break;
                    case "wait":
                        //TODO
                        break;
                    default:
                        Console.WriteLine("What are you doing noob\r\n");
                        break;
                }
            }

            file.Close();

            /*List<String> date_location = new List<string>();
            date_location.Add("Lisboa,2019-11-14");
            date_location.Add("Porto,2020-02-03");

            List<String> invitees = new List<string>();
            invitees.Add("Maria");
            invitees.Add("Johny");
            invitees.Add("Tiago");

            MeetingClient.CreateMeeting("Budget_2020", 5, 2, 3, date_location, invitees);

            /*List<String> date_location2 = new List<string>();
            date_location2.Add("Setubal,2019-11-14");
            date_location2.Add("Braga,2020-02-03");

            List<String> invitees2 = new List<string>();
            invitees2.Add("Tatiana");
            invitees2.Add("Laura");
            invitees2.Add("Daniel");

            MeetingClient.CreateMeeting("Pokemon_GO", 5, 2, 3, date_location2, invitees2);

            MeetingClient.ListMeetings();
            List<String> slots = new List<String>();
            slots.Add("Lisboa,2019-11-14");
            slots.Add("Porto,2020-02-03");
            MeetingClient.JoinMeeting("Budget_2020", slots);

            MeetingClient.ListMeetings();*/

            System.Console.ReadLine();

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

        public void CloseMeeting(String topic)
        {
            throw new NotImplementedException();
        }

        public void CreateProposal(String topic, int min_attendees, int n_slots, int n_invitees, List<Slot> slots, List<String> invitees)
        {
            Server.CreateProposal(this.UserName, topic, min_attendees, n_slots, n_invitees, slots, invitees);
        }

        public void JoinMeeting(String topic, List<Slot> slots)
        {

            Server.JoinMeeting(topic, this.UserName, slots);
        }

        public void ListMeetings()
        {
            Server.ListMeetings();
        }

        public void Connect(string URL)
        {
            Server = (ServerInterface)Activator.GetObject(
                typeof(ServerInterface),
                "tcp://localhost:8888/MeetingServer");
            Console.WriteLine("Registei o servidor");
        }

        public void PrintAllMeetings(string meetings)
        {
            Console.WriteLine(meetings);
        }
    }
}
