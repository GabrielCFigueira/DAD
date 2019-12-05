using Puppet_Server;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
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

            BinaryServerFormatterSinkProvider provider = new BinaryServerFormatterSinkProvider();

            IDictionary props = new Hashtable();
            props["port"] = uri.Port;
            props["timeout"] = 3000; // in milliseconds
            TcpChannel channel = new TcpChannel(props, null, provider);

            //TcpChannel channel = new TcpChannel(uri.Port);
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
        Dictionary<String, String> ClientsURLS;
        Dictionary<String, Proposal> Proposals;
        Dictionary<String, LocationMeetings> Meetings;
        Dictionary<String, int> Servers;
        List<String> Available_Servers;

        string id;
        String url;
        int maxFaults;
        int minDelay;
        int maxDelay;

        String puppetURL;
        String masterServer;
        String lockTicket = "";
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
            string topic;
            int min_attendees;
            int n_slots;
            int n_invitees;
            List<String> slots;
            List<String> invitees;

            public DoCreate(string coordinator, string topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
            {
                this.coordinator = coordinator;
                this.topic = topic;
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
                        Proposal p = new Proposal(coordinator, topic, min_attendees, n_slots, n_invitees, Slots, invitees);
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
            string topic;
            string userName;
            List<string> slots;
            public DoJoin(string topic, string userName, List<string> slots)
            {
                this.topic = topic;
                this.userName = userName;
                this.slots = slots;
                this.command_id = topic +  userName;
            }

            override public AbstractMeeting Execute(ServerInterface si)
            {
                ServerImpl server = (ServerImpl)si;
                lock (server.Proposals)
                {
                    Proposal p = null;

                    if (!server.Proposals.TryGetValue(topic, out p))
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

            override public string getCommandId()
            {
                return this.command_id;
            }
        }

        [Serializable]
        private class DoClose : Command
        {
            string command_id;
            string topic;
            string userName;

            public DoClose(String userName, String topic)
            {
                this.topic = topic;
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
                        Proposal p = server.Proposals[topic];
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
                                lock(server.lockTicket)
                                {
                                    server.lastTicket++;
                                }
                                return p;
                            }
                            else
                            {
                                while (p.Attendees.Count > selectedRoom.Capacity)
                                {
                                    p.Attendees.RemoveAt(p.Attendees.Count - 1);
                                }

                                Meeting meeting = new Meeting(p.Coordinator, p.Topic, p.Min_attendees, p.N_invitees, chosenSlot, p.Invitees, p.Version + 1, selectedRoom, p.Attendees);
                                server.Meetings[chosenSlot.Location.Local].addMeeting(meeting);
                                server.Proposals.Remove(p.Topic);

                                lock (server.lockTicket)
                                {
                                    server.lastTicket++;
                                }

                                return meeting;
                            }
                        }
                        else
                        {
                            lock (server.lockTicket)
                            {
                                server.lastTicket++;
                            }
                            throw new Exception(); //coordinator must be the one closing
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
            this.ClientsURLS = new Dictionary<String, String>();
            this.Servers = new Dictionary<string, int>();
            this.Available_Servers = new List<String>();
            this.Available_Servers.Add(url);
            this.Servers.Add(url, 0);

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

            int newTicket;
            if(masterServer == "1")
            {
                newTicket = GetTicket();
            }
            else
            {
                ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), masterServer);
                newTicket = si.GetTicket();
            }

            lock (lockTicket)
            {
                while (newTicket - 1 != lastTicket)
                {
                    Monitor.Wait(lockTicket);
                }
            }
            Command command = new DoClose(userName, topic);
            command.Execute(this);
            lock (this.Servers)
            {
                this.Servers[this.url]++;
            }
            //Before updateServers (reliable broadcast)
            string command_id = command.getCommandId();
            lock (received_commands) //Adding my message to my received_commands
            {
                received_commands.Add(command_id);
            }

            UpdateServers(command);
            Console.WriteLine("Acabei o close e correu tudo bem");
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
            Proposal p = (Proposal) command.Execute(this);
            lock (this.Servers)
            {
                this.Servers[this.url]++;
            }
            
            lock (received_commands)
            {
                received_commands.Add(command.getCommandId());
            }

            UpdateServers(command);
            ClientInterface c = this.Clients[coordinator];
            Console.WriteLine("Tenho " + this.Clients.Count + " clientes");
            int numberOfMessages = (int)Math.Ceiling(Math.Log(this.Clients.Count, 2));
            int totalRounds;
            if(numberOfMessages == 1)
                totalRounds = (int)Math.Ceiling(Math.Log(this.Clients.Count, 2));
            else
                totalRounds = (int)Math.Ceiling(Math.Log(this.Clients.Count, numberOfMessages));
            totalRounds += 2; //this is for gossip
            //c.AddProposal(p);
            c.Gossip(p,1,totalRounds,numberOfMessages);
            /*if (n_invitees > 0)
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
            }*/
        }

        public void JoinMeeting(String topic, String userName, List<String> slots)
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

            Command command = new DoJoin(topic, userName, slots);
            command.Execute(this);
            lock (this.Servers)
            {
                this.Servers[this.url]++;
            }
            //Before updateServers (reliable broadcast)
            lock (received_commands)
            {
                received_commands.Add(command.getCommandId());
            }
            UpdateServers(command);

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
            //this.UpdateServersClients(client_URL, userName); // rever isto
            ClientInterface c = (ClientInterface)Activator.GetObject(
                 typeof(ClientInterface),
                 client_URL);
            c.Connect(this.url);
            Console.WriteLine("TENHO " + this.Proposals.Count + " PROPOSALS");
            //c.InitializeMeetings(this.Proposals,this.Meetings);
            c.getServers(this.Servers);
            lock (this.Clients)
            {
                lock (this.ClientsURLS)
                {
                    if(this.Clients.Count != 0)
                    {
                        Console.WriteLine("Ja existem mais clientes. Vai pedir os meetings a um deles.");
                        String clientName;
                        String clientURL;
                        (clientName,clientURL) = this.getRandomClientName();
                        c.AskNeighbourForMeetings(clientName,clientURL);
                    }
                    Clients.Add(userName, c);
                    ClientsURLS.Add(userName, client_URL);

                    /*foreach (KeyValuePair<String, ClientInterface> entry in this.Clients)
                    {
                        ClientInterface ci = entry.Value;
                        //ci.UpdateUsers(this.Clients);
                        ci.UpdateUsers(this.ClientsURLS);
                    }*/
                }
            }

            lock (received_commands)
            {
                received_commands.Add(client_URL);
            }

            this.UpdateServersClients(client_URL, userName);
            Console.WriteLine("Registei o/a cliente " + userName);


        }

        public (String,String) getRandomClientName()
        {
            List<String> listClientNames = this.ClientsURLS.Keys.ToList();
            Random random = new Random();
            int randomIndex = random.Next(listClientNames.Count);
            String chosenClientName = listClientNames[randomIndex];
            return (chosenClientName,this.ClientsURLS[chosenClientName]);
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

        public void UpdateServers(Command command)
        {
            //lock (this.Available_Servers)
            //{
                Thread[] pool = new Thread[this.Available_Servers.Count - 1];
                int i = 0;
                foreach (string serverURL in this.Available_Servers)
                {
                    if (this.url != serverURL)
                    {
                        string url = serverURL;
                        pool[i] = new Thread(() => DoUpdate(url, command));
                        pool[i].Start();
                        i++;
                    }
                }
            //}
        }

        private void DoUpdate(string url, Command command)
        {
            try
            {
                ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
                si.UpdateMeeting(command, this.url, this.Servers);
            }
            catch (SocketException)
            {
                Console.WriteLine("O servidor " + url + " crashou.Vou remove-lo");
                //RemoveCrashedServer(url);
                lock (this.Available_Servers) //is this the fix??
                {
                    this.Available_Servers.Remove(url);
                }
            }
        }

        /*public void RemoveCrashedServer(String server_url)
        {
            this.Available_Servers.Remove(server_url);
            foreach (String url in this.Available_Servers)
            {
                if (url != this.url)
                {
                    ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
                    si.RemoveAvailableServer(server_url);
                }
            }
        }*/

        public void RemoveAvailableServer(String server_url)
        {
            this.Available_Servers.Remove(server_url);
        }

        public void UpdateServersClients(String clientUrl, String userName)
        {
            //lock (this.Available_Servers)
            //{

                Thread[] pool = new Thread[this.Available_Servers.Count - 1];
                int i = 0;
                foreach (String serverURL in this.Available_Servers)
                {
                    if(serverURL != this.url)
                    {
                        string url = serverURL;
                        pool[i] = new Thread(() => DoUpdateClient(url, clientUrl, userName));
                        pool[i].Start();
                        i++;
                    }
                }
            //}
        }

        private void DoUpdateClient(string serverUrl, string clientUrl, string userName)
        {
            try
            {
                ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), serverUrl);
                Console.WriteLine("Sou o servidor e vou fazer update com o user " + userName);
                si.UpdateClient(clientUrl, userName, this.url); //bug aqui??
            }
            catch (SocketException)
            {
                Console.WriteLine("O servidor " + serverUrl + " crashou.Vou remove-lo");
                //RemoveCrashedServer(serverUrl);
                lock (this.Available_Servers) //is this the fix??
                {
                    this.Available_Servers.Remove(url);
                }
            }
        }

        public void UpdateClient(string client_url, string userName, string serverURL)
        {
            //Implement Reliable_Broadcast_Client
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
            int x = this.maxFaults + 1;
            lock (acks)
            {
                while (acks[id].Count < x)
                {
                    Monitor.Wait(acks);
                }
                Monitor.PulseAll(acks);
            }
            
            Console.WriteLine("--Causality--");
            //Causality

            lock (this.Clients)
            {
                ClientInterface ci = (ClientInterface)Activator.GetObject(typeof(ClientInterface), client_url);
                if (!this.Clients.ContainsKey(userName))
                {
                    Console.WriteLine("Passei a conhecer o user " + userName);
                    this.Clients[userName] = ci;
                    this.ClientsURLS[userName] = client_url; //rever isto
                }
            }
        }

        public void UpdateMeeting(Command command, string serverURL, Dictionary<string, int> vectorClock)
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
                    UpdateServers(command);
                }
                else
                {
                    return;
                }
            }

            //From this point message will be delivered
            int x = this.maxFaults + 1;
            lock (acks)
            {
                while (acks[command_id].Count < x)
                {
                    Monitor.Wait(acks);
                }

                Monitor.PulseAll(acks);
            }

            Console.WriteLine("Causality");
            //Causality

            //lock(this.Servers)
            //{
            //    Console.WriteLine("Server's own clock");
            //    printClock(this.Servers);
            //    Console.WriteLine("Received Clock");
            //    printClock(vectorClock);
            //    while(!checkClock(serverURL, vectorClock))
            //    {
            //        Monitor.Wait(this.Servers);
            //    }

            //    this.Servers[serverURL]++;

            //    Monitor.Pulse(this.Servers);
            //}

            command.Execute(this);
        }

        private bool checkClock(string serverURL, Dictionary<string, int> vectorClock)
        {
            
            foreach (KeyValuePair<string, int> entry in this.Servers)
            {
                if (serverURL != entry.Key && entry.Value < vectorClock[entry.Key])
                {
                    return false;
                }
                else if (serverURL == entry.Key && entry.Value != vectorClock[entry.Key] - 1)
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
                lock (this.Available_Servers)
                {
                    this.Servers.Add(serverURL, 0);
                    this.Available_Servers.Add(serverURL);
                }
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


        public int GetTicket()
        {
            lock (lockTicket)
            {
                return ++ticket;
            }
        }

    }

}

