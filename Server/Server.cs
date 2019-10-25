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
        List<ClientInterface> Clients;
        List<Proposal> Proposals;
        List<Meeting> Meetings;

        public ServerImpl()
        {
            this.Meetings = new List<Meeting>();
            this.Proposals = new List<Proposal>();
            this.Clients = new List<ClientInterface>();
        }

        public void CloseMeeting(String topic)
        {
            throw new NotImplementedException();
        }

        public void CreateProposal(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
        {
            //TODO construir Location e Local_Date
            Proposal m = new Proposal(coordinator, topic, min_attendees, n_slots, n_invitees, slots, invitees);
            Proposals.Add(m);
        }

        public void JoinMeeting(String topic,String userName, List<String> slots)
        {
            foreach(Meeting m in this.Meetings)
            {
                if(m.Topic == topic)
                {
                    Attendee a = new Attendee(userName, slots);
                    m.Attendees.Add(a);
                }
            }
        }

        public void ListMeetings()
        {
            //TODO tirar duvidas com o prof
            String message = "OPEN MEETINGS\r\n\r\n";
            foreach (Proposal m in Proposals)
            {
                message += "Coordinator: " + m.Coordinator + "\r\nTopic: " + m.Topic + "\r\nMin_attendees: " + m.Min_attendees + "\r\nN_slots: " + m.N_slots + " \r\nN_invitees: " + m.N_invitees + "\r\nSlots: ";
                foreach(String s in m.Slots)
                {
                    message += s + " ";
                }
                message += "\r\nInvitees: ";
                foreach (String s in m.Invitees)
                {
                    message += s + " ";
                }
                message += "\r\nAttendees: ";
                foreach(Attendee a in m.Attendees)
                {
                    message += a.Name + ", Available Slots: ";
                    foreach(String s in a.Available_slots)
                    {
                        message += s + " ";
                    }
                }
                message += "\r\n\r\n\r\nCLOSED MEETINGS\r\n\r\n";
            }

            foreach (Meeting m in Meetings)
            {
                message += "Coordinator: " + m.Coordinator + "\r\nTopic: " + m.Topic + "\r\nMin_attendees: " + m.Min_attendees + "\r\nN_slots: " + m.N_slots + " \r\nN_invitees: " + m.N_invitees + "\r\nLocal: " + m.Local;
                message += "\r\nInvitees: ";
                foreach (String s in m.Invitees)
                {
                    message += s + " ";
                }
                message += "\r\nState: ";
                if (m.State == 0)
                    message += "SCHEDULED\r\n";
                if (m.State == 1)
                    message += "CANCELLED\r\n";
                message += "\r\nAttendees: ";
                foreach (Attendee a in m.Attendees)
                {
                    message += a.Name + ", Available Slots: ";
                    foreach (String s in a.Available_slots)
                    {
                        message += s + " ";
                    }
                }
                message += "\r\n\r\n\r\n";
            }

            foreach (ClientInterface c in Clients)
            {
                c.PrintAllMeetings(message);
            }
        }

        public void Connect(string URL)
        {
            ClientInterface c = (ClientInterface)Activator.GetObject(
                 typeof(ClientInterface),
                 URL);
            c.Connect("tcp://localhost:8888/MeetingServer");
            Clients.Add(c);
            Console.WriteLine("Registei o cliente");

        }
    }

}
