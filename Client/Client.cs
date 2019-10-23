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
    class Client
    {

        static void Main(string[] args)
        {
            TcpChannel channel = new TcpChannel(8000);
            ChannelServices.RegisterChannel(channel, false);

            ClientImpl MeetingClient = new ClientImpl("Ze");
            RemotingServices.Marshal(MeetingClient, "MeetingClient", typeof(ClientImpl));
            ServerInterface server = (ServerInterface)Activator.GetObject(
                typeof(ServerInterface),
                "tcp://localhost:8888/MeetingServer");
            server.Connect("tcp://localhost:" + 8000 + "/MeetingClient");

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

        public void CreateMeeting(String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
        {
            throw new NotImplementedException();
        }

        public void JoinMeeting(String topic)
        {
            throw new NotImplementedException();
        }

        public void ListMeetings()
        {
            throw new NotImplementedException();
        }

        public void Connect(string URL)
        {
            Server = (ServerInterface)Activator.GetObject(
                typeof(ServerInterface),
                "tcp://localhost:8888/MeetingServer");
            Console.WriteLine("Registei o servidor");
        }
    }
}
