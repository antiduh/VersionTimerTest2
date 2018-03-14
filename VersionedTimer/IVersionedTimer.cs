using System;

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