using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VersionedTimer
{
    /// <summary>
    /// Specifies special timeout values for the VersionedTimer class.
    /// </summary>
    public static class VTimeout
    {
        /// <summary>
        /// The maximum TimeSpan that may be specified for a timeout or period parameter. Exactly
        /// equal to 315,569,260,800 seconds, which is approximately 10,000 years.
        /// </summary>
        public static readonly TimeSpan MaxTimeout;

        /// <summary>
        /// Specifies an infinite timeout as a TimeSpan. Has the value of -1 milliseconds.
        /// </summary>
        /// <remarks>
        /// This field has the same value as its twin in <see cref="Timeout"/>.
        /// </remarks>
        public static readonly TimeSpan InfiniteTimeSpan;

        /// <summary>
        /// Specifies an infinite timeout as an integral number of milliseconds. Has the value of -1 milliseconds.
        /// </summary>
        /// <remarks>
        /// This field has the same value as its twin in <see cref="Timeout"/>.
        /// </remarks>
        public const int Infinite = -1;

        static VTimeout()
        {
            // We need to leave room at the top of the TimeSpan to store elapsed, so we need some
            // form of sanity check for the timeouts we allow. DateTime currently is limited to the
            // last second of Dec 31st, 9999. Taking a page from them, limit the timespan to 10000
            // years, but define it in seconds so that the user can have an exact number to rely on.
            //
            // This still leaves us 19,000 years of headroom (TimeSpan.MaxValue is about 29,000
            // years). We need a little headroom above the max parameter because we perform internal
            // calculations using values that might be above it if our timer's timestamp source has
            // had some time to chug along.
            //
            // One year is about 365.2422 days:
            // - https://pumas.jpl.nasa.gov/examples/index.php?id=46
            //
            // 10000 years, 365.2422 days/year, 86400 seconds/day == 315,569,260,800 seconds in an
            // average 10000 years.

            MaxTimeout = TimeSpan.FromSeconds( 315569260800.0 );

            InfiniteTimeSpan = Timeout.InfiniteTimeSpan;
        }

    }
}
