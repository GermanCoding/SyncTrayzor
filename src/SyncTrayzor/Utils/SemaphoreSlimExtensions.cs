﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace SyncTrayzor.Utils
{
    public static class SemaphoreSlimExtensions
    {
        private class SemaphoreSlimLock : IDisposable
        {
            private readonly SemaphoreSlim semaphore;
            public SemaphoreSlimLock(SemaphoreSlim semaphore)
            {
                this.semaphore = semaphore;
            }

            public void Dispose()
            {
                semaphore.Release();
            }
        }

        public static IDisposable WaitDisposable(this SemaphoreSlim semaphore)
        {
            semaphore.Wait();
            return new SemaphoreSlimLock(semaphore);
        }

        public static async Task<IDisposable> WaitAsyncDisposable(this SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            return new SemaphoreSlimLock(semaphore);
        }
    }
}
