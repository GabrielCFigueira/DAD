using Puppet_Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Project
{
    class Server
    {
        static void Main(string[] args)
        {
            int id = Int32.Parse(args[1]);
            String url = args[2];
            int maxFaults = Int32.Parse(args[3]);
            int minDelay = Int32.Parse(args[4]);
            int maxDelay = Int32.Parse(args[5]);

            Uri uri = new Uri(url);
            TcpChannel channel = new TcpChannel(uri.Port);
            ChannelServices.RegisterChannel(channel, false);

            ServerImpl MeetingServer = new ServerImpl(id,url,maxFaults,minDelay,maxDelay);
            RemotingServices.Marshal(MeetingServer, uri.Segments[1], typeof(ServerImpl));

            System.Console.ReadLine();
        }
    }

    class ServerImpl : MarshalByRefObject, ServerInterface, IPS
    {
        List<ClientInterface> Clients;
        Dictionary<String,Proposal> Proposals;
        Dictionary<Location,List<Meeting>> Meetings;
        int id;
        String url;
        int maxFaults;
        int minDelay;
        int maxDelay;
        List<Room> rooms = new List<Room>(); //just to test the functions. Clear this after

        public ServerImpl(int id, String url, int maxFaults, int minDelay, int maxDelay)
        {
            this.id = id;
            this.url = url;
            this.maxFaults = maxFaults;
            this.minDelay = minDelay;
            this.maxDelay = maxDelay;
            this.Meetings = new Dictionary<Location, List<Meeting>>();
            this.Proposals = new Dictionary<string, Proposal>();
            this.Clients = new List<ClientInterface>();

            //JUST TO TEST,CLEAN AFTER
            Room a = new Room("A", 20);
            Room b = new Room("B", 10);
            Room c = new Room("C", 30);
            this.rooms.Add(a);
            this.rooms.Add(b);
            this.rooms.Add(c);
        }

        public void CloseMeeting(String topic)
        {
            throw new NotImplementedException();
        }

        public void CreateProposal(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
        {
            List<Slot> Slots = new List<Slot>();
            //TODO construir Location e Local_Date
            foreach(String s in slots)
            {
                string[] zone_date = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries); //zone_date[0] e um local, zone_date[1] e uma data
                Location loc = new Location(zone_date[0], this.rooms);
                Slot slot = new Slot(loc,zone_date[1]);
                Slots.Add(slot);
            }
            Proposal m = new Proposal(coordinator, topic, min_attendees, n_slots, n_invitees, Slots, invitees);
            Proposals.Add(m.Topic, m);
        }

        public void JoinMeeting(String topic,String userName, List<String> slots)
        {
            List<Slot> Slots = new List<Slot>();
            foreach (String s in slots)
            {
                string[] zone_date = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries); //zone_date[0] e um local, zone_date[1] e uma data
                Location loc = new Location(zone_date[0], this.rooms);
                Slot slot = new Slot(loc, zone_date[1]);
                Slots.Add(slot);
            }

            Attendee a = new Attendee(userName, Slots);
            Proposal p;
            this.Proposals.TryGetValue(topic, out p); //Test this
            p.Attendees.Add(a);

        }

        public void ListMeetings()
        {
            //TODO tirar duvidas com o prof
            String message = "OPEN MEETINGS\r\n\r\n";
            foreach (KeyValuePair<String, Proposal> entry in Proposals)
            {
                Proposal p = entry.Value;
                message += "Coordinator: " + p.Coordinator + "\r\nTopic: " + p.Topic + "\r\nMin_attendees: " + p.Min_attendees + "\r\nN_slots: " + p.N_slots + " \r\nN_invitees: " + p.N_invitees + "\r\nSlots: ";
                foreach(Slot s in p.Slots)
                {
                    message += s.Location + "," + s.Date + " ";
                }
                message += "\r\nInvitees: ";
                foreach (String s in p.Invitees)
                {
                    message += s + " ";
                }
                message += "\r\nAttendees: ";
                foreach(Attendee a in p.Attendees)
                {
                    message += a.Name + ", Available Slots: ";
                    foreach(Slot s in a.Available_slots)
                    {
                        message += s.Location + "," + s.Date + " ";
                    }
                }
                message += "\r\n\r\n\r\nCLOSED MEETINGS\r\n\r\n";
            }

            foreach (KeyValuePair<Location, List<Meeting>> e in Meetings)
            {
                List<Meeting> meet_list = e.Value;
                foreach (Meeting m in meet_list)
                {
                    message += "Coordinator: " + m.Coordinator + "\r\nTopic: " + m.Topic + "\r\nMin_attendees: " + m.Min_attendees + "\r\nN_slots: " + m.N_slots + " \r\nN_invitees: " + m.N_invitees + "\r\nLocal: " + m.Slot.Location;
                    message += "\r\nInvitees: ";
                    foreach (String s in m.Invitees)
                    {
                        message += s + " ";
                    }
                    message += "\r\nState: ";
                    if (m.IsScheduled)
                        message += "SCHEDULED\r\n";
                    if (!m.IsScheduled)
                        message += "CANCELLED\r\n";
                    message += "\r\nAttendees: ";
                    foreach (Attendee a in m.Attendees)
                    {
                        message += a.Name + ", Available Slots: ";
                        foreach (Slot s in a.Available_slots)
                        {
                            message += s.Location + "," + s.Date + " ";
                        }
                    }
                    message += "\r\n\r\n\r\n";
                }
            }

            foreach (ClientInterface c in Clients)
            {
                c.PrintAllMeetings(message);
            }
        }

        public void Connect(string client_URL)
        {
            ClientInterface c = (ClientInterface)Activator.GetObject(
                 typeof(ClientInterface),
                 client_URL);
            c.Connect(this.url);
            Clients.Add(c);
            Console.WriteLine("Registei o cliente");

        }

        public void shutdown()
        {
            Thread thread = new Thread(new ThreadStart(localShutdown));
            thread.Start();
        }

        private void localShutdown()
        {
            Thread.Sleep(2000);
            Environment.Exit(0);
        }
    }

}
