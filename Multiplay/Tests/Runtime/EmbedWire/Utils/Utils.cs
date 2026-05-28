using NUnit.Framework;

using System;
using System.Collections;
using System.Threading.Tasks;

namespace RocketScience.Services.WireDirect.Tests
{
    public delegate object ExceptionHandler(AggregateException e);
    class Utils
    {
        public static IEnumerator TaskToIEnumerator(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }


            if (task.IsFaulted)
            {
                throw task.Exception;
            }
        }

        public static IEnumerator TaskToIEnumerator<T>(Task<T> task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }


            if (task.IsFaulted)
            {
                throw task.Exception;
            }
        }

        public static IEnumerator AssertTaskThrows<T, E>(Task<T> task, ExceptionHandler exceptionHandler)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
            Assert.True(task.IsFaulted, "task is not faulted");
            Assert.IsInstanceOf<E>(exceptionHandler(task.Exception));
        }

        public static IEnumerator AssertTaskThrows<E>(Task task, ExceptionHandler exceptionHandler)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
            Assert.True(task.IsFaulted, "task is not faulted");
            Assert.IsInstanceOf<E>(exceptionHandler(task.Exception));
        }
    }
}
