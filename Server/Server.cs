using Puppet_Server;
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

            MeetingServer.InitializeLocationsAndRooms();

            Console.ReadLine();
        }
    }

    class ServerImpl : MarshalByRefObject, ServerInterface, IPS
    {
        Dictionary<String,ClientInterface> Clients;
        Dictionary<String,Proposal> Proposals;
        Dictionary<Location,List<Meeting>> Meetings;
        int id;
        String url;
        int maxFaults;
        int minDelay;
        int maxDelay;

        public ServerImpl(int id, String url, int maxFaults, int minDelay, int maxDelay)
        {
            this.id = id;
            this.url = url;
            this.maxFaults = maxFaults;
            this.minDelay = minDelay;
            this.maxDelay = maxDelay;
            this.Meetings = new Dictionary<Location, List<Meeting>>();
            this.Proposals = new Dictionary<string, Proposal>();
            this.Clients = new Dictionary<String, ClientInterface>();
        }

        public void CloseMeeting(String topic)
        {
            throw new NotImplementedException();
        }

        public void CreateProposal(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
        {
            List<Slot> Slots = new List<Slot>();
            foreach(String s in slots)
            {
                string[] zone_date = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries); //zone_date[0] e um local, zone_date[1] e uma data
                foreach(Location l in Meetings.Keys)
                {
                    if(l.Local == zone_date[0])
                    {
                        Slot slot = new Slot(l, zone_date[1]);
                        Slots.Add(slot);
                    }
                }             
            }
            Proposal p = new Proposal(coordinator, topic, min_attendees, n_slots, n_invitees, Slots, invitees);
            Proposals.Add(p.Topic, p);
            foreach(KeyValuePair<String, ClientInterface> entry in Clients)
            {
                //Deve ser verificado se o user esta convidado ou nao
                ClientInterface c = entry.Value;
                c.AddProposal(p);
            }
        }

        public void JoinMeeting(String topic,String userName, List<String> slots)
        {
            List<Slot> Slots = new List<Slot>();
            foreach (String s in slots)
            {
                string[] zone_date = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries); //zone_date[0] e um local, zone_date[1] e uma data
                foreach(Location l in Meetings.Keys)
                {
                    if(l.Local == zone_date[0])
                    {
                        Slot slot = new Slot(l, zone_date[1]);
                        Slots.Add(slot);
                    }
                }
            }

            Attendee a = new Attendee(userName, Slots);
            Proposal p = this.Proposals[topic];//check if it is null
            p.Version += 1;
            //this.Proposals.TryGetValue(topic, out p); //Test this
            p.Attendees.Add(a);

        }

        public void ListMeetings()
        {
            foreach (KeyValuePair<String, ClientInterface> entry in Clients)
            {
                ClientInterface c = entry.Value;
                c.UpdateMeetings(this.Proposals,this.Meetings);
            }
        }

        public void Connect(string client_URL, string userName)
        {
            ClientInterface c = (ClientInterface)Activator.GetObject(
                 typeof(ClientInterface),
                 client_URL);
            c.Connect(this.url);
            Clients.Add(userName,c);
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




        public void AddRoom(string location, int capacity, string room_name)
        {
            foreach (Location l in Meetings.Keys)
            {
                if (l.Local == location)
                {
                    Room room = new Room(room_name, capacity);
                    l.addRoom(room);
                }
            }
        }
        public void InitializeLocationsAndRooms()
        {
            StreamReader file = new StreamReader(@"..\..\..\Server\ServerConfig\Config.txt");
            String command = "";
            Boolean alreadyExists = false;
            while ((command = file.ReadLine()) != null)
            {
                string[] commandParams = command.Split(new char[] { ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
                String location_name = commandParams[0];
                int counter = Int32.Parse(commandParams[1]);
                foreach (Location loc in Meetings.Keys)
                {
                    if (loc.Local == location_name)
                    {
                        alreadyExists = true;
                        for (int i = 2; i < counter*2 + 2; i += 2)
                        {
                            Room room = new Room(commandParams[i], Int32.Parse(commandParams[i + 1]));
                            loc.addRoom(room);
                        }
                    }
                }
                if (!alreadyExists)
                {
                    List<Room> rooms = new List<Room>();
                    Location l = new Location(location_name, rooms);
                    for (int i = 2; i < counter * 2 + 2; i += 2)
                    {
                        Room room = new Room(commandParams[i], Int32.Parse(commandParams[i + 1]));
                        l.addRoom(room);
                    }
                    this.Meetings.Add(l, new List<Meeting>());
                }
            }
            file.Close();
        }
    }

}
