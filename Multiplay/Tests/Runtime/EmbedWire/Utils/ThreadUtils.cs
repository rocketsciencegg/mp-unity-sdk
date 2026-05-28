using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Core.Threading.Internal;
using UnityEngine;

namespace RocketScience.Services.WireDirect.Tests.UnityThreadUtils
{
    static class UnityThreadUtils
    {
        static int s_UnityThreadId;

        internal static TaskScheduler UnityThreadScheduler { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void CaptureUnityThreadInfo()
        {
            s_UnityThreadId = Thread.CurrentThread.ManagedThreadId;
            UnityThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }

        public static bool IsRunningOnUnityThread => Thread.CurrentThread.ManagedThreadId == s_UnityThreadId;
    }

    public class UnityThreadUtilsWrapper : IUnityThreadUtils
    {
        public bool IsRunningOnUnityThread => UnityThreadUtils.IsRunningOnUnityThread;
        public Task PostAsync(Action action) => UnityThreadUtilsInternal.PostAsync(action);
        public Task PostAsync(Action<object> action, object state) => UnityThreadUtilsInternal.PostAsync(action, state);
        public Task<T> PostAsync<T>(Func<T> action) => UnityThreadUtilsInternal.PostAsync(action);
        public Task<T> PostAsync<T>(Func<object, T> action, object state) => UnityThreadUtilsInternal.PostAsync(action, state);
        public void Send(Action action) => UnityThreadUtilsInternal.Send(action);
        public void Send(Action<object> action, object state) => UnityThreadUtilsInternal.Send(action, state);
        public T Send<T>(Func<T> action) => UnityThreadUtilsInternal.Send(action);
        public T Send<T>(Func<object, T> action, object state) => UnityThreadUtilsInternal.Send(action, state);
    }

    class UnityThreadUtilsInternal : IUnityThreadUtils
    {
        public static Task PostAsync(Action action)
        {
            return Task.Factory.StartNew(
                action, CancellationToken.None, TaskCreationOptions.None, UnityThreadUtils.UnityThreadScheduler);
        }

        public static Task PostAsync(Action<object> action, object state)
        {
            return Task.Factory.StartNew(
                action, state, CancellationToken.None, TaskCreationOptions.None,
                UnityThreadUtils.UnityThreadScheduler);
        }

        public static Task<T> PostAsync<T>(Func<T> action)
        {
            return Task<T>.Factory.StartNew(
                action, CancellationToken.None, TaskCreationOptions.None, UnityThreadUtils.UnityThreadScheduler);
        }

        public static Task<T> PostAsync<T>(Func<object, T> action, object state)
        {
            return Task<T>.Factory.StartNew(
                action, state, CancellationToken.None, TaskCreationOptions.None,
                UnityThreadUtils.UnityThreadScheduler);
        }

        public static void Send(Action action)
        {
            if (UnityThreadUtils.IsRunningOnUnityThread)
            {
                action();
                return;
            }

            PostAsync(action).Wait();
        }

        public static void Send(Action<object> action, object state)
        {
            if (UnityThreadUtils.IsRunningOnUnityThread)
            {
                action(state);
                return;
            }

            PostAsync(action, state).Wait();
        }

        public static T Send<T>(Func<T> action)
        {
            if (UnityThreadUtils.IsRunningOnUnityThread)
            {
                return action();
            }

            var task = PostAsync(action);
            task.Wait();
            return task.Result;
        }

        public static T Send<T>(Func<object, T> action, object state)
        {
            if (UnityThreadUtils.IsRunningOnUnityThread)
            {
                return action(state);
            }

            var task = PostAsync(action, state);
            task.Wait();
            return task.Result;
        }

        bool IUnityThreadUtils.IsRunningOnUnityThread => UnityThreadUtils.IsRunningOnUnityThread;
        Task IUnityThreadUtils.PostAsync(Action action) => PostAsync(action);
        Task IUnityThreadUtils.PostAsync(Action<object> action, object state) => PostAsync(action, state);
        Task<T> IUnityThreadUtils.PostAsync<T>(Func<T> action) => PostAsync(action);
        Task<T> IUnityThreadUtils.PostAsync<T>(Func<object, T> action, object state) => PostAsync(action, state);
        void IUnityThreadUtils.Send(Action action) => Send(action);
        void IUnityThreadUtils.Send(Action<object> action, object state) => Send(action, state);
        T IUnityThreadUtils.Send<T>(Func<T> action) => Send(action);
        T IUnityThreadUtils.Send<T>(Func<object, T> action, object state) => Send(action, state);
    }
}
