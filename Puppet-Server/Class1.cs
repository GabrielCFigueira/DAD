using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Puppet_Server
{
    public class Class1
    {
    }

    public interface IPuppet
    {
        void AddRoom(String location, int capacity, String room_name);

        void Status();
        void Crash(String serverID);
        void Freeze(String serverID);
        void Unfreeze(String serverID);
    }

    public interface IServerPuppet
    {
        void AddRoom(String location, int capacity, String room_name);

        void Status();
        void Crash();
        void Freeze(String server_id);
        void Unfreeze(String server_id);

        void AddServer(String serverURL);

        void shutdown();
    }
}
