using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RocketScience.Services.Multiplay
{
    internal interface IServerConfigReader
    {
        public ServerConfig LoadServerConfig();
    }
}
