using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project
{
    public interface ClientInterface
    {
        void CreateProposal(String topic, int min_attendees, int n_slots, int n_invitees, List<Slot> slots, List<String> invitees);

        void ListMeetings();

        void PrintAllMeetings(String meetings);

        void JoinMeeting(String topic, List<Slot> slots); //n_slots necessary?

        void CloseMeeting(String topic);
        void Connect(String URL);

    }

    public interface ServerInterface
    {
        void CreateProposal(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<Slot> slots, List<String> invitees);

        void ListMeetings();

        void JoinMeeting(String topic, String userName, List<Slot> slots);

        void CloseMeeting(String topic);

        void Connect(String URL);
    }


    public class Meeting
    {
        String coordinator;
        String topic;
        int min_attendees;
        int n_slots;
        int n_invitees;
        Slot slot;
        List<String> invitees;
        Boolean isScheduled; // True means scheduled, False means cancelled
        Boolean isCancelled; // True means cancelled, False means schedulled
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
        //public Boolean IsCancelled
        //{
        //    get { return isCancelled; }
        //    set { isCancelled = value; _ = isScheduled != value; }  //TODO testar isto!
        //}

        public List<Attendee> Attendees
        {
            get { return attendees; }
            set { attendees = value; }
        }

        public Meeting(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, Slot slot, List<String> invitees)
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


    public class Proposal
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
