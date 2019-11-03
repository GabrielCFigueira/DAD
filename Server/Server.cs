using Puppet_Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Threading;

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

            String puppetURL = args[6]; // O server precisa de ter o url do puppet (Martelado)

            Uri uri = new Uri(url);
            TcpChannel channel = new TcpChannel(uri.Port);
            ChannelServices.RegisterChannel(channel, false);

            ServerImpl MeetingServer = new ServerImpl(id,url,maxFaults,minDelay,maxDelay, puppetURL);
            RemotingServices.Marshal(MeetingServer, uri.Segments[1], typeof(ServerImpl));

            MeetingServer.InitializeLocationsAndRooms();

            Console.ReadLine();
        }
    }

    class ServerImpl : MarshalByRefObject, ServerInterface, IServerPuppet
    {
        Dictionary<String, ClientInterface> Clients;
        Dictionary<String, Proposal> Proposals;
        Dictionary<String, LocationMeetings> Meetings;
        List<string> Servers;
        
        int id;
        String url;
        int maxFaults;
        int minDelay;
        int maxDelay;

        String puppetURL;

        public ServerImpl(int id, String url, int maxFaults, int minDelay, int maxDelay, String puppetURL)
        {
            this.id = id;
            this.url = url;
            this.maxFaults = maxFaults;
            this.minDelay = minDelay;
            this.maxDelay = maxDelay;
            this.Proposals = new Dictionary<String, Proposal>();
            this.Clients = new Dictionary<String, ClientInterface>();
            this.Meetings = new Dictionary<string, LocationMeetings>();
            this.Servers = new List<string>();

            this.puppetURL = puppetURL;
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        public void CloseMeeting(String userName, String topic)
        {
            lock (this.Proposals)
            {
                lock (this.Meetings)
                {
                    Proposal p = this.Proposals[topic];
                    Slot chosenSlot = null;
                    Room selectedRoom = null;
                    double efficiency = 0;
                    if (p.Coordinator == userName)
                    {
                        foreach (Slot s in p.Slots.Values)
                        {
                            List<Meeting> meetings = this.Meetings[s.Location.Local].Meetings;
                            if (meetings.Count != 0)
                            {
                                foreach (Meeting m in meetings)
                                {
                                    foreach (Room r in s.Location.Rooms)
                                    {
                                        if ((m.SelectedRoom != r || m.Slot.Date != s.Date) && s.Votes <= r.Capacity && s.Votes >= p.Min_attendees)
                                        {
                                            double tempEfficiency = (double)s.Votes / r.Capacity;
                                            if ((chosenSlot == null || (chosenSlot != null && chosenSlot.Votes <= s.Votes)) && tempEfficiency > efficiency)
                                            {
                                                chosenSlot = s;
                                                selectedRoom = r;
                                                efficiency = tempEfficiency;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                foreach (Room r in s.Location.Rooms)
                                {
                                    if (s.Votes <= r.Capacity && s.Votes >= p.Min_attendees)
                                    {
                                        double tempEfficiency = (double)s.Votes / r.Capacity;
                                        if ((chosenSlot == null || (chosenSlot != null && chosenSlot.Votes <= s.Votes)) && tempEfficiency > efficiency)
                                        {
                                            chosenSlot = s;
                                            selectedRoom = r;
                                            efficiency = tempEfficiency;
                                        }
                                    }
                                }
                            }
                        }
                        if (chosenSlot == null)
                        {
                            p.IsCancelled = true;
                            p.Version += 1;
                            UpdateServers(p);
                            return;
                        }
                        Meeting meeting = new Meeting(p.Coordinator, p.Topic, p.Min_attendees, p.N_invitees, chosenSlot, p.Invitees, p.Version + 1, selectedRoom, p.Attendees);
                        this.Meetings[chosenSlot.Location.Local].addMeeting(meeting);
                        this.Proposals.Remove(p.Topic);
                        UpdateServers(meeting);
                    }
                }
            }
        }

        public void CreateProposal(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
        {
            lock (this.Proposals)
            {
                lock (this.Clients)
                {
                    Dictionary<String, Slot> Slots = new Dictionary<String, Slot>();
                    foreach (String s in slots)
                    {
                        string[] zone_date = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries); //zone_date[0] e um local, zone_date[1] e uma data

                        Location l = Meetings[zone_date[0]].Location;
                        Slot slot = new Slot(l, zone_date[1]);
                        Slots.Add(s, slot);
                    }
                    Proposal p = new Proposal(coordinator, topic, min_attendees, n_slots, n_invitees, Slots, invitees);
                    Proposals.Add(p.Topic, p);
                    UpdateServers(p);
                    if (n_invitees > 0)
                    {
                        foreach (String s in invitees)
                        {
                            ClientInterface c = this.Clients[s];
                            c.AddProposal(p);
                        }
                        this.Clients[coordinator].AddProposal(p);
                    }
                    else if (n_invitees == 0)
                    {
                        foreach (KeyValuePair<String, ClientInterface> entry in Clients)
                        {
                            ClientInterface c = entry.Value;
                            c.AddProposal(p);
                        }
                    }
                }
            }
        }

        public void JoinMeeting(String topic, String userName, List<String> slots)
        {
            lock (this.Proposals)
            {
                Proposal p = this.Proposals[topic];
                List<Slot> Slots = new List<Slot>();
                if ((p.N_invitees != 0 && p.Invitees.Contains(userName)) || p.N_invitees == 0 || p.Coordinator == userName)
                {
                    foreach (String s in slots)
                    {
                        string[] zone_date = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries); //zone_date[0] e um local, zone_date[1] e uma data

                        Location l = Meetings[zone_date[0]].Location;
                        Slot slot = new Slot(l, zone_date[1]);
                        p.Slots[s].Votes += 1;
                        slot.Votes = p.Slots[s].Votes;
                        Slots.Add(slot);

                    }

                    Attendee a = new Attendee(userName, Slots);
                    p.Version += 1;
                    p.Attendees.Add(a);
                    UpdateServers(p);
                }
                else
                {
                    Console.WriteLine("Sou o/a " + userName + " e estou a dar join a um meeting onde nao estou convidado/a");
                }
            }

        }

        public void ListMeetings(String userName)
        {
            lock (this.Proposals)
            {
                lock (this.Meetings)
                {
                    ClientInterface c = this.Clients[userName];
                    c.UpdateMeetings(this.Proposals, this.Meetings);
                }
            }
        }

        public void Connect(string client_URL, string userName)
        {
            ClientInterface c = (ClientInterface)Activator.GetObject(
                 typeof(ClientInterface),
                 client_URL);
            c.Connect(this.url);
            lock (this.Clients)
            {
                Clients.Add(userName, c);
            }
            Console.WriteLine("Registei o cliente");

        }

        public void InitializeLocationsAndRooms()
        {
            StreamReader file = new StreamReader(@"..\..\..\Server\ServerConfig\Config.txt");
            string command;

            while ((command = file.ReadLine()) != null)
            {
                string[] commandParams = command.Split(new char[] { ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
                string location_name = commandParams[0];
                int counter = Int32.Parse(commandParams[1]);


                if (Meetings.ContainsKey(location_name))
                {
                    Location l = Meetings[location_name].Location;
                    for (int i = 2; i < counter * 2 + 2; i += 2)
                    {
                        Room room = new Room(commandParams[i], Int32.Parse(commandParams[i + 1]));
                        l.addRoom(room);
                    }
                }
                else
                {
                    List<Room> rooms = new List<Room>();
                    Location l = new Location(location_name, rooms);
                    for (int i = 2; i < counter * 2 + 2; i += 2)
                    {
                        Room room = new Room(commandParams[i], Int32.Parse(commandParams[i + 1]));
                        l.addRoom(room);
                    }
                    this.Meetings.Add(location_name, new LocationMeetings(l));
                }
            }
            file.Close();
        }

        private void UpdateServers(AbstractMeeting absMeeting)
        {
            lock (this.Servers)
            {
                foreach (String url in this.Servers)
                {
                    ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
                    si.UpdateMeeting(absMeeting);
                }
            }
        } 

        public void UpdateMeeting(AbstractMeeting absMeeting)
        {
            lock (this.Proposals)
            {
                lock (this.Meetings)
                {
                    if (absMeeting.isProposal())
                    {
                        this.Proposals[absMeeting.Topic] = (Proposal)absMeeting;
                    }
                    else
                    {
                        Meeting m = (Meeting)absMeeting;
                        this.Proposals.Remove(absMeeting.Topic);
                        this.Meetings[m.Slot.Location.Local].addMeeting(m);
                    }
                }
            }
            
        }

        public void AddServer(String serverURL)
        {
            this.Servers.Add(serverURL);
        }


        public void AddRoom(string location, int capacity, string room_name)
        {
            lock (this.Meetings)
            {
                Location l = Meetings[location].Location;
                Room room = new Room(room_name, capacity);
                l.addRoom(room);
            }
        }


        public void Status()
        {
            //string status = "Ola";
            IPuppet puppet = (IPuppet)Activator.GetObject(typeof(IPuppet), puppetURL);
            Console.WriteLine("Mandei um status de ola");
            puppet.Status("OLA");
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

        public void Unfreeze(string server_id)
        {
            throw new NotImplementedException();
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

