using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading.Tasks;

namespace Project
{
    class Server
    {
        static void Main(string[] args)
        {
            TcpChannel channel = new TcpChannel(8888);
            ChannelServices.RegisterChannel(channel, false);

            ServerImpl MeetingServer = new ServerImpl();
            RemotingServices.Marshal(MeetingServer, "MeetingServer", typeof(ServerImpl));

            System.Console.ReadLine();
        }
    }

    class ServerImpl : MarshalByRefObject, ServerInterface
    {
        ClientInterface Client;

        public void CloseMeeting(String topic)
        {
            throw new NotImplementedException();
        }

        public void CreateMeeting(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
        {
            throw new NotImplementedException();
        }

        public void JoinMeeting(String topic)
        {
            throw new NotImplementedException();
        }

        public String ListMeetings()
        {
            throw new NotImplementedException();
        }

        public void Connect(string URL)
        {
            Client = (ClientInterface)Activator.GetObject(
                 typeof(ClientInterface),
                 URL);
            Client.Connect("tcp://localhost:8888/MeetingServer");
            Console.WriteLine("Registei o cliente");

        }
    }

    class Meeting
    {
        String Coordinator;
        String Topic;
        int Min_attendees;
        int N_slots;
        int N_invitees;
        List<String> Slots;
        List<String> Invitees;

        public Meeting(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
        {
            this.Coordinator = coordinator;
            this.Topic = topic;
            this.Min_attendees = min_attendees;
            this.N_slots = n_slots;
            this.N_invitees = n_invitees;
            this.Slots = slots;
            this.Invitees = invitees;
        }

    }

}
