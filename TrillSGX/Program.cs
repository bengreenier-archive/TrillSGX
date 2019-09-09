using System;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.StreamProcessing;

namespace TrillSGX
{
    class Program
    {
        static void Main(string[] args)
        {
            // create the data source
            // note: we cannot create more than one of these right now (limitation on the unmanaged side for simplicity)
            IObservable<NativeData> source = new NativeObservable();

            // create the stream processor
            var inputStream = source
                    .Select(e => StreamEvent.CreateStart(DateTime.Now.Ticks, e))
                    .ToStreamable(
                        null,
                        FlushPolicy.FlushOnPunctuation,
                        PeriodicPunctuationPolicy.Time((ulong)TimeSpan.FromSeconds(1).Ticks));

            // The query
            long windowSize = TimeSpan.FromSeconds(2).Ticks;
            var query = inputStream
                .AlterEventDuration(windowSize)
                // we "filter" using our unmanaged matcher
                .Where(e => NativeSubscription.MatchData(e.Data))
                .Select(e => e.Data);

            // Egress results and write to console
            query.ToStreamEventObservable().ForEachAsync(e => WriteEvent(e)).Wait();
        }

        /// <summary>
        /// Event logging helper
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="e"></param>
        private static void WriteEvent<T>(StreamEvent<T> e)
        {
            if (e.IsData)
            {
                Console.WriteLine($"EventKind = {e.Kind,8}\t" +
                    $"StartTime = {e.StartTime,4}\tEndTime = {e.EndTime,20}\t" +
                    $"Payload = ( {e.Payload.ToString()} )");
            }
            else // IsPunctuation
            {
                Console.WriteLine($"EventKind = {e.Kind}\tSyncTime  = {e.StartTime,4}");
            }
        }
    }

}
