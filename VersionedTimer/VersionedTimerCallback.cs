using System;

namespace VersionedTimer
{
    /// <summary>
    /// Represents the method that handles callbacks from a VersionedTimer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="state">
    /// An value containing application-specific information relevant to the method invoked by
    /// this delegate.
    /// </param>
    /// <param name="version">
    /// The value of the timer's version, as last provided to the time by calling the Change
    /// method, when the callback began executing.
    /// </param>
    public delegate void VersionedTimerCallback<T>( T state, long version );
}