﻿using System;
using System.Threading;

namespace SyncTrayzor.Utils
{
    public class SynchronizedEventDispatcher
    {
        private readonly object sender;
        private readonly SynchronizationContext synchronizationContext;

        public SynchronizedEventDispatcher(object sender)
            : this(sender, SynchronizationContext.Current)
        {
            if (SynchronizationContext.Current == null)
                throw new ArgumentNullException("Implicit SynchronizationContext.Current cannot be null");
        }

        public SynchronizedEventDispatcher(object sender, SynchronizationContext synchronizationContext)
        {
            this.sender = sender;
            this.synchronizationContext = synchronizationContext;
        }

        public void Raise(EventHandler eventHandler)
        {
            if (eventHandler != null)
                Post(_ => eventHandler(sender, EventArgs.Empty), null);
        }

        public void Raise<T>(EventHandler<T> eventHandler, T eventArgs)
        {
            if (eventHandler != null)
                Post(_ => eventHandler(sender, eventArgs), null);
        }

        public void Raise<T>(EventHandler<T> eventHandler, Func<T> eventArgs)
        {
            if (eventHandler != null)
                Post(_ => eventHandler(sender, eventArgs()), null);
        }

        private void Post(SendOrPostCallback callback, object state)
        {
            if (synchronizationContext != null)
                synchronizationContext.Post(callback, state);
            else
                callback(state);
        }
    }
}
