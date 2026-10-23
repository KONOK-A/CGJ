using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using UnityEngine;
using YLFramework.Core.Pool;

namespace YLFramework.Timing.Internal
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal sealed class TimerLifetimeListener : MonoBehaviour
    {
        private readonly HashSet<TimerGameObjectBinding> _bindings = new();
        private GameObject _owner;
        internal void Add(TimerGameObjectBinding binding)
        {
            _owner = gameObject;
            _bindings.Add(binding);
        }
        internal void Remove(TimerGameObjectBinding binding) => _bindings.Remove(binding);

        internal void CheckOwner()
        {
            if (!_owner || !this)
            {
                Notify(true, false);
            }
            else if (!_owner.activeInHierarchy)
            {
                Notify(false, false);
            }
        }

        private void OnDisable()
        {
            // 只禁用监听组件不代表 owner GameObject 已停用。
            if (!gameObject.activeInHierarchy)
            {
                Notify(false);
            }
        }
        private void OnDestroy() => Notify(true);

        private void Notify(bool destroyed, bool reportErrors = true)
        {
            var snapshot = ListPool<TimerGameObjectBinding>.Allocate();
            List<Exception> errors = null;
            try
            {
                snapshot.AddRange(_bindings);
                foreach (var binding in snapshot)
                {
                    try
                    {
                        binding.OwnerEnded(destroyed);
                    }
                    catch (Exception error)
                    {
                        if (reportErrors)
                        {
                            Debug.LogException(error);
                        }
                        else
                        {
                            (errors ??= new List<Exception>()).Add(error);
                        }
                    }
                }
            }
            finally
            {
                ListPool<TimerGameObjectBinding>.Recycle(snapshot);
            }
            if (errors == null)
            {
                return;
            }

            if (errors.Count == 1)
            {
                ExceptionDispatchInfo.Capture(errors[0]).Throw();
            }

            throw new AggregateException(errors);
        }
    }
}
