using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VersionedTimer
{
    internal interface IVersionedTimer
    {
        TimeSpan Period { get; set; }

        long Version { get; set; }

        TimeSpan NextTimeout { get; set; }

        void FireTimerCallback( long version );
        
    }
}
