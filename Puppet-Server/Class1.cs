using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Puppet_Server
{

    public interface IServerPuppet
    {
        void AddRoom(String location, int capacity, String room_name);

        void Status();
        void Crash();
        void Freeze();
        void Unfreeze();

        void AddServer(String serverURL);

        void shutdown();
    }
}
