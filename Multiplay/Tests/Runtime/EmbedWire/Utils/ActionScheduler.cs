using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Core.Scheduler.Internal;

namespace RocketScience.Services.WireDirect.Tests
{
    class ActionScheduler : IActionScheduler
    {
        static long s_CurrentIndex = 0;

        TaskScheduler m_TaskScheduler;

        ConcurrentDictionary<long, CancellationTokenSource> m_CancellationTokens;
        public ActionScheduler()
        {
            m_TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            m_CancellationTokens = new ConcurrentDictionary<long, CancellationTokenSource>();
        }

        public void CancelAction(long actionId)
        {
            if (m_CancellationTokens.TryGetValue(actionId, out var token))
            {
                token.Cancel();
            }
            else
            {
                throw new KeyNotFoundException($"action not found {actionId}");
            }
        }

        public long ScheduleAction(Action action, double delaySeconds = 0)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            s_CurrentIndex++;
            var myIndex = s_CurrentIndex;
            m_CancellationTokens.TryAdd(myIndex, cancellationTokenSource);
            Task.Factory.StartNew(async() =>
            {
                await Task.Delay((int)(delaySeconds * 1000));
                action();
            }, cancellationTokenSource.Token, TaskCreationOptions.None, m_TaskScheduler).ContinueWith((task) =>
                {
                    m_CancellationTokens.TryRemove(myIndex, out _);
                });
            return myIndex;
        }
    }
}
