using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TrillSGX
{
    /// <summary>
    /// Represents native data
    /// 
    /// Not actually needed anymore, leftover from when i was trying
    /// to marshal the data as a struct (not just a string)
    /// </summary>
    internal struct NativeData
    {
        /// <summary>
        /// The stringy data
        /// </summary>
        public string Data;
    }

    /// <summary>
    /// A subscription to a native data feed
    /// </summary>
    internal class NativeSubscription : IDisposable
    {
        /// <summary>
        /// Native pinvokes
        /// </summary>
        private class NativeMethods
        {
            private const string DllName = "TrillSGXWindowsEnclave.dll";

            public delegate void NativeCallback();

            [DllImport(DllName)]
            public static extern void RegisterNativeCallback(NativeCallback cb);

            [DllImport(DllName, CharSet = CharSet.Unicode)]
            public static extern int ReceiveCallbackData(StringBuilder data, int max);

            [DllImport(DllName, CharSet = CharSet.Unicode)]
            public static extern bool MatchData(string str, int length);

            [DllImport(DllName)]
            public static extern void UnregisterNativeCallback();
        }

        /// <summary>
        /// Front the native MatchData call, so folks outside of this class can use it
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool MatchData(string data)
        {
            return NativeMethods.MatchData(data, data.Length);
        }

        // thread-safe lock
        private readonly object sync = new object();

        // refrence to the observer
        private IObserver<NativeData> observer;

        // a pinned (in memory) delegate to marshal as a function pointer
        private NativeMethods.NativeCallback pinnedDelegate;

        /// <summary>
        /// Creates a native subscription
        /// </summary>
        /// <param name="observable"></param>
        /// <param name="observer"></param>
        public NativeSubscription(NativeObservable observable, IObserver<NativeData> observer)
        {
            this.observer = observer;

            lock (this.sync)
            {
                this.pinnedDelegate = new NativeMethods.NativeCallback(HandleNativeEvent);
                NativeMethods.RegisterNativeCallback(this.pinnedDelegate);
            }
        }

        /// <summary>
        /// Handler that the unmanaged side invokes - this is our callback
        /// to let us know some data has arrived
        /// </summary>
        private void HandleNativeEvent()
        {
            lock (this.sync)
            {
                var maxSize = 1024;
                var populateInNative = new StringBuilder(maxSize);
                var fillInManaged = new StringBuilder();
                while (NativeMethods.ReceiveCallbackData(populateInNative, populateInNative.Capacity + 1) > 0)
                {
                    fillInManaged.Append(populateInNative.ToString());
                }

                // TODO(bengreenier): refactor/remove this struct altogether
                NativeData nd = new NativeData()
                {
                    Data = fillInManaged.ToString()
                };

                // inform the stream processor that we've got data
                this.observer.OnNext(nd);
            }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.pinnedDelegate = null;
                }

                lock (this.sync)
                {
                    NativeMethods.UnregisterNativeCallback();
                }

                disposedValue = true;
            }
        }

        ~NativeSubscription()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

    }

    /// <summary>
    /// A native observable, to monitor the unmanaged side for data
    /// </summary>
    class NativeObservable : IObservable<NativeData>
    {
        public IDisposable Subscribe(IObserver<NativeData> observer)
        {
            // represent this new data subscription
            // TODO(bengreenier): right now, this will fail if you have more
            // than one, as the native side can only have one callback
            // so for now, ONLY CALL THIS ONCE
            return new NativeSubscription(this, observer);
        }
    }
}
