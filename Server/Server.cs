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
            string id = args[1];
            String url = args[2];
            int maxFaults = Int32.Parse(args[3]);
            int minDelay = Int32.Parse(args[4]);
            int maxDelay = Int32.Parse(args[5]);

            String puppetURL = args[6]; // O server precisa de ter o url do puppet (Martelado)
            String masterServer = args[7]; // (Martelado)

            Uri uri = new Uri(url);
            TcpChannel channel = new TcpChannel(uri.Port);
            ChannelServices.RegisterChannel(channel, false);

            ServerImpl MeetingServer = new ServerImpl(id,url,maxFaults,minDelay,maxDelay, puppetURL, masterServer);
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
        Dictionary<String, int> Servers;
        Dictionary<String, Command> Closes;
        Dictionary<int, Ticket> Tickets;

        string id;
        String url;
        int maxFaults;
        int minDelay;
        int maxDelay;

        String puppetURL;
        String masterServer;
        Int32 ticket = 0;
        Int32 lastTicket = 0;

        [Serializable]
        private class DoCreate : Command
        {
            string coordinator;
            int min_attendees;
            int n_slots;
            int n_invitees;
            List<String> slots;
            List<String> invitees;

            public DoCreate(string coordinator, string topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
                : base(topic)
            {
                this.coordinator = coordinator;
                this.min_attendees = min_attendees;
                this.n_slots = n_slots;
                this.n_invitees = n_invitees;
                this.slots = slots;
                this.invitees = invitees;
            }

            override public AbstractMeeting Execute(ServerInterface si)
            {
                ServerImpl server = (ServerImpl)si;
                lock (server.Proposals)
                {
                    lock (server.Clients)
                    {
                        Dictionary<String, Slot> Slots = new Dictionary<String, Slot>();
                        foreach (String s in slots)
                        {
                            string[] zone_date = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries); //zone_date[0] e um local, zone_date[1] e uma data

                            Location l = server.Meetings[zone_date[0]].Location;
                            Slot slot = new Slot(l, zone_date[1]);
                            Slots.Add(s, slot);
                        }
                        Proposal p = new Proposal(coordinator, this.Topic, min_attendees, n_slots, n_invitees, Slots, invitees);
                        server.Proposals.Add(p.Topic, p);

                        return p;
                    }
                }
            }
        }

        [Serializable]
        private class DoJoin : Command
        {
            string userName;
            List<string> slots;
            public DoJoin(string topic, string userName, List<string> slots)
                : base(topic)
            {
                this.userName = userName;
                this.slots = slots;
            }

            override public AbstractMeeting Execute(ServerInterface si)
            {
                ServerImpl server = (ServerImpl)si;
                lock (server.Proposals)
                {
                    Proposal p = null;

                    if (!server.Proposals.TryGetValue(this.Topic, out p))
                    {
                        Console.WriteLine("O cliente " + userName + " fez join a uma Meeting nao existente");
                        return p;
                    }

                    List<Slot> Slots = new List<Slot>();
                    if ((p.N_invitees != 0 && p.Invitees.Contains(userName)) || p.N_invitees == 0 || p.Coordinator == userName)
                    {
                        foreach (String s in slots)
                        {
                            string[] zone_date = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries); //zone_date[0] e um local, zone_date[1] e uma data

                            Location l = server.Meetings[zone_date[0]].Location;
                            Slot slot = new Slot(l, zone_date[1]);
                            p.Slots[s].Votes += 1;
                            slot.Votes = p.Slots[s].Votes;
                            Slots.Add(slot);
                        }

                        Attendee a = new Attendee(userName, Slots);
                        p.Version += 1;
                        p.Attendees.Add(a);
                    }
                    else
                    {
                        Console.WriteLine("Sou o/a " + userName + " e estou a dar join a um meeting onde nao estou convidado/a");
                    }

                    return p;

                }
            }
        }

        [Serializable]
        private class DoClose : Command
        {
            string userName;

            public DoClose(String userName, String topic)
                : base(topic)
            {
                this.userName = userName;
            }

            override public AbstractMeeting Execute(ServerInterface si)
            {
                ServerImpl server = (ServerImpl)si;
                lock (server.Proposals)
                {
                    lock (server.Meetings)
                    {
                        Proposal p = server.Proposals[this.Topic];
                        Slot chosenSlot = null;
                        Room selectedRoom = null;
                        double efficiency = 0;
                        if (p.Coordinator == userName)
                        {
                            foreach (Slot s in p.Slots.Values)
                            {
                                List<Meeting> meetings = server.Meetings[s.Location.Local].Meetings;
                                if (meetings.Count != 0)
                                {
                                    foreach (Meeting m in meetings)
                                    {
                                        foreach (Room r in s.Location.Rooms)
                                        {
                                            if ((m.SelectedRoom.Name != r.Name || m.Slot.Date != s.Date) && Math.Min(s.Votes, r.Capacity) >= p.Min_attendees)
                                            {
                                                double tempEfficiency = (double)s.Votes / r.Capacity;
                                                if (chosenSlot == null
                                                || Math.Min(chosenSlot.Votes, selectedRoom.Capacity) < Math.Min(s.Votes, r.Capacity)
                                                || (Math.Min(chosenSlot.Votes, selectedRoom.Capacity) == Math.Min(s.Votes, r.Capacity)
                                                && tempEfficiency > efficiency))
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
                                        if (Math.Min(s.Votes, r.Capacity) >= p.Min_attendees)
                                        {
                                            double tempEfficiency = (double)s.Votes / r.Capacity;
                                            if (chosenSlot == null
                                                || Math.Min(chosenSlot.Votes, selectedRoom.Capacity) < Math.Min(s.Votes, r.Capacity)
                                                || (Math.Min(chosenSlot.Votes, selectedRoom.Capacity) == Math.Min(s.Votes, r.Capacity)
                                                && tempEfficiency > efficiency))
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
                                return p;
                            }
                            else
                            { 
                                List<Attendee> attendees = new List<Attendee>();
                                foreach(Attendee at in p.Attendees)
                                {
                                    foreach(Slot s in at.Available_slots)
                                    {
                                        if(s.Date == chosenSlot.Date && s.Location.Local == chosenSlot.Location.Local)
                                        {
                                            attendees.Add(at);
                                            break;
                                        }
                                    }
                                }

                                while (attendees.Count > selectedRoom.Capacity)
                                {
                                    attendees.RemoveAt(attendees.Count - 1);
                                }

                                Meeting meeting = new Meeting(p.Coordinator, p.Topic, p.Min_attendees, attendees.Count, chosenSlot, p.Invitees, p.Version + 1, selectedRoom, attendees);
                                server.Meetings[chosenSlot.Location.Local].addMeeting(meeting);
                                server.Proposals.Remove(p.Topic);

                                return meeting;
                            }
                        }
                        else
                        {
                            throw new Exception(); //coordinator must be the one closing
                        }

                    }
                }
            }
        }


        public ServerImpl(string id, String url, int maxFaults, int minDelay, int maxDelay, String puppetURL, string masterServer)
        {
            this.id = id;
            this.url = url;
            this.maxFaults = maxFaults;
            this.minDelay = minDelay;
            this.maxDelay = maxDelay;
            this.Proposals = new Dictionary<String, Proposal>();
            this.Clients = new Dictionary<String, ClientInterface>();
            this.Meetings = new Dictionary<string, LocationMeetings>();
            this.Servers = new Dictionary<string, int>();
            this.Servers.Add(url, 0);
            this.Closes = new Dictionary<string, Command>();
            this.Tickets = new Dictionary<int, Ticket>();

            this.puppetURL = puppetURL;
            this.masterServer = masterServer;
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        public void waitBetweenRequests()
        {
            Random timeout = new Random();
            int timeSleeping = timeout.Next(this.minDelay, this.maxDelay);
            Thread.Sleep(timeSleeping);
        }

        public void CloseMeeting(String userName, String topic)
        {
            this.waitBetweenRequests();
            lock (this.Servers)
            {
                lock (this.Tickets)
                {
                    lock (this.Closes)
                    {
                        Command command = new DoClose(userName, topic);
                        ExecuteTicket(command);
                    }
                }
            }
            
        }

        public void CreateProposal(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
        {
            this.waitBetweenRequests();
           
            Command command = new DoCreate(coordinator, topic, min_attendees, n_slots, n_invitees, slots, invitees);
            Proposal p;
            lock (this.Servers)
            {
                p = (Proposal)command.Execute(this);
                this.Servers[this.url]++;
            }
            UpdateServers(command);
            if (n_invitees > 0)
            {
                foreach (String s in invitees)
                {
                    ClientInterface c = this.Clients[s];
                    c.AddProposal(p);
                }
                try
                { //in case the coordinator invites himself
                    this.Clients[coordinator].AddProposal(p);
                } catch (ArgumentException e)
                {
                    Console.WriteLine("O utilizador " + coordinator + " convidou-se a si mesmo");
                }
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

        public void JoinMeeting(String topic, String userName, List<String> slots)
        {
            this.waitBetweenRequests();
            if(!this.Proposals.ContainsKey(topic)) //FIXME pending create?
            {
                return; //FIXME failure
            }
            Command command = new DoJoin(topic, userName, slots);
            lock (this.Servers)
            {
                command.Execute(this);
                this.Servers[this.url]++;
            }
            UpdateServers(command);

        }

        public void ListMeetings(String userName)
        {
            this.waitBetweenRequests();
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
            this.UpdateServersClients(client_URL, userName);
            Console.WriteLine("Registei o/a cliente " + userName);

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

        public void PropagateClose(Command command)
        {
            Thread[] pool = new Thread[this.Servers.Count - 1];
            int i = 0;
            foreach (KeyValuePair<String, int> entry in this.Servers)
            {
                if (this.url != entry.Key)
                {
                    string url = entry.Key;
                    pool[i] = new Thread(() => DoPropagate(url, command));
                    pool[i].Start();
                    i++;
                }
            }
        }

        private void DoPropagate(string url, Command command)
        {
            ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
            si.UpdateClose(command, this.url, this.Servers);
        }

        public void UpdateServers(Command command)
        {
            lock (this.Servers)
            {
                Thread[] pool = new Thread[this.Servers.Count - 1];
                int i = 0;
                foreach(KeyValuePair<String, int> entry in this.Servers)
                {
                    if (this.url != entry.Key)
                    {
                        string url = entry.Key;
                        pool[i] = new Thread(() => DoUpdate(url, command));
                        pool[i].Start();
                        i++;
                    }
                }
            }
        }

        private void DoUpdate(string url, Command command)
        {
            ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
            si.UpdateMeeting(command, this.url, this.Servers);
        }

        public void UpdateServersClients(String clientUrl, String userName)
        {
            lock (this.Servers)
            {

                Thread[] pool = new Thread[this.Servers.Count - 1];
                int i = 0;
                foreach (KeyValuePair<String, int> entry in this.Servers)
                {
                    if(entry.Key != this.url)
                    {
                        string url = entry.Key;
                        pool[i] = new Thread(() => DoUpdateClient(url, clientUrl, userName));
                        pool[i].Start();
                        i++;
                    }
                }
            }
        }

        private void DoUpdateClient(string serverUrl, string clientUrl, string userName)
        {
            ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), serverUrl);
            Console.WriteLine("Sou o servidor e vou fazer update com o user " + userName);
            si.UpdateClient(clientUrl, userName);
        }

        public void UpdateClient(String client_url,string userName)
        {
            lock (this.Clients)
            {
                ClientInterface ci = (ClientInterface)Activator.GetObject(typeof(ClientInterface), client_url);
                if (!this.Clients.ContainsKey(userName))
                {
                    Console.WriteLine("Passei a conhecer o user " + userName);
                    this.Clients[userName] = ci;
                }
            }
        }

        public void UpdateMeeting(Command command, string serverURL, Dictionary<string, int> vectorClock)
        {
            lock(this.Servers)
            {
                Console.WriteLine("Server's own clock");
                printClock(this.Servers);
                Console.WriteLine("Received Clock");
                printClock(vectorClock);
                while(!checkClock(serverURL, vectorClock))
                {
                    Monitor.Wait(this.Servers);
                }

                this.Servers[serverURL]++;
                command.Execute(this);
                Monitor.PulseAll(this.Servers);

            }
            
        }

        public void UpdateClose(Command command, string serverURL, Dictionary<string, int> vectorClock)
        {
            lock (this.Servers)
            {
                Console.WriteLine("Server's own clock");
                printClock(this.Servers);
                Console.WriteLine("Received Clock");
                printClock(vectorClock);
                while (!checkClock(serverURL, vectorClock))
                {
                    Monitor.Wait(this.Servers);
                }

                this.Servers[serverURL]++;

                lock (this.Closes)
                {
                    this.Closes.Add(command.Topic, command);
                }
                Monitor.PulseAll(this.Servers);
            }
        }

        private bool checkClock(string serverURL, Dictionary<string, int> vectorClock)
        {
            
            foreach (KeyValuePair<string, int> entry in this.Servers)
            {
                if (serverURL != entry.Key && entry.Value < vectorClock[entry.Key])
                {
                    return false;
                }
                else if (serverURL == entry.Key && entry.Value < vectorClock[entry.Key] - 1)
                {
                    return false;
                }
            }
            return true;
        }

        private void printClock(Dictionary<string, int> vectorClock)
        {
            foreach (KeyValuePair<string, int> entry in vectorClock)
            {
                Console.WriteLine("Server: " + entry.Key + " Clock: " + entry.Value);
            }
        }

        public void AddServer(String serverURL)
        {
            lock (this.Servers)
            {
                this.Servers.Add(serverURL, 0);
            }
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
            Console.WriteLine("\n-----STATUS-----\n");
            Console.WriteLine("I'm ALIVE!");
            Console.WriteLine("Server id: " + id + " Server url: " + url);
            //Console.WriteLine("Maximum Faults: " + maxFaults);
            //Console.WriteLine("Maximum Delay: " +  maxDelay);
            //Console.WriteLine("Minimum Delay: " +  minDelay);
            
            Console.WriteLine("Servers that are alive: ");
            if (Servers.Count != 0) 
            {
                foreach (KeyValuePair<String, int> entry in this.Servers)
                {
                    Console.WriteLine("Server: " + entry.Key);
                }
            } else { Console.WriteLine("No Servers Available"); }

            Console.WriteLine("Clients:");
            if (Clients.Keys.Count != 0)
            {
                foreach (string client in Clients.Keys)
                {
                    Console.WriteLine("\t" + client);
                }
            } else { Console.WriteLine("\t No Clients Available"); }

            Console.WriteLine("Proposals: ");
            if (Proposals.Keys.Count != 0)
            {
                foreach (string s in Proposals.Keys)
                {
                    Console.WriteLine("\t" + s);
                    Console.WriteLine("\t\t\t " + Proposals[s].PrintInfo());
                }
            } else { Console.WriteLine("\t No Proposals Available"); }
          

            Console.WriteLine("Locations and Meetings: ");
            if (Meetings.Keys.Count != 0)
            {
                foreach (string location in Meetings.Keys)
                {
                    Console.WriteLine("\t" + location);
                    foreach (Room room in Meetings[location].Location.Rooms)
                    {
                        Console.WriteLine("\t\t" + room.ToString());
                    }
                    Console.WriteLine("\t\t" + Meetings[location].ToString());
                }
            } else { Console.WriteLine("\t No Locations Available"); }

        }

        public void Crash()
        {
            shutdown();
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


        public int GetTicket(string topic, string serverURL, Dictionary<string, int> vectorClock)
        {
            lock (this.Servers)
            {
                lock (this.Tickets)
                {

                    Console.WriteLine("Server's own clock");
                    printClock(this.Servers);
                    Console.WriteLine("Received Clock");
                    printClock(vectorClock);
                    while (!checkClock(serverURL, vectorClock))
                    {
                        Monitor.Wait(this.Servers);
                    }

                    ticket++;
                    this.Tickets.Add(ticket, new Ticket(ticket, serverURL, this.Closes[topic]));
                    Monitor.PulseAll(this.Servers);
                    return ticket;
                }
            }
        }

        private void PropagateTicket(int ticket, AbstractMeeting am)
        {
            lock (this.Servers)
            {
                Thread[] pool = new Thread[this.Servers.Count - 1];
                int i = 0;
                foreach (KeyValuePair<String, int> entry in this.Servers)
                {
                    if (this.url != entry.Key)
                    {
                        string url = entry.Key;
                        pool[i] = new Thread(() => DoPropagateTicket(url, ticket, am));
                        pool[i].Start();
                        i++;
                    }
                }
            }
        }

        private void DoPropagateTicket(string url, int ticket, AbstractMeeting am)
        {
            ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
            si.ReceiveTicketResult(am.Topic, this.url, ticket, am, this.Servers);
        }

        public void ReceiveTicketResult(string topic, string serverURL, int ticket, AbstractMeeting am, Dictionary<string, int> vectorClock)
        {
            List<Command> pendingJoins = new List<Command>();
            lock (this.Servers)
            {
                Console.WriteLine("Server's own clock");
                printClock(this.Servers);
                Console.WriteLine("Received Clock");
                printClock(vectorClock);
                while (!checkClock(serverURL, vectorClock))
                {
                    Monitor.Wait(this.Servers);
                }

                this.Servers[serverURL]++;


                lock (this.Tickets)
                {
                    while (ticket - 1 != lastTicket)
                    {
                        Monitor.Wait(this.Tickets);
                    }
                    Monitor.PulseAll(this.Tickets);
                }
                lock (this.Proposals)
                {
                    lock (this.Meetings)
                    {
                        lock (this.Tickets)
                        {
                            lock (this.Closes)
                            {
                                if (am.isProposal())
                                {
                                    this.Proposals[topic] = (Proposal)am;
                                }
                                else
                                {
                                    Meeting m = (Meeting)am;
                                    this.Meetings[m.Slot.Location.Local].addMeeting(m);
                                    Proposal p = this.Proposals[topic];
                                    this.Proposals.Remove(topic);

                                    foreach (Attendee a in p.Attendees)
                                    {
                                        bool notIn = true;
                                        foreach (Attendee attendee in m.Attendees)
                                        {
                                            if (attendee.Name == a.Name)
                                            {
                                                notIn = false;
                                            }
                                        }
                                        if (notIn && CouldAttend(m, a))
                                        {
                                            List<string> slots = new List<string>();
                                            foreach (Slot s in a.Available_slots)
                                            {
                                                slots.Add(s.Location + "," + s.Date);
                                            }
                                            Command command = new DoJoin(topic + a.Name, a.Name, slots);
                                            pendingJoins.Add(command);
                                        }

                                    }
                                }
                                if (this.masterServer != this.url)
                                {
                                    this.Tickets.Add(ticket, new Ticket(ticket, serverURL, this.Closes[topic]));
                                }
                                this.Closes.Remove(topic);
                                lastTicket++;
                                foreach (Command command in pendingJoins)
                                {
                                    ExecuteTicket(command);
                                }
                            }
                        }
                    }
                }
                Monitor.PulseAll(this.Servers);

            }
        }

        private Boolean CouldAttend(Meeting m, Attendee user)
        {
            if(m.N_invitees == m.SelectedRoom.Capacity)
            {
                return false;
            }
            Slot chosenSlot = m.Slot;
            foreach (Slot s in user.Available_slots)
            {
                if ((s.Date == chosenSlot.Date && s.Location.Local == chosenSlot.Location.Local))
                {
                    return true;
                }
            }
            return false;
        }

        private void ExecuteTicket(Command command)
        {
            string topic = command.Topic;
            
            this.Closes.Add(topic, command);
            this.Servers[this.url]++;
            PropagateClose(command);

            int newTicket;
            if (masterServer == this.url)
            {
                newTicket = GetTicket(topic, this.url, this.Servers);
            }
            else
            {
                ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), masterServer);
                newTicket = si.GetTicket(topic, this.url, this.Servers);

                this.Tickets.Add(newTicket, new Ticket(newTicket, this.url, this.Closes[topic]));
            }

            
            while (newTicket - 1 != lastTicket)
            {
                Monitor.Wait(this.Tickets);
            }
            

            
            AbstractMeeting am = command.Execute(this);
            this.Servers[this.url]++;
            lastTicket++;
            this.Closes.Remove(topic);
                   
            PropagateTicket(newTicket, am);

            Monitor.PulseAll(this.Tickets);
        }


    }

}

