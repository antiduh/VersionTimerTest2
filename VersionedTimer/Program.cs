using System;
using System.Threading;

namespace VersionedTimer
{
    public class Program
    {
        private static int count = 0;

        private static VersionedTimer<int> myTimer;

        public static void Main( string[] args )
        {
            VersionedTimer<int> timer = new VersionedTimer<int>( 0, Callback );
            myTimer = timer;

            timer.Change( 500, 500, 0 );

            Thread.Sleep( 2500 );

            timer.Change( Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan, 1 );

            Thread.Sleep( 5000 );

            timer.Change( 2000, Timeout.Infinite, 2 );

            Thread.Sleep( 5000 );

            timer.Dispose();
        }

        private static void Callback( int state, long version )
        {
            Console.WriteLine( "State: {0}, Version: {1}", state, version );
            count++;

            if( count > 10 )
            {
                myTimer.Change( Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan, 1 );
            }
        }

        private static void Callback( object state )
        {
        }
    }
}