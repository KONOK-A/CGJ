using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Singletons;

namespace YLFramework.DataAssets
{
    internal static class DataAssetLifetime
    {
        private static readonly HashSet<IDisposable> _owners = new HashSet<IDisposable>();
        private static bool _isClearing;
        internal static int Generation
        {
            get; private set;
        }

        static DataAssetLifetime()
        {
            SingletonSessionState.RegisterSessionReset(Clear);
            Application.quitting += Clear;
        }

        internal static void Track(IDisposable owner)
        {
            if (_isClearing)
            {
                throw new InvalidOperationException("The DataAssets session is closing.");
            }

            _owners.Add(owner);
        }

        internal static void Untrack(IDisposable owner) => _owners.Remove(owner);

        internal static void Clear()
        {
            if (_isClearing)
            {
                return;
            }

            _isClearing = true;
            unchecked
            {
                Generation++;
            }
            List<Exception> errors = null;
            try
            {
                foreach (var owner in new List<IDisposable>(_owners))
                {
                    try
                    {
                        owner.Dispose();
                    }
                    catch (Exception error)
                    {
                        (errors ??= new List<Exception>()).Add(error);
                    }
                }
            }
            finally
            {
                _owners.Clear();
                _isClearing = false;
            }
            if (errors != null)
            {
                throw new AggregateException("DataAssets cleanup failed.", errors);
            }
        }
    }
}
