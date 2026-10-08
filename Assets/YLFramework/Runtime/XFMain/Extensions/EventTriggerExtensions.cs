// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/EventTriggerExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.

using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace YLFramework.Core.Extensions
{

    public static class EventTriggerExtensions
    {
        // Fix编码

        public static void AddListener(this EventTrigger trigger, EventTriggerType eventType, UnityAction<BaseEventData> action)
        {

            if (trigger == null) return;

            EventTrigger.Entry entry = null;

            foreach (var item in trigger.triggers)
            {
                if (item.eventID == eventType)
                {
                    entry = item;
                    break;
                }
            }

            if (entry == null)
            {
                entry = new EventTrigger.Entry();
                entry.eventID = eventType;
                trigger.triggers.Add(entry);
            }

            entry.callback.AddListener(action);
        }

        public static void RemoveListener(this EventTrigger trigger, EventTriggerType eventType, UnityAction<BaseEventData> action)
        {
            if (trigger == null) return;

            foreach (var item in trigger.triggers)
            {
                if (item.eventID == eventType)
                    item.callback.RemoveListener(action); 
            }
        }

        public static void RemoveAllListener(this EventTrigger trigger, EventTriggerType eventType)
        {
            if (trigger == null) return;

            foreach (var item in trigger.triggers)
            {
                if (item.eventID == eventType)
                {
                    item.callback.RemoveAllListeners();
                }
            }
        }


        public static void RemoveAllListener(this EventTrigger trigger) 
        {
            if (trigger == null) return;

            foreach (var item in trigger.triggers)
            { 
                item.callback.RemoveAllListeners(); 
            }
        }

    }
}