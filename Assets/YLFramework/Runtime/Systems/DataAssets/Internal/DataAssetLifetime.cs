using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Core.Singleton;

namespace YLFramework.DataAssets
{
    internal static class DataAssetLifetime
    {
        private static readonly HashSet<IDisposable> owners = new HashSet<IDisposable>();
        private static bool clearing;
        internal static int Generation { get; private set; }

        static DataAssetLifetime()
        {
            SingletonSessionState.RegisterSessionReset(Clear);
            Application.quitting += Clear;
        }

        internal static void Track(IDisposable owner)
        {
            if (clearing) throw new InvalidOperationException("The DataAssets session is closing.");
            owners.Add(owner);
        }

        internal static void Untrack(IDisposable owner) => owners.Remove(owner);

        internal static void Clear()
        {
            if (clearing) return;
            clearing = true;
            unchecked { Generation++; }
            List<Exception> errors = null;
            try
            {
                foreach (var owner in new List<IDisposable>(owners))
                {
                    try { owner.Dispose(); }
                    catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
                }
            }
            finally { owners.Clear(); clearing = false; }
            if (errors != null) throw new AggregateException("DataAssets cleanup failed.", errors);
        }
    }
}
