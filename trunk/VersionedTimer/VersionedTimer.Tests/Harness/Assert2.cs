using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VersionedTimer.Tests.Harness
{
    public static class Assert2
    {
        public static T Throws<T>( Action func ) where T : Exception
        {
            T caughtException = null;

            try
            {
                func.Invoke();
            }
            catch( T e )
            {
                caughtException = e;
            }

            if( caughtException == null )
            {
                throw new AssertFailedException(
                    string.Format( 
                        "Assert2.Throws failed. No exception was thrown. Expected an exception of type {0} to be thrown.", 
                        typeof( T ) 
                    )
                );
            }

            return caughtException;
        }
    }
}