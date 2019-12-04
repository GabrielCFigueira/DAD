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
        Dictionary<string, Ticket> Tickets;

        string id;
        string url;
        int maxFaults;
        int minDelay;
        int maxDelay;

        String puppetURL;
        String masterServer;
        Int32 ticket = 0;
        Int32 lastTicket = 0;

        //Reliable_BroadCast structures
        Dictionary<string, List<string>> acks; //Key: depends on command, values: list of servers
        List<string> received_commands;

        //Freeze and Unfreeze variable
        bool freeze = false;

        [Serializable]
        private class DoCreate : Command
        {
            string command_id;
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
                this.command_id = topic + "Create";  //id Create
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

            override public string getCommandId()
            {
                return this.command_id;
            }
        }

        [Serializable]
        private class DoJoin : Command
        {
            string command_id;
            string userName;
            List<string> slots;
            public DoJoin(string topic, string userName, List<string> slots)
                : base(topic)
            {
                this.userName = userName;
                this.slots = slots;
                this.command_id = topic +  userName;
            }

            override public AbstractMeeting Execute(ServerInterface si)
            {
                ServerImpl server = (ServerImpl)si;
                lock (server.Proposals)
                {
                    AbstractMeeting am = null;
                    if (!server.Proposals.ContainsKey(this.Topic)) //FIXME join antes do create
                    {
                        Console.WriteLine("O cliente " + userName + " fez join atrasado a uma Meeting ja fechada");
                        foreach (LocationMeetings lm in server.Meetings.Values)
                        {
                            foreach (Meeting m in lm.Meetings)
                            {
                                if (m.Topic == this.Topic)
                                {
                                    am = m;
                                    if (am.Attendees.Count == m.SelectedRoom.Capacity)
                                    {
                                        Console.WriteLine("Mas não há espaço suficiente");
                                        return am;
                                    }
                                        goto label;
                                }
                            }
                        }
                    }
                    else
                    {
                        am = server.Proposals[this.Topic];
                    }

                    label:
                    List<Slot> Slots = new List<Slot>();
                    if ((am.N_invitees != 0 && am.Invitees.Contains(userName)) || am.N_invitees == 0 || am.Coordinator == userName)
                    {
                        foreach (String s in slots)
                        {
                            string[] zone_date = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries); //zone_date[0] e um local, zone_date[1] e uma data
                            Location l = server.Meetings[zone_date[0]].Location;
                            Slot slot = new Slot(l, zone_date[1]);
                            if(am.isProposal())
                            {
                                ((Proposal) am).Slots[s].Votes += 1;
                            }
                            Slots.Add(slot);
                        }

                        Attendee a = new Attendee(userName, Slots);
                        if(!am.isProposal())
                        {
                            a.LateArrival = true;
                        }
                        am.Version += 1;
                        am.Attendees.Add(a);
                    }
                    else
                    {
                        Console.WriteLine("Sou o/a " + userName + " e estou a dar join a um meeting onde nao estou convidado/a");
                    }

                    return am;

                }
            }

            override public string getCommandId()
            {
                return this.command_id;
            }
        }

        [Serializable]
        private class DoClose : Command
        {
            string command_id;
            string userName;

            public DoClose(String userName, String topic)
                : base(topic)
            {
                this.userName = userName;
                this.command_id = topic;
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
                                    attendees.RemoveAt(attendees.Count - 1); //FIXME decide who to remove
                                }

                                Meeting meeting = new Meeting(p.Coordinator, p.Topic, p.Min_attendees, p.N_invitees, chosenSlot, p.Invitees, p.Version + 1, selectedRoom, attendees);
                                server.Meetings[chosenSlot.Location.Local].addMeeting(meeting);
                                server.Proposals.Remove(p.Topic);

                                return meeting;
                            }
                        }
                        else
                        {
                            throw new Exception(); //coordinator must be the one closing FIXME
                        }

                    }
                }
            }

            override public string getCommandId()
            {
                return this.command_id;
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
            this.Tickets = new Dictionary<string, Ticket>();

            this.puppetURL = puppetURL;
            this.masterServer = masterServer;

            //Reliable_Broadcast
            this.acks = new Dictionary<string, List<string>>();
            this.received_commands = new List<string>();
;
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

            lock (this)
            {
                while (freeze)
                {
                    Monitor.Wait(this);
                }
                Monitor.PulseAll(this);
            }
            
            Command command = new DoClose(userName, topic);
            ExecuteTicket(command, "");
                   
            
        }

        public void CreateProposal(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
        {
            this.waitBetweenRequests();

            lock (this)
            {
                while (freeze)
                {
                    Monitor.Wait(this);
                }
                Monitor.PulseAll(this);
            }


            Command command = new DoCreate(coordinator, topic, min_attendees, n_slots, n_invitees, slots, invitees);
            Proposal p;
            lock (this.Servers)
            {
                p = (Proposal)command.Execute(this);
                this.Servers[this.url]++;
                Monitor.PulseAll(this.Servers);
            }
            
            lock (received_commands)
            {
                received_commands.Add(command.getCommandId());
            }
            
            UpdateServers(command, this.url, this.Servers);
            
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
            //this.waitBetweenRequests();

            lock (this)
            {
                while (freeze)
                {
                    Monitor.Wait(this);
                }
                Monitor.PulseAll(this);
            }


            if (!this.Proposals.ContainsKey(topic)) //FIXME pending create? FIXME lock nas Proposals
            {
                Command command = new DoJoin(topic, userName, slots);
                ExecuteTicket(command, userName);
            }
            else
            {
                Command command = new DoJoin(topic, userName, slots);
                lock (received_commands)
                {
                    received_commands.Add(command.getCommandId());
                }
                lock (this.Servers)
                {
                    command.Execute(this);
                    this.Servers[this.url]++;

                    UpdateServers(command, this.url, this.Servers);
                    Monitor.PulseAll(this.Servers);
                }
            }

        }

        public void ListMeetings(String userName)
        {
            this.waitBetweenRequests();

            lock (this)
            {
                while (freeze)
                {
                    Monitor.Wait(this);
                }
                Monitor.PulseAll(this);
            }

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

            lock (received_commands)
            {
                received_commands.Add(client_URL);
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

        public void PropagateClose(Command command, string topic)
        {
            Thread[] pool = new Thread[this.Servers.Count - 1];
            int i = 0;
            Dictionary<string, int> vectorClock = new Dictionary<string, int>(this.Servers);
            foreach (KeyValuePair<String, int> entry in this.Servers)
            {
                if (this.url != entry.Key)
                {
                    string url = entry.Key;
                    pool[i] = new Thread(() => DoPropagate(url, command, topic, vectorClock));
                    pool[i].Start();
                    i++;
                }
            }
        }

        private void DoPropagate(string url, Command command, string topic, Dictionary<string, int> vectorClock)
        {
            ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
            si.UpdateClose(command, topic, this.url, vectorClock);
        }

        public void UpdateServers(Command command, string senderURL, Dictionary<string, int> clock)
        {
            lock (this.Servers)
            {
                Thread[] pool = new Thread[this.Servers.Count - 1];
                int i = 0;
                Dictionary<string, int> vectorClock = new Dictionary<string, int>(clock);
                foreach (KeyValuePair<String, int> entry in this.Servers)
                {
                    if (this.url != entry.Key)
                    {
                        string url = entry.Key;
                        pool[i] = new Thread(() => DoUpdate(url, senderURL, command, vectorClock));
                        pool[i].Start();
                        i++;
                    }
                }
                Monitor.PulseAll(this.Servers);
            }
        }

        private void DoUpdate(string url, string senderURL, Command command, Dictionary<string, int> vectorClock)
        {
            this.waitBetweenRequests();
            ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
            si.UpdateMeeting(command, senderURL, this.url, vectorClock);
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
                Monitor.PulseAll(this.Servers);
            }
        }

        private void DoUpdateClient(string serverUrl, string clientUrl, string userName)
        {
            ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), serverUrl);
            Console.WriteLine("Sou o servidor e vou fazer update com o user " + userName);
            si.UpdateClient(clientUrl, userName, this.url);
        }

        public void UpdateClient(string client_url, string userName, string serverURL)
        {
            //if command is in acks
            string id = client_url;
            lock (acks)
            {
                if (acks.ContainsKey(id))
                {
                    //adds server to the acks of the message
                    acks[id].Add(serverURL);
                }
                else
                {
                    acks.Add(id, new List<string>());
                    acks[id].Add(serverURL);
                }
                Monitor.PulseAll(acks);  //Wake every spleeping thread that are waiting for acks
            }

            lock (received_commands)
            {
                //If not command received broadcast to everyone
                if (!received_commands.Contains(id))
                {
                    received_commands.Add(id);
                    UpdateServersClients(client_url, userName);
                }
                else
                {
                    return;
                }
            }

            //From this point message will be delivered
            
            //For now it's like this
            int f = 1;   //Number of fails
            int x = f + 1;
            lock (acks)
            {
                while (acks[id].Count < x)
                {
                    Monitor.Wait(acks);
                }
                Monitor.PulseAll(acks);
            }

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

        public void UpdateMeeting(Command command, string originalSender, string serverURL, Dictionary<string, int> vectorClock)
        {
            //Implement Reliable_Broadcast_Servers
            //if command is in acks
            string command_id = command.getCommandId();
            lock (acks)
            {
                if (acks.ContainsKey(command_id))
                {
                    //adds server to the acks of the message
                    acks[command_id].Add(serverURL);
                }
                else
                {
                    acks.Add(command_id, new List<string>());
                    acks[command_id].Add(serverURL);
                }
                Monitor.PulseAll(acks);  //Wake every spleeping thread that are waiting for acks
            }

            lock (received_commands)
            {
                //If not command received broadcast to everyone
                if (!received_commands.Contains(command_id))
                {
                    received_commands.Add(command_id);
                    UpdateServers(command, originalSender, vectorClock);
                }
                else
                {
                    return;
                }
            }

            //From this point message will be delivered
            int f = 1; //Number of fails
            int x = f + 1;
            lock (acks)
            {
                while (acks[command_id].Count < x)
                {
                    Monitor.Wait(acks);
                }

                Monitor.PulseAll(acks);
            }
            Console.WriteLine(command_id);
            lock (this.Servers)
            {
                printClocks(originalSender, vectorClock, this.Servers);
                while (!checkClock(originalSender, vectorClock))
                {
                    Monitor.Wait(this.Servers);
                }

                this.Servers[originalSender]++;

                command.Execute(this);
                Monitor.PulseAll(this.Servers);
                Console.WriteLine("join/create executed");
            }
        }
        public void UpdateClose(Command command, string topic, string serverURL, Dictionary<string, int> vectorClock)
        {
            //FIXME add RB
            lock (this.Servers)
            {
                printClocks(serverURL, vectorClock, this.Servers);
                while (!checkClock(serverURL, vectorClock))
                {
                    Monitor.Wait(this.Servers);
                }

                this.Servers[serverURL]++;

                lock (this.Closes)
                {
                    if(!this.Closes.ContainsKey(topic))
                        this.Closes.Add(topic, command);
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

        public void AddServer(String serverURL)
        {
            lock (this.Servers)
            {
                this.Servers.Add(serverURL, 0);
                Monitor.PulseAll(this.Servers);
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

            lock (this)
            {

                if (freeze)
                {
                    Console.WriteLine("But I'm Freezed");
                }
            }

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

        public void Freeze()
        {
            lock (this)
            {
                freeze = true;
                Monitor.PulseAll(this);
            }
        }

        public void Unfreeze()
        {
            lock (this)
            {
                freeze = false;
                Monitor.PulseAll(this);
            }
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

        private void printClocks(string serverURL, Dictionary<string, int> clock1, Dictionary<string, int> clock2)
        {
            Console.WriteLine("\r\nClock from " + serverURL + "\r\n");
            string value = "";
            foreach (KeyValuePair<string, int> entry in clock1)
            {
                if(!clock2.ContainsKey(entry.Key))
                {
                    value = "#";
                }
                else
                {
                    value = clock2[entry.Key] + "";
                }
                Console.WriteLine(entry.Key + ": " + value + " " + entry.Value);
            }
        }


        public int GetTicket(string topic, string serverURL, Dictionary<string, int> vectorClock)
        {
            
            lock (this.Servers)
            {
                printClocks(serverURL, vectorClock, this.Servers);
                while (!checkClock(serverURL, vectorClock))
                {
                    Monitor.Wait(this.Servers);
                }

                lock(this.Closes) {

                    lock (this.Tickets)
                    {

                        if (this.Tickets.ContainsKey(topic))
                        {
                            return 0; //default value for already existing tickets FIXME fault tolerance
                                      //FIXME in case of failure, check with everyone
                        }
                        else
                        {
                            ticket++;
                            this.Tickets.Add(topic, new Ticket(ticket, serverURL, this.Closes[topic]));
                        }
                        Monitor.PulseAll(this.Servers);
                        Monitor.PulseAll(this.Tickets);
                        return ticket;
                    }
                }
            }
        }

        private void PropagateTicket(int ticket, string topic, AbstractMeeting am, Dictionary<string, int> vectorClock)
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
                        pool[i] = new Thread(() => DoPropagateTicket(url, ticket, topic, am, vectorClock));
                        pool[i].Start();
                        i++;
                    }
                }
                Monitor.PulseAll(this.Servers);
            }
        }

        private void DoPropagateTicket(string url, int ticket, string topic, AbstractMeeting am, Dictionary<string, int> vectorClock)
        {
            ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
            si.ReceiveTicketResult(topic, this.url, ticket, am, vectorClock);
        }

        public void ReceiveTicketResult(string topic, string serverURL, int ticket, AbstractMeeting am, Dictionary<string, int> vectorClock)
        {
            Console.WriteLine(topic + "achtung!1");
            Dictionary<string, Command> pendingJoins = new Dictionary<string, Command>();
            lock (this.Servers)
            {
                printClocks(serverURL, vectorClock, this.Servers);
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
                Console.WriteLine(topic + "achtung!2");
                lock (this.Proposals)
                {
                    lock (this.Meetings)
                    {
                        lock (this.Closes)
                        {
                            lock (this.Tickets)
                            {
                                Console.WriteLine(topic + "achtung!3");
                                if (am.isProposal())
                                {
                                    this.Proposals[topic] = (Proposal)am;
                                }
                                else
                                {
                                    Meeting m = (Meeting)am;
                                    this.Meetings[m.Slot.Location.Local].addMeeting(m); //FIXME must replace, not add
                                    AbstractMeeting p = null;
                                    if (this.Proposals.ContainsKey(topic))
                                    {
                                        p = this.Proposals[topic];
                                        this.Proposals.Remove(topic);
                                    }
                                    else
                                    {
                                        foreach(Meeting meeting in this.Meetings[m.Slot.Location.Local].Meetings)
                                        {
                                            if(meeting.Topic == m.Topic)
                                            {
                                                p = meeting;
                                                break;
                                            }
                                        }
                                    }
                                    
                                    foreach (Attendee a in p.Attendees)
                                    {
                                        bool notIn = true;
                                        foreach (Attendee attendee in m.Attendees)
                                        {
                                            if (attendee.Name == a.Name)
                                            {
                                                notIn = false;
                                                break;
                                            }
                                        }
                                        if (notIn && CouldAttend(m, a) && !this.Closes.ContainsKey(topic + a.Name))
                                        {
                                            List<string> slots = new List<string>();
                                            foreach (Slot s in a.Available_slots)
                                            {
                                                slots.Add(s.Location.Local + "," + s.Date);
                                            }
                                            Command command = new DoJoin(topic, a.Name, slots);
                                            pendingJoins.Add(a.Name, command);
                                        }

                                    }
                                }
                                
                                if (this.masterServer != this.url)
                                {
                                    this.Tickets.Add(topic, new Ticket(ticket, serverURL, this.Closes[topic]));
                                }
                                this.Closes.Remove(topic);
                                lastTicket++;
                                Monitor.PulseAll(this.Tickets);
                            }
                        }
                    }
                }
                Monitor.PulseAll(this.Servers);

            }

            foreach (KeyValuePair<string, Command> entry in pendingJoins)
            {
                ExecuteTicket(entry.Value, entry.Key);
            }
        }

        private Boolean CouldAttend(Meeting m, Attendee user)
        {
            if (m.Attendees.Count == m.SelectedRoom.Capacity)
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

        private void ExecuteTicket(Command command, string userName)
        {
            string topic = command.Topic + userName;
            int newTicket;
            Dictionary<string, int> vectorClock;
            lock (this.Servers)
            {
                lock (this.Closes)
                {
                    if (this.Closes.ContainsKey(topic))
                    {
                        Monitor.PulseAll(this.Servers);
                        return;
                    }
                    this.Closes.Add(topic, command);
                
                    this.Servers[this.url]++;
                    PropagateClose(command, topic);
                    this.Servers[this.url]++;
                    if (masterServer == this.url)
                    {
                        newTicket = GetTicket(topic, this.url, this.Servers);
                        if (newTicket == 0)
                        {
                            this.Servers[this.url]--;
                            Monitor.PulseAll(this.Servers);
                            return;
                        }
                    }
                    else
                    {
                        ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), masterServer);
                        newTicket = si.GetTicket(topic, this.url, this.Servers);
                        if (newTicket == 0)
                        {
                            this.Servers[this.url]--;
                            Monitor.PulseAll(this.Servers);
                            return;
                        }
                        lock (this.Tickets)
                        {
                            this.Tickets.Add(topic, new Ticket(newTicket, this.url, this.Closes[topic]));
                            Monitor.PulseAll(this.Tickets);
                        }
                    }
                }
                vectorClock = new Dictionary<string, int>(this.Servers);
                Monitor.PulseAll(this.Servers);
            }



            lock (this.Tickets)
            {
                while (newTicket - 1 != lastTicket)
                {
                    Monitor.Wait(this.Tickets);
                }
                Monitor.PulseAll(this.Tickets);
            }

            Console.WriteLine("Executing ticket " + newTicket + " " + topic);



            //Before updateServers (reliable broadcast)
            string command_id = command.getCommandId();
            lock (received_commands) //Adding my message to my received_commands
            {
                received_commands.Add(command_id);
            }
            AbstractMeeting am = command.Execute(this);
            lock (this.Closes)
            {
                lock (this.Tickets)
                {
                    lastTicket++;
                    this.Closes.Remove(topic);
                    Monitor.PulseAll(this.Tickets);
                }
            }

            PropagateTicket(newTicket, topic, am, vectorClock);
            
        }

    }

}

