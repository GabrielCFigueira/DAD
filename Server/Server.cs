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

            String masterServer = args[6]; // Initial

            Uri uri = new Uri(url);

            BinaryServerFormatterSinkProvider provider = new BinaryServerFormatterSinkProvider();

            IDictionary props = new Hashtable();
            props["port"] = uri.Port;
            props["timeout"] = 6000; // in milliseconds
            TcpChannel channel = new TcpChannel(props, null, provider);

            try
            {
                //TcpChannel channel = new TcpChannel(uri.Port);
                ChannelServices.RegisterChannel(channel, false);

                ServerImpl MeetingServer = new ServerImpl(id, url, maxFaults, minDelay, maxDelay, masterServer);
                RemotingServices.Marshal(MeetingServer, uri.Segments[1], typeof(ServerImpl));

                MeetingServer.InitializeLocationsAndRooms();

                Console.ReadLine();
            }
            catch (Exception e) 
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
                Console.ReadLine();
            }
        }
    }


    class ServerImpl : MarshalByRefObject, ServerInterface, IServerPuppet
    {

        Dictionary<String, ClientInterface> Clients;
        Dictionary<String, String> ClientsURLS;
        Dictionary<String, Proposal> Proposals;
        Dictionary<String, LocationMeetings> Meetings;
        Dictionary<String, int> MyVectorClock;
        Dictionary<String, Command> PendingCommands;
        Dictionary<string, Ticket> Tickets;
        List<String> Available_Servers;

        string id;
        string myURL;
        int maxFaults;
        int minDelay;
        int maxDelay;

        Thread thread;

        String masterServer;
        Int32 ticket = 0;
        Int32 lastTicket = 0;

        //Reliable_BroadCast structures
        Dictionary<string, List<string>> acks; //Key: depends on command, values: list of servers
        List<string> received_commands;

        //Leader_Election
        Dictionary<string, int> tickets; //string is serverURL, int is ticket of that server
        string LE;

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
                this.command_id = "Create " + topic;  //id Create
            }

            override public AbstractMeeting Execute(ServerInterface si)
            {
                ServerImpl server = (ServerImpl)si;
                lock (server.Proposals)
                {
                    if (server.Proposals.ContainsKey(this.Topic)) //idempotency
                    {
                        return server.Proposals[this.Topic];
                    }
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
            Dictionary<string, int> vectorClock;
            string serverURL;
            public DoJoin(string topic, string userName, List<string> slots, Dictionary<string, int> vectorClock, string serverURL)
                : base(topic)
            {
                this.userName = userName;
                this.slots = slots;
                this.command_id = "join " + topic + userName;
                this.vectorClock = new Dictionary<string, int>(vectorClock);
                this.serverURL = serverURL;
            }

            override public AbstractMeeting Execute(ServerInterface si)
            {
                ServerImpl server = (ServerImpl)si;
                lock (server.Proposals)
                {
                    AbstractMeeting am = null;
                    if (!server.Proposals.ContainsKey(this.Topic))
                    {
                        
                        foreach (LocationMeetings lm in server.Meetings.Values)
                        {
                            foreach (Meeting m in lm.Meetings)
                            {
                                if (m.Topic == this.Topic)
                                {
                                    am = m;
                                    if (am.Attendees.Count == m.SelectedRoom.Capacity)
                                    {
                                        Console.WriteLine("Não há espaço suficiente para um join atrasado");
                                        return am;
                                    }
                                    Console.WriteLine("O cliente " + userName + " fez join atrasado a uma Meeting ja fechada, vamos tentar juntá-lo");
                                    goto label;
                                }
                            }
                        }
                        return null;
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
                            if (am.isProposal())
                            {
                                ((Proposal)am).Slots[s].Votes += 1;
                            }
                            Slots.Add(slot);
                        }

                        Attendee a = new Attendee(userName, Slots, vectorClock, serverURL);
                        if (!am.isProposal())
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

            public void setCommandId(string id)
            {
                this.command_id = id;
            }
        }

        [Serializable]
        private class DoClose : Command
        {
            private class JoinSorter : IComparer<Attendee>
            {
                public int Compare(Attendee a, Attendee b)
                {
                    int result = 0;

                    if (checkClock(a.ServerURL, a.VectorClock, b.VectorClock))
                        result++;

                    if (checkClock(b.ServerURL, b.VectorClock, a.VectorClock))
                        result--;

                    if (result == 0)
                    {
                        result = -string.Compare(a.Name, b.Name, comparisonType: StringComparison.OrdinalIgnoreCase);
                    }
                    return result;
                }



            }

            string command_id;
            string userName;

            public DoClose(String userName, String topic)
                : base(topic)
            {
                this.userName = userName;
                this.command_id = "close " + topic;
            }

            override public AbstractMeeting Execute(ServerInterface si)
            {
                ServerImpl server = (ServerImpl)si;
                lock (server.Proposals)
                {
                    lock (server.Meetings)
                    {
                        if (!server.Proposals.ContainsKey(this.Topic))
                        {
                            foreach (LocationMeetings lm in server.Meetings.Values)
                            {
                                foreach (Meeting m in lm.Meetings)
                                {
                                    if (m.Topic == this.Topic)
                                    {
                                        return m;
                                    }
                                }
                            }
                        }
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
                                foreach (Attendee at in p.Attendees)
                                {
                                    foreach (Slot s in at.Available_slots)
                                    {
                                        if (s.Date == chosenSlot.Date && s.Location.Local == chosenSlot.Location.Local)
                                        {
                                            attendees.Add(at);
                                            break;
                                        }
                                    }
                                }
                                if (attendees.Count > selectedRoom.Capacity)
                                {
                                    attendees.Sort(new JoinSorter());
                                    do
                                    {
                                        attendees.RemoveAt(attendees.Count - 1);
                                    }
                                    while (attendees.Count > selectedRoom.Capacity);
                                }

                                Meeting meeting = new Meeting(p.Coordinator, p.Topic, p.Min_attendees, p.N_invitees, chosenSlot, p.Invitees, p.Version + 1, selectedRoom, attendees);
                                server.Meetings[chosenSlot.Location.Local].Meetings.Remove(meeting);
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


        public ServerImpl(string id, String url, int maxFaults, int minDelay, int maxDelay, string masterServer)
        {
            this.id = id;
            this.myURL = url;
            this.maxFaults = maxFaults;
            this.minDelay = minDelay;
            this.maxDelay = maxDelay;
            this.Proposals = new Dictionary<String, Proposal>();
            this.Clients = new Dictionary<String, ClientInterface>();
            this.Meetings = new Dictionary<string, LocationMeetings>();
            this.ClientsURLS = new Dictionary<String, String>();
            this.MyVectorClock = new Dictionary<string, int>();
            this.Available_Servers = new List<String>();
            this.Available_Servers.Add(url);
            this.MyVectorClock.Add(url, 0);
            this.PendingCommands = new Dictionary<string, Command>();
            this.Tickets = new Dictionary<string, Ticket>();

            this.masterServer = masterServer; //Leader_ID

            this.thread = new Thread(() => checkPendings());
            this.thread.Start();

            //Reliable_Broadcast
            this.acks = new Dictionary<string, List<string>>();
            this.received_commands = new List<string>();

            //Leader_Election
            this.tickets = new Dictionary<string, int>();
            this.tickets.Add(url, -1);
            this.LE = "";
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

            lock (this) //FIXME freeze entre servidores
            {
                while (freeze)
                {
                    Monitor.Wait(this);
                }
                Monitor.PulseAll(this);
            }

            Command command = new DoClose(userName, topic);
            ExecuteTicket(command);
                   
            
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
            Dictionary<string, int> clock;
            lock (this.MyVectorClock)
            {
                p = (Proposal)command.Execute(this);
                this.MyVectorClock[this.myURL]++;
                clock = new Dictionary<string, int>(this.MyVectorClock);
                Monitor.PulseAll(this.MyVectorClock);
            }

            lock (received_commands)
            {
                received_commands.Add(command.getCommandId());
            }
            Console.WriteLine("Saí do received_commands");

            UpdateServers(command, this.myURL, clock);
            ClientInterface c = this.Clients[coordinator];
            Console.WriteLine("Tenho " + this.Clients.Count + " clientes");
            int numberOfMessages = (int)Math.Ceiling(Math.Log(this.Clients.Count, 2));
            int totalRounds;
            if (numberOfMessages == 1)
                totalRounds = (int)Math.Ceiling(Math.Log(this.Clients.Count, 2));
            else
                totalRounds = (int)Math.Ceiling(Math.Log(this.Clients.Count, numberOfMessages));
            totalRounds += 3; //this is for gossip
            Thread thread = new Thread(() => c.Gossip(p, 1, totalRounds, numberOfMessages));
            thread.Start();

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

            lock (this.Proposals)
            {
                if (!this.Proposals.ContainsKey(topic))
                {
                    Console.WriteLine("No such open meeting as " + topic);
                    return; //FIXME give error message
                }
                else
                {
                    Dictionary<string, int> clock;
                    Command command;
                    lock (this.MyVectorClock)
                    {
                        this.MyVectorClock[this.myURL]++;
                        command = new DoJoin(topic, userName, slots, this.MyVectorClock, this.myURL);
                        lock (received_commands)
                        {
                            received_commands.Add(command.getCommandId());
                        }
                        command.Execute(this);

                        clock = new Dictionary<string, int>(this.MyVectorClock);
                        Monitor.PulseAll(this.MyVectorClock);
                    }
                    UpdateServers(command, this.myURL, clock);
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
            //this.UpdateServersClients(client_URL, userName); // rever isto
            ClientInterface c = (ClientInterface)Activator.GetObject(
                 typeof(ClientInterface),
                 client_URL);
            c.Connect(this.myURL);
            Console.WriteLine("TENHO " + this.Proposals.Count + " PROPOSALS");
            //c.InitializeMeetings(this.Proposals,this.Meetings);
            c.getServers(this.MyVectorClock);
            lock (this.Clients)
            {
                lock (this.ClientsURLS)
                {
                    if (this.Clients.Count != 0)
                    {
                        Console.WriteLine("Ja existem mais clientes. Vai pedir os meetings a um deles.");
                        String clientName;
                        String clientURL;
                        (clientName, clientURL) = this.getRandomClientName();
                        c.AskNeighbourForMeetings(clientName, clientURL);
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

        public (String, String) getRandomClientName()
        {
            lock (this.ClientsURLS)
            {
                List<String> listClientNames = this.ClientsURLS.Keys.ToList();
                Random random = new Random();
                int randomIndex = random.Next(listClientNames.Count);
                String chosenClientName = listClientNames[randomIndex];
                return (chosenClientName, this.ClientsURLS[chosenClientName]);
            }
        }

        public Dictionary<String, String> GetListOfRandomClients(int numberOfClients, String userName)
        {
            Dictionary<String, String> clients =  new Dictionary<String, String>();
            if(numberOfClients < this.Clients.Count - 1)
            {
                for(int i = 0; i < numberOfClients; i++)
                {
                    (String clientName,String clientURL) = this.getRandomClientName();
                    String test;
                    clients.TryGetValue(clientName, out test);
                    while (clientName == userName || test != null)
                    {
                        (clientName, clientURL) = this.getRandomClientName();
                        clients.TryGetValue(clientName, out test);
                    }
                    clients[clientName] = clientURL;
                }
            }
            else
            {
                //clients = this.ClientsURLS;
                foreach(KeyValuePair<String,String> entry in this.ClientsURLS)
                {
                    if(entry.Key != userName)
                        clients[entry.Key] = entry.Value;
                }
            }
            return clients;
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

        public void PropagateClose(Command command, string topic, string senderURL, Dictionary<string, int> vectorClock)
        {
            lock (this.Available_Servers)
            {
                Thread[] pool = new Thread[this.Available_Servers.Count - 1];
                int i = 0;
                for(i = 1; i < this.Available_Servers.Count; i++)
                {
                    if (this.myURL != this.Available_Servers[i] && this.Available_Servers[i] != "failed")
                    {
                        string url = this.Available_Servers[i];
                        pool[i - 1] = new Thread(() => DoPropagate(url, senderURL, command, topic, vectorClock));
                        pool[i - 1].Start();
                    }
                }
            }
        }

        private void DoPropagate(string url, string senderURL, Command command, string topic, Dictionary<string, int> vectorClock)
        {
            lock (this) //FIXME freeze entre servidores
            {
                while (freeze)
                {
                    Monitor.Wait(this);
                }
                Monitor.PulseAll(this);
            }
            try
            {
                ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
                si.UpdateClose(command, topic, senderURL, this.myURL, vectorClock);
            }
            catch (SocketException)
            {
                Console.WriteLine("O servidor " + url + " crashou.Vou remove-lo1");
                lock (this.Available_Servers) //is this the fix??
                {
                    this.WriteFailed(url);
                    //this.Available_Servers.Remove(url);
                }
                lock (tickets)
                {
                    tickets.Remove(url);
                    Monitor.PulseAll(tickets);
                }

                if (masterServer == url)
                {
                    //Start Leader_Election
                    Thread thread = new Thread(() => leader_election("LE", this.lastTicket, this.myURL, this.myURL, url));
                    thread.Start();
                }
            }
        }

               

        public void UpdateServers(Command command, string senderURL, Dictionary<string, int> vectorClock)
        {
            lock (this.Available_Servers)
            {
                Thread[] pool = new Thread[this.Available_Servers.Count - 1];
                int i = 0;
                if (senderURL == this.myURL) //FIXME hardcoded delay
                {
                    this.waitBetweenRequests();
                }
                for(i = 1; i < this.Available_Servers.Count; i++)
                {
                    if (this.myURL != this.Available_Servers[i] && this.Available_Servers[i] != "failed")
                    {
                        string url = this.Available_Servers[i];
                        pool[i - 1] = new Thread(() => DoUpdate(url, senderURL, command, vectorClock));
                        pool[i - 1].Start();
                    }
                }
            }
        }

        private void DoUpdate(string url, string senderURL, Command command, Dictionary<string, int> vectorClock)
        {
            lock (this) //FIXME freeze entre servidores
            {
                while (freeze)
                {
                    Monitor.Wait(this);
                }
                Monitor.PulseAll(this);
            }
            try
            {
                ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
                si.UpdateMeeting(command, senderURL, this.myURL, vectorClock);
            }
            catch (SocketException)
            {
                Console.WriteLine("O servidor " + url + " crashou.Vou remove-lo2");
                lock (this.Available_Servers)
                {
                    this.WriteFailed(url);
                    //this.Available_Servers.Remove(url);
                }

                lock (tickets)
                {
                    tickets.Remove(url);
                    Monitor.PulseAll(tickets);
                }

                if (masterServer == url)
                {
                    //Start Leader_Election


                    lock (tickets) //Add my own ticket
                    {
                        tickets[this.myURL] = this.lastTicket;
                    }

                    Console.WriteLine("LEADER ELECTION!!!");
                    Thread thread = new Thread(() => leader_election("LE", this.lastTicket, this.myURL, this.myURL, url));
                    thread.Start();
                }
            }
        }



        public void UpdateServersClients(String clientUrl, String userName)
        {
            lock (this.Available_Servers)
            {

                Thread[] pool = new Thread[this.Available_Servers.Count];
                int i = 0;
                for(i = 0; i <this.Available_Servers.Count; i++)
                {
                    if(this.Available_Servers[i] != this.myURL && this.Available_Servers[i] != "failed")
                    {
                        string url = this.Available_Servers[i]; 
                        pool[i - 1] = new Thread(() => DoUpdateClient(url, clientUrl, userName));
                        pool[i - 1].Start();
                    }
                }
                Monitor.PulseAll(Available_Servers);
            }
        }

        private void DoUpdateClient(string serverUrl, string clientUrl, string userName)
        {
            try
            {
                ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), serverUrl);
                Console.WriteLine("Sou o servidor e vou fazer update com o user " + userName);
                si.UpdateClient(clientUrl, userName, this.myURL);
            }
            catch (SocketException)
            {
                Console.WriteLine("O servidor " + serverUrl + " crashou.Vou remove-lo3");
                lock (this.Available_Servers) //is this the fix??
                {
                    this.WriteFailed(serverUrl);
                }

                lock (tickets)
                {
                    tickets.Remove(serverUrl);
                    Monitor.PulseAll(tickets);
                }


                if (masterServer == serverUrl)
                {
                    //Start Leader_Election
                    Thread thread = new Thread(() => leader_election("LE", this.lastTicket, this.myURL, this.myURL, serverUrl));
                    thread.Start();
                }
            }
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
                }
                else
                {
                    return;
                }
            }

            UpdateServersClients(client_url, userName);

            //From this point message will be delivered

            //For now it's like this
            int x = this.maxFaults;
            lock (acks)
            {
                while (acks[id].Count < x && acks[id].Count < this.tickets.Count - 1)
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
                    this.ClientsURLS[userName] = client_url; //rever isto
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
                }
                else
                {
                    return;
                }
            }
            UpdateServers(command, originalSender, vectorClock);

            //From this point message will be delivered
            int x = this.maxFaults;
            lock (acks)
            {
                while (acks[command_id].Count < x && acks[command_id].Count < this.tickets.Count - 1)
                {
                    Monitor.Wait(acks);
                }

                Monitor.PulseAll(acks);
            }

            lock (this.MyVectorClock)
            {
                printClocks(originalSender, vectorClock, this.MyVectorClock);
                while (!checkClock(originalSender, vectorClock, this.MyVectorClock))
                {
                    Monitor.Wait(this.MyVectorClock);
                }
                this.MyVectorClock[originalSender] = Math.Max(vectorClock[originalSender],this.MyVectorClock[originalSender]);

                command.Execute(this);
                Monitor.PulseAll(this.MyVectorClock);
            }
        }
        public void UpdateClose(Command command, string topic, string originalSender, string serverURL, Dictionary<string, int> vectorClock)
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
                }
                else
                {
                    return;
                }
            }

            
            PropagateClose(command, topic, originalSender, vectorClock);
            //From this point message will be delivered
            //Number of fails
            int x = this.maxFaults;
            lock (acks)
            {
                while (acks[command_id].Count < x && acks[command_id].Count < this.tickets.Count - 1)

                {
                    Monitor.Wait(acks);
                }
                Monitor.PulseAll(acks);
            }



            lock (this.MyVectorClock)
            {
                printClocks(originalSender, vectorClock, this.MyVectorClock);
                while (!checkClock(originalSender, vectorClock, this.MyVectorClock))
                {
                    Monitor.Wait(this.MyVectorClock);
                }

                this.MyVectorClock[originalSender] = Math.Max(vectorClock[originalSender], this.MyVectorClock[originalSender]);

                lock (this.PendingCommands)
                {
                    if (!this.PendingCommands.ContainsKey(topic))
                        this.PendingCommands.Add(topic, command);
                }
                Console.WriteLine("adicionei um close nos pendings");
                Monitor.PulseAll(this.MyVectorClock);
            }

        }

        public static bool checkClock(string serverURL, Dictionary<string, int> clock1, Dictionary<string, int> clock2)
        {

            foreach (KeyValuePair<string, int> entry in clock1)
            {
                if (!clock2.ContainsKey(entry.Key))
                {
                    return false;
                }
                else if (serverURL != entry.Key && entry.Value > clock2[entry.Key])
                {
                    return false;
                }
                else if (serverURL == entry.Key && entry.Value != clock2[entry.Key] + 1 && entry.Value != clock2[entry.Key])
                {
                    return false;
                }
            }
            return true;
        }

        public void AddServer(String serverURL)
        {
            lock (this.MyVectorClock)
            {
                lock (this.Available_Servers)
                {
                    lock (tickets)
                    {
                        this.tickets.Add(serverURL, -1);
                    }
                    this.MyVectorClock.Add(serverURL, 0);
                    this.Available_Servers.Add(serverURL);
                    Monitor.PulseAll(this.MyVectorClock);
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

            if (masterServer == this.myURL)
            {
                Console.WriteLine("IM THE MASTER!!");
            }

            Console.WriteLine("Server id: " + id + " Server url: " + this.myURL);
            //Console.WriteLine("Maximum Faults: " + maxFaults);
            //Console.WriteLine("Maximum Delay: " +  maxDelay);
            //Console.WriteLine("Minimum Delay: " +  minDelay);

            Console.WriteLine("Servers that are alive: ");
            lock (this.Available_Servers)
            {
                if (this.Available_Servers.Count != 0)
                {
                    foreach (string serverURL in this.Available_Servers)
                    {
                        if(serverURL != "failed")
                            Console.WriteLine("Server: " + serverURL);
                    }
                }
                else { Console.WriteLine("No Servers Available"); }
            }

            Console.WriteLine("Clients:");
            lock (this.Clients)
            {
                if (Clients.Keys.Count != 0)
                {
                    foreach (string client in Clients.Keys)
                    {
                        Console.WriteLine("\t" + client);
                    }
                }
                else { Console.WriteLine("\t No Clients Available"); }
            }

            Console.WriteLine("Proposals: ");

            lock (this.Proposals)
            {
                if (Proposals.Keys.Count != 0)
                {
                    foreach (string s in Proposals.Keys)
                    {
                        Console.WriteLine("\t" + s);
                        Console.WriteLine("\t\t\t " + Proposals[s].PrintInfo());
                    }
                }
                else { Console.WriteLine("\t No Proposals Available"); }
            }

            lock (this.Meetings)
            {
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
                }
                else { Console.WriteLine("\t No Locations Available"); }
            }

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

        public void Ping()
        {
            lock (this) //FIXME freeze entre servidores
            {
                while (freeze)
                {
                    Monitor.Wait(this);
                }
                Monitor.PulseAll(this);
            }
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
                if (!clock2.ContainsKey(entry.Key))
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
            lock (this) //FIXME freeze entre servidores
            {
                while (freeze)
                {
                    Monitor.Wait(this);
                }
                Monitor.PulseAll(this);
            }

            Console.WriteLine("A ticket was requested by " + serverURL + "for topic " + topic);
            lock (this.MyVectorClock)
            {
                printClocks(serverURL, vectorClock, this.MyVectorClock);
                while (!checkClock(serverURL, vectorClock, this.MyVectorClock))
                {
                    Monitor.Wait(this.MyVectorClock);
                }

                lock (this.PendingCommands) {

                    lock (this.Tickets)
                    {

                        if (this.Tickets.ContainsKey(topic) && serverURL != this.myURL)
                        {
                            Monitor.PulseAll(this.MyVectorClock);
                            Monitor.PulseAll(this.Tickets);
                            return 0; 
                        } else if (this.Tickets.ContainsKey(topic) && serverURL == this.myURL)
                        {
                            string url = Tickets[topic].ServerURL;
                            try
                            {
                                url = Tickets[topic].ServerURL;
                                ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
                                si.Ping();
                                return 0;
                            }
                            catch (SocketException)
                            {
                                
                                lock (this.Available_Servers)
                                {
                                    if(this.Available_Servers.Contains(url))
                                    Console.WriteLine("O servidor " + url + " crashou.Vou remove-lo4");
                                    this.Available_Servers.Remove(url);
                                }
                            }
                            Monitor.PulseAll(this.MyVectorClock);
                            Monitor.PulseAll(this.Tickets);
                            return ticket;
                        }

                        ticket++;
                        this.Tickets.Add(topic, new Ticket(ticket, serverURL, this.PendingCommands[topic]));
                        Monitor.PulseAll(this.MyVectorClock);
                        Monitor.PulseAll(this.Tickets);
                        return ticket;
                    }
                }
            }
        }

        private void PropagateTicket(int ticket, string topic, string originalSender, AbstractMeeting am, Dictionary<string, int> vectorClock)
        {
            lock (this.Available_Servers)
            {
                Thread[] pool = new Thread[this.Available_Servers.Count - 1];
                int i = 0;
                for(i = 1; i < this.Available_Servers.Count; i++)
                {
                    if (this.myURL != this.Available_Servers[i] && this.Available_Servers[i] != "failed")
                    {
                        string url = this.Available_Servers[i];
                        pool[i - 1] = new Thread(() => DoPropagateTicket(url, originalSender, ticket, topic, am, vectorClock));
                        pool[i - 1].Start();
                    }
                }
                Monitor.PulseAll(this.Available_Servers);
            }
        }

        private void DoPropagateTicket(string url, string originalSender, int ticket, string topic, AbstractMeeting am, Dictionary<string, int> vectorClock)
        {
            lock (this) //FIXME freeze entre servidores
            {
                while (freeze)
                {
                    Monitor.Wait(this);
                }
                Monitor.PulseAll(this);
            }
            try
            {
                ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), url);
                si.ReceiveTicketResult(topic, originalSender, this.myURL, ticket, am, vectorClock);
            }
            catch (SocketException)
            {
                Console.WriteLine("O servidor " + url + " crashou.Vou remove-lo5");
                lock (this.Available_Servers) //is this the fix??
                {
                    this.WriteFailed(url);
                    //this.Available_Servers.Remove(url);
                }

                lock (tickets)
                {
                    tickets.Remove(url);
                    Monitor.PulseAll(tickets);
                }

                if (masterServer == url)
                {
                    //Start Leader_Election
                    Thread thread = new Thread(() => leader_election("LE", this.lastTicket, this.myURL, this.myURL, url));
                    thread.Start();
                }
            }
        }

        public void ReceiveTicketResult(string topic, string originalSender, string serverURL, int ticket, AbstractMeeting am, Dictionary<string, int> vectorClock)
        {

            //Reliable_BroadCast
            string msg_id = topic + " " + ticket;
            lock (acks)
            {
                if (acks.ContainsKey(msg_id))
                {
                    //adds server to the acks of the message
                    acks[msg_id].Add(serverURL);
                }
                else
                {
                    acks.Add(msg_id, new List<string>());
                    acks[msg_id].Add(serverURL);
                }
                Monitor.PulseAll(acks);  //Wake every spleeping thread that are waiting for acks
            }

            lock (received_commands)
            {
                //If not command received broadcast to everyone
                if (!received_commands.Contains(msg_id))
                {
                    received_commands.Add(msg_id);
                }
                else
                {
                    return;
                }
            }

            PropagateTicket(ticket, topic, originalSender, am, vectorClock);

            //From this point message will be delivered
            //Number of fails
            int x = this.maxFaults;
            lock (acks)
            {
                while (acks[msg_id].Count < x && acks[msg_id].Count < this.Available_Servers.Count)
                {
                    Monitor.Wait(acks);
                }
                Monitor.PulseAll(acks);
            }

            Dictionary<string, int> clock;
            lock (this.MyVectorClock)
            {
                printClocks(originalSender, vectorClock, this.MyVectorClock);
                while (!checkClock(originalSender, vectorClock, this.MyVectorClock))
                {
                    Monitor.Wait(this.MyVectorClock);
                }


                this.MyVectorClock[originalSender] = Math.Max(vectorClock[originalSender], this.MyVectorClock[originalSender]);
                clock = new Dictionary<string, int>(this.MyVectorClock);
                Monitor.PulseAll(this.MyVectorClock);
            }

            List<Command> pendingJoins = new List<Command>();
            


            lock (this.Tickets)
            {
                while (ticket - 1 > lastTicket)
                {
                    Monitor.Wait(this.Tickets);
                }
                Monitor.PulseAll(this.Tickets);
            }

            Console.WriteLine("Receiving ticket " + ticket + " result for topic "+ topic);
            
            lock (this.Proposals)
            {
                lock (this.Meetings)
                {
                    lock (this.PendingCommands)
                    {
                        lock (this.Tickets)
                        {
                            if (am.isProposal())
                            {
                                this.Proposals[am.Topic] = (Proposal)am;
                            }
                            else
                            {
                                Meeting m = (Meeting)am;
                                this.Meetings[m.Slot.Location.Local].Meetings.Remove(m);
                                this.Meetings[m.Slot.Location.Local].addMeeting(m); //FIXME must replace, not add
                                AbstractMeeting p = null;
                                if (this.Proposals.ContainsKey(am.Topic))
                                {
                                    p = this.Proposals[am.Topic];
                                    this.Proposals.Remove(am.Topic);
                                }
                                else
                                {
                                    foreach (Meeting meeting in this.Meetings[m.Slot.Location.Local].Meetings)
                                    {
                                        if (meeting.Topic == m.Topic)
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
                                    if (notIn && CouldAttend(m, a))
                                    {
                                        List<string> slots = new List<string>();
                                        foreach (Slot s in a.Available_slots)
                                        {
                                            slots.Add(s.Location.Local + "," + s.Date);
                                        }
                                        DoJoin command  = new DoJoin(m.Topic, a.Name, slots, clock, this.myURL);
                                        command.setCommandId("late " + command.getCommandId()); 
                                        pendingJoins.Add(command);
                                        Console.WriteLine("detected");
                                    }

                                }
                            }
                                
                            if (!this.Tickets.ContainsKey(topic))
                            {
                                this.Tickets.Add(topic, new Ticket(ticket, serverURL, this.PendingCommands[topic]));
                            }
                            this.PendingCommands.Remove(topic);
                            lastTicket = Math.Max(lastTicket, ticket);
                            Monitor.PulseAll(this.Tickets);
                        }
                    }
                }

            }

            foreach (Command command in pendingJoins)
            {
                ExecuteTicket(command);
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

        public void WriteFailed(String url)
        {
            for (int i = 0; i < this.Available_Servers.Count; i++)
            {
                if (this.Available_Servers[i] == url)
                    this.Available_Servers[i] = "failed";
            }
        }

        private void ExecuteTicket(Command command)
        {
            string topic = command.getCommandId();
            int newTicket;
            Dictionary<string, int> clock = null;
            Dictionary<string, int> vectorClock;
            bool propagate = false;
            lock (this.MyVectorClock)
            {
                lock (this.PendingCommands)
                {
                    lock (received_commands) //Adding my message to my received_commands
                    {
                        if (!received_commands.Contains(topic))
                            received_commands.Add(topic);
                    }
                    if (!this.PendingCommands.ContainsKey(topic))
                    {
                        this.PendingCommands.Add(topic, command);
                        this.MyVectorClock[this.myURL]++;
                        clock = new Dictionary<string, int>(this.MyVectorClock);
                        propagate = true;
                    }
                }
            }
            if (propagate)
                PropagateClose(command, topic, this.myURL, clock);
            lock (this.MyVectorClock)
            {
                lock (this.PendingCommands)
                {
                    this.MyVectorClock[this.myURL]++;
                    if (masterServer == this.myURL) //Se o master estiver crashado nao faz isto
                    {
                        newTicket = GetTicket(topic, this.myURL, this.MyVectorClock);
                        if (newTicket == 0)
                        {
                            this.MyVectorClock[this.myURL]--;
                            Monitor.PulseAll(this.MyVectorClock);
                            return;
                        }
                    }
                    else
                    {
                        try
                        {
                            ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), masterServer);
                            newTicket = si.GetTicket(topic, this.myURL, this.MyVectorClock);
                            if (newTicket == 0)
                            {
                                this.MyVectorClock[this.myURL]--;
                                Monitor.PulseAll(this.MyVectorClock);
                                return;
                            }
                            lock (this.Tickets)
                            {
                                this.Tickets.Add(topic, new Ticket(newTicket, this.myURL, this.PendingCommands[topic]));
                                Monitor.PulseAll(this.Tickets);
                            }
                        } catch (SocketException)
                        {
                            this.MyVectorClock[this.myURL]--;
                            Console.WriteLine("O servidor master " + masterServer + " crashou.Vou remove-lo6");
                            lock (this.Available_Servers)
                            {
                                WriteFailed(masterServer);
                            }

                            lock (tickets)
                            {
                                tickets.Remove(masterServer);
                                Monitor.PulseAll(tickets);
                            }
                            Thread thread = new Thread(() => leader_election("LE", lastTicket, this.myURL, this.myURL, masterServer));
                            thread.Start();
                            Monitor.PulseAll(this.MyVectorClock);
                            return;
                        }
                    }
                }
                vectorClock = new Dictionary<string, int>(this.MyVectorClock);
                Monitor.PulseAll(this.MyVectorClock);
            }

            lock (this.Tickets)
            {
                while (newTicket - 1 > lastTicket)
                {
                    Monitor.Wait(this.Tickets);
                }
                Monitor.PulseAll(this.Tickets);
            }

            Console.WriteLine("Executing ticket " + newTicket + " " + topic);




            AbstractMeeting am = command.Execute(this);
            lock (this.PendingCommands)
            {
                lock (this.Tickets)
                {
                    lastTicket = Math.Max(newTicket, lastTicket);
                    this.PendingCommands.Remove(topic);
                    Monitor.PulseAll(this.Tickets);
                }
            }

            //Before updateServers (reliable broadcast)
            string msg_id = topic + " " + newTicket;
            lock (received_commands) //Adding my message to my received_commands
            {
                received_commands.Add(msg_id);
            }

            PropagateTicket(newTicket, topic, this.myURL, am, vectorClock);
        }


        //
        //                      L E A D E R   E L E C T I O N
        //

        /*
         TODO:
            1-  Acabar leader_election - DONE
            2- Make Sure que estou a remover o que foi crashado - DONE
            3- Bloquear tickets quando estou LE = "LE"
            4- Chamar o leader election - DONE
            5 - Depois de LE continuar o processo 
            6- Testar 
        */

        public void leader_election(string LE, int myTicket, string originalSender, string sender, string crashedServer)
        {
            //Detetei falha e vou chamar todos os available servers
            //RB_Send, e depois o RB ha de chamar received_LE
            //Add my message before broadcast
            string msg_id = this.myURL + myTicket;
            lock (received_commands)
            {
                received_commands.Add(msg_id);
            }

            //Update global LE
            lock (LE)
            {
                if (this.LE != "LE")
                    this.LE = "LE";

                Monitor.PulseAll(LE);
            }

            lock (this.Available_Servers)
            {
                Thread[] pool = new Thread[this.Available_Servers.Count - 1];
                int i = 0;
                if (sender == this.myURL) //FIXME hardcoded delay
                {
                    this.waitBetweenRequests();
                }
                //printServers();
                foreach (string serverURL in this.Available_Servers)
                {
                    if (this.myURL != serverURL && serverURL != "failed")
                    {
                        string receiver = serverURL;
                        pool[i] = new Thread(() => PropagateLE("LE", myTicket, originalSender, sender, receiver, crashedServer)); //sender  -> myUrl
                        pool[i].Start();
                        i++;
                    }
                }
            }
        }

        public void PropagateLE(string LE, int myTicket, string originalSender, string sender, string receiver, string crashedServer)
        {
            try
            {//FIXME
                ServerInterface si = (ServerInterface)Activator.GetObject(typeof(ServerInterface), receiver);
                si.received_LE(LE, myTicket, originalSender, sender, crashedServer);
            }
            catch (SocketException e)
            {
                //Se crashar remover dos available servers e dos tickets
                Console.WriteLine("O server " + receiver + " crashou! Vou Remover");
                //Console.WriteLine(e);
                lock (this.Available_Servers) //is this the fix??
                {
                    this.Available_Servers.Remove(receiver);
                }

                lock (tickets)
                {
                    this.tickets.Remove(receiver);
                    Monitor.PulseAll(tickets);
                }

            }
        }

        public void received_LE(string LE, int received_ticket, string originalSender, string sender, string crashedServer)
        {

            //Vou ver se ja recebi esta mensagem
            string msg_id = originalSender + received_ticket;
            lock (received_commands)
            {
                //If not command received broadcast to everyone
                if (!received_commands.Contains(msg_id))
                {
                    received_commands.Add(msg_id);
                }
                else
                {
                    return;
                }
            }

            //Update global LE
            lock (LE)
            {
                if (this.LE != "LE")
                    this.LE = "LE";

                Monitor.PulseAll(LE);
            }

            //Remove Crashed Server
            lock (Available_Servers)
            {
                Available_Servers.Remove(crashedServer);
            }
            lock (tickets)
            {
                tickets.Remove(crashedServer);
                Monitor.PulseAll(tickets);
            }

            lock (tickets) //Save the ticket from server p
            {
                tickets[originalSender] = received_ticket;
                Monitor.PulseAll(tickets);  //Wake every spleeping thread that are waiting for acks
            }
            Console.WriteLine("BROADCAST DO QUE ACABEI DE RECEBER");
            leader_election("LE", received_ticket, originalSender, this.myURL, crashedServer); //RB da mensagem que acabei de receber

            //RB do meu ticket
            int myTicket = this.lastTicket;
            lock (tickets) //Add my own ticket
            {
                tickets[this.myURL] = myTicket;
            }
            Console.WriteLine("BROADCAST DO MEU TICKET");
            leader_election("LE", myTicket, this.myURL, this.myURL, crashedServer);

            lock (tickets)
            {
                while (notAllTickets())
                {
                    Monitor.Wait(tickets);
                }
                Monitor.PulseAll(tickets);
            }

            //Decide leader
            (string new_leader, int new_ticket) = decide_new_leader();
            lock (masterServer)
            {
                this.masterServer = new_leader;
            }
            //this.lastTicket = new_ticket;

            //Reset ticket
            Console.WriteLine("Reset dos tickets");
            Console.WriteLine(masterServer);
            //lock (tickets)
            //{
            //    reset_ticket();
            //}

            //lock (tickets)
            //{
            //    foreach (string s in tickets.Keys)
            //    {
            //        tickets[s] = -1;
            //    }

            //}

            Console.WriteLine("Nao crashei!!");
            lock (LE)
            {
                this.LE = "DONE";
                Monitor.PulseAll(LE);
            }

            Console.WriteLine("JA HA NOVO LEADER");

        }

        public Boolean notAllTickets()
        {
            lock (tickets)
            {
                foreach (string s in tickets.Keys)
                {
                    if (tickets[s] == -1)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public (string, int) decide_new_leader()
        {
            int temp_i = -1;
            string temp_s = "";
            lock (tickets)
            {
                foreach (string s in tickets.Keys)
                {
                    if (tickets[s] > temp_i)
                    {
                        temp_i = tickets[s];
                        temp_s = s;
                    }
                    else if (tickets[s] == temp_i)
                    {
                        if (string.Compare(temp_s, s, comparisonType: StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            temp_s = s;
                        }
                    }
                }
            }
            return (temp_s, temp_i);
        }

        public void reset_ticket()
        {
           
        }

        public void printServers()
        {

            Console.WriteLine("Servers that are alive: ");
            lock (this.Available_Servers)
            {
                if (this.Available_Servers.Count != 0)
                {
                    foreach (string serverURL in this.Available_Servers)
                    {
                        Console.WriteLine("Server: " + serverURL);
                    }
                }
                else { Console.WriteLine("No Servers Available"); }
            }
        }
            
        private void checkPendings()
        {
            while (true)
            {
                Thread.Sleep(500);
                lock (this.MyVectorClock)
                {
                    lock (this.PendingCommands)
                    {
                        if (this.PendingCommands.Count > 0)
                        {
                            Random random = new Random();

                            List<string> topics = Enumerable.ToList(this.PendingCommands.Keys);
                            string topic = topics[random.Next(0, topics.Count - 1)];
                            
                            ExecuteTicket(this.PendingCommands[topic]);
                            
                        }
                        
                    }
                }
                
            }
        }

    }

}

