// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/TimerManager/TimerManager.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using YLFramework.Core.Extensions;
using System;
using System.Collections;
using System.Collections.Generic; 
using UnityEngine;
using ApplicationTool = YLFramework.Foundation.ApplicationTool;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;


namespace YLFramework.Core
{

    public enum TimerType {
        Frame,
        Time
    }

    public class Timer 
    { 
        #region 字段

        internal TimerType type;
        internal Action callback;

        /// <summary>
        /// 中断的回调
        /// </summary>
        internal Action on_abort; 
        internal float time;
        internal bool ignoreTimeScale;
        internal int frameCount; 

        private GameObject gameObject;

        private bool bindingWithGameObject;

        internal bool repeat;

        /// <summary>
        /// 重复调用时的时间间隔 
        /// </summary>
        internal float interval;

        /// <summary>
        /// 当前的时间间隔
        /// </summary>
        internal float current_interval; 

        /// <summary>
        /// 重复调用时的帧数间隔
        /// </summary>
        internal int interval_frame_count;
        /// <summary>
        /// 当前的帧数(用于计时,判断是否需要触发回调)
        /// </summary>
        internal int current_interval_frame_count;

        /// <summary>
        /// 重复调用的次数 -1 表示一直调用，无限次数
        /// </summary>
        internal int repeat_count;

        internal bool isValid;

         
        #endregion

        #region 属性

        private DisableListener DisableListener
        {
            get 
            {
                if (!gameObject)
                    return null;

                DisableListener listener = gameObject.GetComponent<DisableListener>();

                if (listener == null)
                    listener = gameObject.AddComponent<DisableListener>();

                return listener;
            }
        }

        internal bool IsRunning { get; private set; } = false;


        #endregion


        #region 方法
         
        internal void BindGameObject(GameObject gameObject)
        {
            //isValid = true;
            //repeat = false;

            this.gameObject = gameObject;
            bindingWithGameObject = gameObject;

            if (bindingWithGameObject) 
            {
                if (DisableListener) 
                { 
                    DisableListener.onDisable.RemoveListener(OnDisable);
                    DisableListener.onDisable.AddListener(OnDisable);  
                } 
            }

        }

        private void OnDisable() 
        {
            if(DisableListener)
                DisableListener.onDisable.RemoveListener(OnDisable);
             
            gameObject = null;
            Abort();
        }

        /// <summary>
        /// 中断
        /// </summary>
        public void Abort() 
        {
            if (!isValid) return;

            // 中断的话直接设置成无效即可
            isValid = false;
            on_abort?.Invoke();

        }

        internal void Invoke(bool remove = true) 
        {
            //try
            //{
                callback?.Invoke(); 
            //}
            //catch (Exception e)
            //{
            //    Debug.LogException(e);
            //}


            // 把isValid设置为false , 会认为该延迟任务无效，进而把它移除
            if (remove) 
            {             
                // 触发之后设置成无效 避免重复触发
                isValid = false;
            }
        }

         

        internal bool IsValid() 
        {
            if (bindingWithGameObject) 
            {
                if(!gameObject || !gameObject.activeInHierarchy)
                    return false; 
            }
            return isValid;
        }

        internal void Update() 
        { 
            switch (type)
            {
                case TimerType.Frame:

                    if (repeat)
                    {

                        if (frameCount > 0)
                        {
                            frameCount--;
                        }
                        else
                        {
                            current_interval_frame_count--; 

                            if (current_interval_frame_count <= -1)
                            {
                                Invoke(false);
                                current_interval_frame_count = interval_frame_count;

                                // 如果等于-1 一直重复
                                if (repeat_count != -1)
                                {
                                    repeat_count--;
                                    if (repeat_count <= 0)
                                        isValid = false;
                                }

                            }
                        }

                    }
                    else 
                    {

                        frameCount--;

                        // 这里因为是 先让 frameCount 减少，减完之后再判断的，所以是 <= -1 
                        // 比如frameCount为1时， 第一次判断为0，第二次判断为-1 ，会触发，刚好是延迟一帧
                        if (frameCount <= -1)  
                            Invoke(); 
                    }


                    break;
                case TimerType.Time:

                    if (repeat)
                    {
                        if (time > 0)
                        {
                            time -= TimeExtensions.GetDeltaTime(ignoreTimeScale);
                            //if (time <= 0)
                            //     Invoke(false);
                        }
                        else 
                        {
                            current_interval -= TimeExtensions.GetDeltaTime(ignoreTimeScale);
                            if (current_interval <= 0) 
                            {
                                Invoke(false);
                                current_interval = interval;

                                // 如果等于-1 一直重复
                                if (repeat_count != -1) 
                                {
                                    repeat_count--;
                                    if(repeat_count <= 0)
                                        isValid = false;
                                }

                            }
                        }

                    }
                    else 
                    { 
                        time -= TimeExtensions.GetDeltaTime(ignoreTimeScale);
                        if (time <= 0)
                            Invoke();
                    }

                    break; 
            }
        }

        #endregion


        #region 生命周期

        internal void OnAdd() 
        {
            IsRunning = true;
            isValid = true;
        }

        internal void OnRemove()
        {
            IsRunning = false;
        }

        #endregion

    }

    public class TimerManager : MonoBehaviour
    {

        #region 静态字段

        // 继承自MonoBehaviour的静态对象 在编辑器关掉的时候会自动设置为空
        // 即使禁用 Edit > Project Settings > Editor >  Enter Play Mode Settings > Reload Domain 也没关系
        private static TimerManager _instance;

        #endregion

        #region 静态属性

        private static TimerManager Instance 
        {
            get 
            {
                if (!_instance && ApplicationTool.isPlaying) 
                {
                    GameObject obj = new GameObject(typeof(TimerManager).FullName);
                    DontDestroyOnLoad(obj);
                    _instance = obj.AddComponent<TimerManager>();
                }

                return _instance;
            }

        }

        #endregion

        #region 字段

        private List<Timer> timers = new List<Timer>();

        #endregion

        #region 生命周期

        private void Update()
        {
            for (int i = timers.Count - 1; i >= 0; i--) 
            {
                Timer timer = timers[i];

                if (!timer.IsValid())
                { 
                    timers.RemoveAt(i);
                    timer.OnRemove();
                }
                else
                {
                    timer.Update();
                } 
            }
        }

        #endregion


        #region 延迟调用

        /// <summary>
        /// 延迟调用
        /// </summary>
        /// <param name="callback">回调方法</param>
        /// <param name="time">延迟时间</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer DelayInvoke(Action callback,float time,bool ignoreTimeScale = true, Action onAbort = null) 
        { 
            return DelayInvoke(null,callback,time,ignoreTimeScale, onAbort);
        }

        /// <summary>
        /// 延迟调用(绑定GameObject)
        /// </summary>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="time">延迟时间</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer DelayInvoke(GameObject gameObject,Action callback, float time, bool ignoreTimeScale = true,Action onAbort = null)
        { 
            if (!Instance) return default;

            Timer timer = new Timer(); 
            DelayInvokeNonAlloc(timer, gameObject, callback, time, ignoreTimeScale, onAbort); 
            return timer;
        }

        /// <summary>
        /// 延迟一帧调用
        /// </summary>
        /// <param name="callback">回调方法</param> 
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer DelayFrame(Action callback, Action onAbort = null) 
        {  
            return DelayFrame(null, callback, onAbort);
        }

        /// <summary>
        /// 延迟一帧调用(绑定GameObject)
        /// </summary>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer DelayFrame(GameObject gameObject,Action callback, Action onAbort = null)
        {
            return DelayFrame(gameObject, callback, 1, onAbort);
        }

        /// <summary>
        /// 延迟指定帧数调用
        /// </summary>
        /// <param name="callback">回调方法</param>
        /// <param name="frameCount">延迟的帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer DelayFrame(Action callback,int frameCount, Action onAbort = null)
        {  
            return DelayFrame(null,callback,frameCount, onAbort);
        }

        /// <summary>
        /// 延迟指定帧数调用(绑定GameObject)
        /// </summary>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="frameCount">延迟的帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer DelayFrame(GameObject gameObject,Action callback, int frameCount, Action onAbort = null)
        {  
             
            if (!Instance) return default;
            Timer timer = new Timer();
            DelayFrameNonAlloc(timer, gameObject, callback, frameCount, onAbort);
            return timer;
        }

        #endregion

        #region 重复调用

        /// <summary>
        /// 重复调用
        /// </summary>
        /// <param name="callback">回调方法</param>
        /// <param name="interval">重复调用时间间隔</param>
        /// <param name="ignoreTimeScale">忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatInvoke(Action callback, float interval, bool ignoreTimeScale = true, Action onAbort = null)
        {
            return RepeatInvoke(callback, 0, interval, -1, ignoreTimeScale, onAbort);
        }

        /// <summary>
        /// 重复调用
        /// </summary>
        /// <param name="callback">回调方法</param>
        /// <param name="delay">延迟时间</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatInvoke(Action callback, float delay, float interval, bool ignoreTimeScale = true, Action onAbort = null)
        {
            return RepeatInvoke(callback, delay, interval, -1, ignoreTimeScale, onAbort);
        }


        /// <summary>
        ///  重复调用(指定重复次数)
        /// </summary>
        /// <param name="callback">回调方法</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="repeat_count">重复调用次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatInvoke(Action callback, float interval, int repeat_count, bool ignoreTimeScale = true, Action onAbort = null)
        {
            return RepeatInvoke(null, callback, 0, interval, repeat_count, ignoreTimeScale, onAbort);
        }

        /// <summary>
        /// 重复调用(指定重复次数)
        /// </summary>
        /// <param name="callback">回调方法</param>
        /// <param name="delay">延迟时间</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="repeat_count">重复调用次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatInvoke(Action callback, float delay, float interval, int repeat_count, bool ignoreTimeScale = true, Action onAbort = null)
        {
            return RepeatInvoke(null, callback, delay, interval, repeat_count, ignoreTimeScale, onAbort);
        }

        /// <summary>
        /// 重复调用(绑定GameObject)
        /// </summary>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="interval">延迟时间</param>
        /// <param name="ignoreTimeScale">重复调用的时间间隔</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatInvoke(GameObject gameObject, Action callback, float interval, bool ignoreTimeScale = true, Action onAbort = null)
        { 
            return RepeatInvoke(gameObject, callback, 0, interval, -1, ignoreTimeScale, onAbort);
        }

        /// <summary>
        /// 重复调用(绑定GameObject)
        /// </summary>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="delay">延迟时间</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatInvoke(GameObject gameObject, Action callback, float delay, float interval, bool ignoreTimeScale = true, Action onAbort = null)
        { 
            return RepeatInvoke(gameObject, callback, delay, interval, -1, ignoreTimeScale, onAbort);
        }

        /// <summary>
        /// 重复调用(绑定GameObject 并 指定重复次数)
        /// </summary>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="repeat_count">重复调用次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatInvoke(GameObject gameObject, Action callback, float interval, int repeat_count, bool ignoreTimeScale = true, Action onAbort = null) 
        {
            return RepeatInvoke(gameObject,callback,0,interval,repeat_count,ignoreTimeScale,onAbort);
        }

        /// <summary>
        /// 重复调用(绑定GameObject 并 指定重复次数)
        /// </summary>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="delay">延迟时间</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="repeat_count">重复调用次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatInvoke(GameObject gameObject, Action callback, float delay, float interval, int repeat_count, bool ignoreTimeScale = true, Action onAbort = null)
        {
            if (!Instance) return default;
            Timer timer = new Timer();
            RepeatInvokeNonAlloc( timer, gameObject, callback, delay, interval, repeat_count, ignoreTimeScale , onAbort );
            return timer;
        }

        /// <summary>
        /// 间隔固定帧数重复调用
        /// </summary>
        /// <param name="callback">回调</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatFrame(Action callback, int interval_frame_count, Action onAbort = null)
        {
            return RepeatFrame(  callback, 0, interval_frame_count, onAbort);
        }

        /// <summary>
        /// 间隔固定帧数重复调用
        /// </summary>
        /// <param name="callback">回调</param>
        /// <param name="delay_frame_count">延迟帧数</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatFrame(Action callback, int delay_frame_count, int interval_frame_count, Action onAbort = null)
        {
            return RepeatFrame(null, callback, delay_frame_count, interval_frame_count, -1, onAbort);
        }

        /// <summary>
        /// 间隔固定帧数重复调用
        /// </summary>
        /// <param name="callback">回调</param>
        /// <param name="delay_frame_count">延迟帧数</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="repeat_count">重复次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatFrame(Action callback, int delay_frame_count, int interval_frame_count, int repeat_count, Action onAbort = null)
        {
            return RepeatFrame(null,callback,delay_frame_count,interval_frame_count,repeat_count,onAbort);
        }

        /// <summary>
        /// 间隔固定帧数重复调用
        /// </summary>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatFrame(GameObject gameObject, Action callback,  int interval_frame_count, Action onAbort = null)
        {
            return RepeatFrame(gameObject, callback, 0, interval_frame_count, -1, onAbort);
        }

        /// <summary>
        /// 间隔固定帧数重复调用
        /// </summary>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调</param>
        /// <param name="delay_frame_count">延迟帧数</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatFrame(GameObject gameObject,Action callback, int delay_frame_count, int interval_frame_count,  Action onAbort = null)
        {
            return RepeatFrame(gameObject, callback, delay_frame_count, interval_frame_count, -1, onAbort);
        }

        /// <summary>
        /// 间隔固定帧数重复调用
        /// </summary>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调</param>
        /// <param name="delay_frame_count">延迟帧数</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="repeat_count">重复次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static Timer RepeatFrame(GameObject gameObject, Action callback, int delay_frame_count, int interval_frame_count, int repeat_count, Action onAbort = null)
        {

            if (!Instance) return default;

            Timer timer = new Timer();
            RepeatFrameNonAlloc(timer, gameObject, callback, delay_frame_count, interval_frame_count, repeat_count, onAbort);
            return timer;

        }

        #endregion

        #region 延迟调用 NonAlloc

        /// <summary>
        /// 延迟调用(无GC)
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="callback">回调方法</param>
        /// <param name="time">延迟时间</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        public static void DelayInvokeNonAlloc(Timer timer, Action callback, float time, bool ignoreTimeScale = true, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            DelayInvokeNonAlloc(timer,null, callback, time, ignoreTimeScale, onAbort);
        }

        /// <summary>
        /// 延迟调用(绑定GameObject) 无GC 
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="time">延迟时间</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param> 
        public static void DelayInvokeNonAlloc(Timer timer,GameObject gameObject, Action callback, float time, bool ignoreTimeScale = true, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            if (!Instance) return;
             
            timer.BindGameObject(gameObject);

            timer.callback = callback;
            timer.on_abort = onAbort;
            timer.ignoreTimeScale = ignoreTimeScale;
            timer.time = time;
            timer.type = TimerType.Time;

            timer.frameCount = 0; // 延迟的帧数 该字段只有在类型是 TimerType.Frame 时有效
            timer.repeat = false; // 是否重复触发(有特定的方法,在那里设置成true 即可)
            timer.interval = 0;   // 重复触发的时间间隔，仅在repeat为true时有效
            timer.current_interval = 0;// 重复触发的当前时间间隔，仅在repeat为true时有效 
            timer.repeat_count = 0; // 重复触发的次数，仅在repeat为true时有效
            timer.interval_frame_count = 0;
            timer.current_interval_frame_count = 0;

            AddTimer(timer); 
        }

        /// <summary>
        /// 延迟一帧调用(无GC)
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="callback">回调方法</param> 
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void DelayFrameNonAlloc(Timer timer,Action callback, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            DelayFrameNonAlloc(timer,null, callback, onAbort);
        }

        /// <summary>
        /// 延迟一帧调用(绑定GameObject) 无GC
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void DelayFrameNonAlloc(Timer timer, GameObject gameObject, Action callback, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            DelayFrameNonAlloc(timer,gameObject, callback, 1, onAbort);
        }

        /// <summary>
        /// 延迟指定帧数调用(无GC)
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="callback">回调方法</param>
        /// <param name="frameCount">延迟的帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void DelayFrameNonAlloc(Timer timer, Action callback, int frameCount, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            DelayFrameNonAlloc(timer,null, callback, frameCount, onAbort);
        }

        /// <summary>
        /// 延迟指定帧数调用(绑定GameObject) 无GC
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="frameCount">延迟的帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void DelayFrameNonAlloc(Timer timer,GameObject gameObject, Action callback, int frameCount, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            if (!Instance) return;
            //Timer timer = new Timer();
            timer.BindGameObject(gameObject);

            timer.callback = callback;
            timer.on_abort = onAbort;
            timer.ignoreTimeScale = false;
            timer.time = 0;
            timer.type = TimerType.Frame;
            timer.frameCount = frameCount;
            timer.repeat = false; // 是否重复触发(有特定的方法,在那里设置成true 即可)
            timer.interval = 0;   // 重复触发的时间间隔，仅在repeat为true时有效
            timer.current_interval = 0;// 重复触发的当前时间间隔，仅在repeat为true时有效 
            timer.repeat_count = 0; // 重复触发的次数，仅在repeat为true时有效
            timer.interval_frame_count = 0;
            timer.current_interval_frame_count = 0;

            AddTimer(timer); 
        }

        #endregion


        #region 重复调用 NonAlloc

        /// <summary>
        /// 重复调用(无GC)
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="callback">回调方法</param>
        /// <param name="interval">重复调用时间间隔</param>
        /// <param name="ignoreTimeScale">忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatInvokeNonAlloc(Timer timer,Action callback, float interval, bool ignoreTimeScale = true, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatInvokeNonAlloc(timer,callback, 0, interval, -1, ignoreTimeScale, onAbort);
        }
         
        /// <summary>
        /// 重复调用(无GC)
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="callback">回调方法</param>
        /// <param name="delay">延迟时间</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatInvokeNonAlloc(Timer timer, Action callback, float delay, float interval, bool ignoreTimeScale = true, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatInvokeNonAlloc(timer, callback, delay, interval, -1, ignoreTimeScale, onAbort);
        }
         
        /// <summary>
        ///  重复调用(指定重复次数) 无GC
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="callback">回调方法</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="repeat_count">重复调用次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatInvokeNonAlloc(Timer timer, Action callback, float interval, int repeat_count, bool ignoreTimeScale = true, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatInvokeNonAlloc(timer, null, callback, 0, interval, repeat_count, ignoreTimeScale, onAbort);
        }


        /// <summary>
        /// 重复调用(指定重复次数) 无GC
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="callback">回调方法</param>
        /// <param name="delay">延迟时间</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="repeat_count">重复调用次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatInvokeNonAlloc(Timer timer, Action callback, float delay, float interval, int repeat_count, bool ignoreTimeScale = true, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatInvokeNonAlloc(timer, null, callback, delay, interval, repeat_count, ignoreTimeScale, onAbort);
        }
         
        /// <summary>
        /// 重复调用(绑定GameObject) 无GC
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="interval">延迟时间</param>
        /// <param name="ignoreTimeScale">重复调用的时间间隔</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatInvokeNonAlloc(Timer timer, GameObject gameObject, Action callback, float interval, bool ignoreTimeScale = true, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatInvokeNonAlloc(timer, gameObject, callback, 0, interval, -1, ignoreTimeScale, onAbort);
        }

        /// <summary>
        /// 重复调用(绑定GameObject) 无GC
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="delay">延迟时间</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatInvokeNonAlloc(Timer timer, GameObject gameObject, Action callback, float delay, float interval, bool ignoreTimeScale = true, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatInvokeNonAlloc(timer, gameObject, callback, delay, interval, -1, ignoreTimeScale, onAbort);
        }


        /// <summary>
        /// 重复调用(绑定GameObject 并 指定重复次数) 无GC
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="repeat_count">重复调用次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatInvokeNonAlloc(Timer timer, GameObject gameObject, Action callback, float interval, int repeat_count, bool ignoreTimeScale = true, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatInvokeNonAlloc(timer, gameObject, callback, 0, interval, repeat_count, ignoreTimeScale, onAbort);
        }

        /// <summary>
        /// 重复调用(绑定GameObject 并 指定重复次数) 无GC
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调方法</param>
        /// <param name="delay">延迟时间</param>
        /// <param name="interval">重复调用的时间间隔</param>
        /// <param name="repeat_count">重复调用次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatInvokeNonAlloc(Timer timer, GameObject gameObject, Action callback, float delay, float interval, int repeat_count, bool ignoreTimeScale = true, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            if (!Instance) return;
            //Timer timer = new Timer();
            timer.BindGameObject(gameObject);

            timer.callback = callback;
            timer.on_abort = onAbort;
            timer.ignoreTimeScale = ignoreTimeScale;
            timer.time = delay;
            timer.interval = interval;
            timer.current_interval = interval;
            timer.type = TimerType.Time;
            timer.repeat = true;
            timer.repeat_count = repeat_count;
            timer.frameCount = 0; // 仅在 type 为 TimerType.Time 时有效
            timer.interval_frame_count = 0;
            timer.current_interval_frame_count = 0;

            AddTimer(timer);
            //Instance.timers.Add(timer);
            //return timer;
        }

        /// <summary>
        /// 间隔固定帧数重复调用(无GC)
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="callback">回调</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatFrameNonAlloc(Timer timer,Action callback, int interval_frame_count, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatFrameNonAlloc(timer, callback, 0, interval_frame_count, onAbort);
        }

        /// <summary>
        /// 间隔固定帧数重复调用(无GC)
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="callback">回调</param>
        /// <param name="delay_frame_count">延迟帧数</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatFrameNonAlloc(Timer timer, Action callback, int delay_frame_count, int interval_frame_count, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatFrameNonAlloc(timer, null, callback, delay_frame_count, interval_frame_count, -1, onAbort);
        }

        /// <summary>
        /// 间隔固定帧数重复调用(无GC)
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="callback">回调</param>
        /// <param name="delay_frame_count">延迟帧数</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="repeat_count">重复次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatFrameNonAlloc(Timer timer, Action callback, int delay_frame_count, int interval_frame_count, int repeat_count, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatFrameNonAlloc(timer, null, callback, delay_frame_count, interval_frame_count, repeat_count, onAbort);
        }

        /// <summary>
        /// 间隔固定帧数重复调用(无GC)
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatFrameNonAlloc(Timer timer, GameObject gameObject, Action callback, int interval_frame_count, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatFrameNonAlloc(timer, gameObject, callback, 0, interval_frame_count, -1, onAbort);
        }

        /// <summary>
        /// 间隔固定帧数重复调用(无GC)
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调</param>
        /// <param name="delay_frame_count">延迟帧数</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatFrameNonAlloc(Timer timer, GameObject gameObject, Action callback, int delay_frame_count, int interval_frame_count, Action onAbort = null)
        {
            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            RepeatFrameNonAlloc(timer, gameObject, callback, delay_frame_count, interval_frame_count, -1, onAbort);
        }

        /// <summary>
        /// 间隔固定帧数重复调用(无GC)
        /// </summary>
        /// <param name="timer">计时对象</param>
        /// <param name="gameObject">绑定的GameObject,如果GameObject被销毁或处于非活跃状态,自动取消延迟调用</param>
        /// <param name="callback">回调</param>
        /// <param name="delay_frame_count">延迟帧数</param>
        /// <param name="interval_frame_count">间隔帧数</param>
        /// <param name="repeat_count">重复次数( -1 表示一直重复调用,不会停止)</param>
        /// <param name="onAbort">中断回调</param>
        /// <returns></returns>
        public static void RepeatFrameNonAlloc(Timer timer, GameObject gameObject, Action callback, int delay_frame_count, int interval_frame_count, int repeat_count, Action onAbort = null)
        {

            if (timer == null)
            {
                Debug.LogException(new Exception("timer is null!"));
                return;
            }

            if (!Instance) return;

            //Timer timer = new Timer();
            timer.BindGameObject(gameObject);

            timer.callback = callback;
            timer.on_abort = onAbort;
            timer.ignoreTimeScale = false; // 只有类型为 TimerType.Time 时有效 ， TimerType.Frame 

            timer.type = TimerType.Frame; // 

            timer.time = 0;     // 延迟时间 (仅在类型是 TimerType.Time 时生效)
            timer.interval = 0; // 重复调用的时间间隔(仅在类型是 TimerType.Time 时生效)
            timer.current_interval = 0; // 当前的时间间隔(仅在类型是 TimerType.Time 时生效)

            timer.frameCount = delay_frame_count;
            timer.interval_frame_count = interval_frame_count;
            timer.current_interval_frame_count = interval_frame_count;

            timer.repeat = true;
            timer.repeat_count = repeat_count;

            AddTimer(timer);
            ////Instance.timers.Add(timer);
            //return timer; 
        }


        #endregion


        #region 方法

        private static void AddTimer(Timer timer)
        {
            if (!timer.IsRunning)  // 说明已经添加过了 不能重复添加 
                Instance.timers.Add(timer);
            timer.OnAdd();
        }
         
        /// <summary>
        /// 等待协程执行结束后执行
        /// </summary>
        /// <param name="coroutine">协程对象</param>
        /// <param name="onFinsh">回调</param>
        public static void WaitForCoroutine(Coroutine coroutine, Action onFinsh)
        {
            CoroutineStarter.Start(WaitForCoroutineExcute(coroutine, onFinsh));
        }

        private static IEnumerator WaitForCoroutineExcute(Coroutine coroutine, Action onFinsh)
        {
            yield return coroutine;
            onFinsh?.Invoke();
        }
         
        #endregion

    }

}