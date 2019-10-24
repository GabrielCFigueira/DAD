using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Puppet_PCS
{
    public interface IPCS
    {
        void createServer(string serverID, int port, int maxFaults, int minDelay, int maxDelay);

        void createClient(string username, int port, string serverURL, string pathScriptFile);

    }
}
