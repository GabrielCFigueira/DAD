using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project
{
    public interface ClientInterface
    {
        void CreateMeeting(String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees);

        void ListMeetings();

        void PrintAllMeetings(String meetings);

        void JoinMeeting(String topic);

        void CloseMeeting(String topic);
        void Connect(String URL);

    }

    public interface ServerInterface
    {
        void CreateMeeting(String coordinator, String topic, int min_attendees, int n_slots, int n_invitees, List<String> slots, List<String> invitees);

        void ListMeetings();

        void JoinMeeting(String topic);

        void CloseMeeting(String topic);

        void Connect(String URL);
    }
}
