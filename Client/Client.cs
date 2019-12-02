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
    class Client
    {

        static void Main(string[] args)
        {
            try
            {
                String userName = args[1];
                String clientUrl = args[2];
                String serverUrl = args[3];
                String scriptFileName = args[4];

                Uri clientUri = new Uri(clientUrl);

                TcpChannel channel = new TcpChannel(clientUri.Port);
                ChannelServices.RegisterChannel(channel, false);

                Console.WriteLine(userName);

                ClientImpl MeetingClient = new ClientImpl(userName);
                RemotingServices.Marshal(MeetingClient, clientUri.Segments[1], typeof(ClientImpl));
                ServerInterface server = (ServerInterface)Activator.GetObject(typeof(ServerInterface), serverUrl);
                server.Connect(clientUrl,userName);

                bool interactive = true;
                StreamReader file = new StreamReader(scriptFileName);
                string command = file.ReadLine();
                Console.Write(" h - imprimir esta ajuda\r\n n - executar o próximo comando\r\n l - imprimir próximo comando\r\n r - correr todos os comandos restantes\r\n");


                while (command != null)
                {
                    if (interactive)
                    {
                        Console.Write("> ");
                        switch (Console.ReadLine())
                        {
                            case "n":
                                Console.WriteLine(command);
                                MeetingClient.ReadCommands(command);
                                command = file.ReadLine();
                                break;
                            case "l":
                                Console.WriteLine(command);
                                break;
                            case "r":
                                interactive = false;
                                break;
                            case "h":
                                Console.Write(" h - imprimir esta ajuda\r\n n - executar o próximo comando\r\n l - imprimir próximo comando\r\n r - correr todos os comandos restantes\r\n");
                                break;
                        }
                    }
                    else
                    {
                        MeetingClient.ReadCommands(command);
                        command = file.ReadLine();
                    }
                }

                file.Close();


                while (true)
                {
                    command = Console.ReadLine();
                    MeetingClient.ReadCommands(command);
                }
            } catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
                Console.ReadLine();
            }

        }
    }


    class ClientImpl : MarshalByRefObject, ClientInterface
    {
        String UserName;
        ServerInterface Server;
        Dictionary<String, AbstractMeeting> Meetings;
        Dictionary<String, String> Clients;
        Dictionary<String, String> ClientsSent;

        public ClientImpl(String userName)
        {
            this.UserName = userName;
            this.Meetings = new Dictionary<string, AbstractMeeting>();
            this.Clients = new Dictionary<String, String>();
            this.ClientsSent = new Dictionary<String, String>();
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        public void ReadCommands(String command)
        {
            string[] commandParams = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (commandParams.Length == 0)
                return;
            switch (commandParams[0])
            {
                case "list":
                    this.ListMeetings();
                    break;
                case "create":
                    String meetingTopic = commandParams[1];
                    int min_attendees = Int32.Parse(commandParams[2]);
                    int n_slots = Int32.Parse(commandParams[3]);
                    int n_invitees = Int32.Parse(commandParams[4]);
                    List<String> meeting_slots = new List<String>();
                    List<String> invitees = new List<String>();
                    for (int i = 5; i < n_slots + 5; i++)
                    {     
                        string slot = commandParams[i];
                        meeting_slots.Add(slot);
                    }
                    if (n_invitees != 0) { 
                        for (int i = 5 + n_slots; i < n_invitees + 5 + n_slots; i++)
                        {
                            string invitee = commandParams[i];
                            invitees.Add(invitee);
                        }
                    }
                    this.CreateProposal(meetingTopic, min_attendees, n_slots, n_invitees, meeting_slots, invitees);
                    break;
                case "join":
                    String topic = commandParams[1];
                    int slot_count = Int32.Parse(commandParams[2]);
                    List<String> slots = new List<String>();
                    for (int i = 3; i < slot_count + 3; i++)
                    {
                        string slot = commandParams[i];
                        slots.Add(slot);
                    }
                    this.JoinMeeting(topic, slots);
                    break;
                case "close":
                    String meeting_topic = commandParams[1];
                    this.CloseMeeting(meeting_topic);
                    break;
                case "wait":
                    int interval = Int32.Parse(commandParams[1]);
                    Thread.Sleep(interval);
                    break;
                default:
                    Console.WriteLine("Wrong Command");
                    break;
            }
        }

        public void CloseMeeting(String topic)
        {
            Server.CloseMeeting(this.UserName,topic);
        }

        public void CreateProposal(String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees)
        {
            Server.CreateProposal(this.UserName, topic, min_attendees, n_slots, n_invitees, slots, invitees);
        }

        public void JoinMeeting(String topic, List<String> slots)
        {
            Server.JoinMeeting(topic, this.UserName, slots);
        }

        public void ListMeetings()
        {
            Server.ListMeetings(this.UserName);
        }

        public void Connect(String server_URL)
        {
            Server = (ServerInterface)Activator.GetObject(
                typeof(ServerInterface),
                server_URL);
            Console.WriteLine("Registei o servidor. Sou o/a " + this.UserName);
        }

        public void UpdateUsers(Dictionary<String, String> clients)
        {
            this.Clients = clients;
        }

        public void PrintAllMeetings(string meetings)
        {
            Console.WriteLine(meetings);
        }

        public void AddProposal(Proposal p)
        {
            this.Meetings.Add(p.Topic, p);
        }

        public void Gossip(Proposal p, int actualRound, int totalRounds)
        {
            this.ClientsSent = new Dictionary<String, String>();
            Console.WriteLine("Comecei o Gossip");
            foreach (KeyValuePair<String, String> entry in this.Clients)
            {
                Console.WriteLine(entry.Value);
            }

            Console.WriteLine("Actual Round: " + actualRound);
            Console.WriteLine("Total rounds: " + totalRounds);

            if (actualRound > totalRounds)
            {
                Console.WriteLine("Acabou o gossip");
                return;
            }

            AbstractMeeting am = (AbstractMeeting)p;

            lock (this.Meetings)
            {
                AbstractMeeting p2;
                this.Meetings.TryGetValue(am.Topic, out p2);
                if (p2 == null && am.N_invitees == 0)
                {
                    Console.WriteLine("Este proposal é aberta, sou o/a " + this.UserName + " e passei a conhecer a meeting com o Topic " + am.Topic);
                    this.Meetings.Add(am.Topic, am);
                }
                else if (p2 == null && am.N_invitees != 0 && (am.Invitees.Contains(this.UserName) || this.UserName == am.Coordinator))
                {
                    Console.WriteLine("Este proposal é fechada e sou convidado/a, sou o/a " + this.UserName + " e passei a conhecer a meeting com o Topic " + am.Topic);
                    this.Meetings.Add(am.Topic, am);
                }
            }

            int numberOfMessages = 2;
            Console.WriteLine("Vou mandar " + numberOfMessages + " mensagens");
            Thread[] pool = new Thread[numberOfMessages];
            for (int i = 0; i < numberOfMessages; i++)
            {
                pool[i] = new Thread(() => DoSpreadMessage(p, actualRound, totalRounds));
                pool[i].Start();
            }
        }

        public void DoSpreadMessage(Proposal p, int actualRound, int totalRounds)
        {
            ServerInterface s = this.Server;
            var chosenClientNameAndURL = s.getRandomClientName();
            lock (this.ClientsSent)
            {
                while (chosenClientNameAndURL.Item1 == this.UserName || this.ClientsSent.ContainsKey(chosenClientNameAndURL.Item1))
                {
                    chosenClientNameAndURL = s.getRandomClientName();
                }

                this.ClientsSent.Add(chosenClientNameAndURL.Item1, chosenClientNameAndURL.Item2);
            }

            Console.WriteLine("Mandei ao/a " + chosenClientNameAndURL);
            ClientInterface chosenClient = (ClientInterface)Activator.GetObject(typeof(ClientInterface), chosenClientNameAndURL.Item2);
            chosenClient.Gossip(p, actualRound + 1, totalRounds);
        }

        public void UpdateMeetings(Dictionary<string, Proposal> proposals, Dictionary<string, LocationMeetings> meetings)
        {
            foreach(KeyValuePair<String, Proposal> entry in proposals)
            {
                //Proposal que veio do servidor
                Proposal p1 = entry.Value;

                //Respetivo proposal no cliente
                AbstractMeeting p2;
                lock (this.Meetings)
                {
                    this.Meetings.TryGetValue(p1.Topic, out p2);
                    if (p2 != null && p1.Version > p2.Version)  //  || p2 == null  shouldnt be necessary in the condition TODO why?
                    {
                        this.Meetings[p1.Topic] = p1;
                    }
                }
            }


            foreach (KeyValuePair<string, LocationMeetings> entry in meetings)
            {
                foreach (Meeting m1 in entry.Value.Meetings) { 


                    //Respetivo meeting no cliente
                    AbstractMeeting m2;
                    lock (this.Meetings)
                    {
                        this.Meetings.TryGetValue(m1.Topic, out m2);
                        if ((m2 != null && m1.Version > m2.Version) || m2 == null) //  || m2 == null  shouldnt be necessary in the condition TODO why?
                        {
                            this.Meetings[m1.Topic] = m1;
                        }
                    }
                }
            }

            foreach (KeyValuePair<String, AbstractMeeting> entry in Meetings)
            {
                AbstractMeeting m = entry.Value;
                Console.WriteLine(m.PrintInfo());
            }
        }
    }
}
