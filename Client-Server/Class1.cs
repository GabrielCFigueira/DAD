using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project
{
    public interface ClientInterface
    {
        void CreateProposal(String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees);

        void ListMeetings();

        void PrintAllMeetings(String meetings);

        void JoinMeeting(String topic, List<String> slots);

        void CloseMeeting(String topic);
        void Connect(String URL);

        void AddProposal(Proposal p);

        void UpdateMeetings(Dictionary<String, Proposal> proposals, Dictionary<Location, List<Meeting>> meetings);

    }

    public interface ServerInterface
    {
        void CreateProposal(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees);

        void ListMeetings();

        void JoinMeeting(String topic, String userName, List<String> slots);

        void CloseMeeting(String topic);

        void Connect(String URL,String userName);
    }


    public abstract class AbstractMeeting
    {
        int version;

        public int Version
        {
            get { return version; }
            set { version = value; }
        }

        public abstract Boolean isProposal();

        public abstract void PrintInfo();
    }
    public class Meeting:AbstractMeeting
    {
        String coordinator;
        String topic;
        int min_attendees;
        int n_slots;
        int n_invitees;
        Slot slot;
        List<String> invitees;
        Boolean isScheduled; // True means scheduled, False means cancelled
        List<Attendee> attendees;

        public String Coordinator
        {
            get { return coordinator; }
            set { coordinator = value; }
        }

        public String Topic
        {
            get { return topic; }
            set { topic = value; }
        }

        public int Min_attendees
        {
            get { return min_attendees; }
            set { min_attendees = value; }
        }

        public int N_slots
        {
            get { return n_slots; }
            set { n_slots = value; }
        }

        public int N_invitees
        {
            get { return n_invitees; }
            set { n_invitees = value; }
        }

        public Slot Slot
        {
            get { return slot; }
            set { slot = value; }
        }

        public List<String> Invitees
        {
            get { return invitees; }
            set { invitees = value; }
        }

        public Boolean IsScheduled
        {
            get { return isScheduled; }
            set { isScheduled = value; }
        }

        public List<Attendee> Attendees
        {
            get { return attendees; }
            set { attendees = value; }
        }

        public Meeting(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, Slot slot, List<String> invitees, int lastVersion)
        {
            this.Coordinator = coordinator;
            this.Topic = topic;
            this.Min_attendees = min_attendees;
            this.N_slots = n_slots;
            this.N_invitees = n_invitees;
            this.slot = slot;
            this.Invitees = invitees;
            this.isScheduled = false;
            this.Attendees = new List<Attendee>();
            this.Version = lastVersion + 1;
        }

        public override Boolean isProposal()
        {
            return false;
        }

        public override void PrintInfo()
        {
            String message = "\r\nMEETING\r\n";
            message += "Coordinator: " + this.Coordinator + "\r\nTopic: " + this.Topic + "\r\nMin_attendees: " + this.Min_attendees + "\r\nN_slots: " + this.N_slots + " \r\nN_invitees: " + this.N_invitees + "\r\nLocal: " + this.Slot.Location;
            message += "\r\nInvitees: ";
            foreach (String s in this.Invitees)
            {
                message += s + " ";
            }
            message += "\r\nState: ";
            if (this.IsScheduled)
                message += "SCHEDULED\r\n";
            if (!this.IsScheduled)
                message += "CANCELLED\r\n";
            message += "\r\nAttendees: ";
            foreach (Attendee a in this.Attendees)
            {
                message += a.Name + ", Available Slots: ";
                foreach (Slot s in a.Available_slots)
                {
                    message += s.Location + "," + s.Date + " ";
                }
            }
            message += "\r\n";
            Console.WriteLine(message);
        }
    }

    public class Attendee
    {
        String name;
        List<Slot> available_slots;

        public Attendee(String Name, List<Slot> Available_slots)
        {
            this.name = Name;
            this.available_slots = Available_slots;
        }

        public String Name
        {
            get { return name; }
            set { name = value; }
        }

        public List<Slot> Available_slots
        {
            get { return available_slots; }
            set { available_slots = value; }
        }
    }


    public class Proposal:AbstractMeeting
    {
        String coordinator;
        String topic;
        int min_attendees;
        int n_slots;
        int n_invitees;
        List<Slot> slots;
        List<String> invitees;
        List<Attendee> attendees;

        public String Coordinator
        {
            get { return coordinator; }
            set { coordinator = value; }
        }

        public String Topic
        {
            get { return topic; }
            set { topic = value; }
        }

        public int Min_attendees
        {
            get { return min_attendees; }
            set { min_attendees = value; }
        }

        public int N_slots
        {
            get { return n_slots; }
            set { n_slots = value; }
        }

        public int N_invitees
        {
            get { return n_invitees; }
            set { n_invitees = value; }
        }

        public List<Slot> Slots
        {
            get { return slots; }
            set { slots = value; }
        }

        public List<String> Invitees
        {
            get { return invitees; }
            set { invitees = value; }
        }

        public List<Attendee> Attendees
        {
            get { return attendees; }
            set { attendees = value; }
        }

        public Proposal(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<Slot> slots, List<String> invitees)
        {
            this.Coordinator = coordinator;
            this.Topic = topic;
            this.Min_attendees = min_attendees;
            this.N_slots = n_slots;
            this.N_invitees = n_invitees;
            this.Slots = slots;
            this.Invitees = invitees;
            this.Attendees = new List<Attendee>();
            this.Version = 1;
        }

        public override Boolean isProposal()
        {
            return true;
        }

        public override void PrintInfo()
        {
            String message = "\r\nPROPOSAL\r\n";
            message += "Coordinator: " + this.Coordinator + "\r\nTopic: " + this.Topic + "\r\nMin_attendees: " + this.Min_attendees + "\r\nN_slots: " + this.N_slots + " \r\nN_invitees: " + this.N_invitees + "\r\nSlots: ";
            foreach (Slot s in this.Slots)
            {
                message += s.Location + "," + s.Date + " ";
            }
            message += "\r\nInvitees: ";
            foreach (String s in this.Invitees)
            {
                message += s + " ";
            }
            message += "\r\nAttendees: ";
            foreach (Attendee a in this.Attendees)
            {
                message += a.Name + ", Available Slots: ";
                foreach (Slot s in a.Available_slots)
                {
                    message += s.Location + "," + s.Date + " ";
                }
            }
            message += "\r\n";
            Console.WriteLine(message);
        }
    }

    public class Room
    {
        String name;
        int capacity;

        public String Name
        {
            get { return name; }
            set { name = value; }
        }

        public int Capacity
        {
            get { return capacity; }
            set { capacity = value; }
        }

        public Room(String name, int capacity)
        {
            this.Name = name;
            this.Capacity = capacity;
        }
    }

    public class Location
    {
        String local;
        List<Room> rooms;

        public String Local
        {
            get { return local; }
            set { local = value; }
        }

        public List<Room> Rooms
        {
            get { return rooms; }
            set { rooms = value; }
        }

        public void addRoom(Room room)
        {
            rooms.Add(room);
        }

        public Location(String local, List<Room> rooms)
        {
            this.Local = local;
            this.Rooms = rooms;
        }
    }

    public class Slot
    {
        Location location;
        String date;
        int votes;

        public Location Location
        {
            get { return location; }
            set { location = value; }
        }

        public String Date
        {
            get { return date; }
            set { date = value; }
        }

        public Slot(Location location, String date)
        {
            this.location = location;
            this.date = date;
            this.votes = 0;
        }
    }
}
