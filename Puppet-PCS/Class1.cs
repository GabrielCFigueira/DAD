using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Puppet_PCS
{
    public interface IPCS
    {
        void createServer(string serverID, string url, string maxFaults, string minDelay, string maxDelay);

        void createClient(string username, string url, string serverURL, string pathScriptFile);

    }
}
