using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace YLFramework.Core
{
    internal class DisableListener : MonoBehaviour
    {
        [SerializeField]
        internal UnityEvent onDisable = new UnityEvent();
         
        private void OnDisable()
        {
            onDisable?.Invoke();
        } 
    }

}

