/* 
 * %CopyrightBegin%
 * 
 * Copyright Takayuki Usui 2009. All Rights Reserved.
 * Copyright Ericsson AB 2000-2009. All Rights Reserved.
 * 
 * The contents of this file are subject to the Erlang Public License,
 * Version 1.1, (the "License"); you may not use this file except in
 * compliance with the License. You should have received a copy of the
 * Erlang Public License along with this software. If not, it can be
 * retrieved online at http://www.erlang.org/.
 * 
 * Software distributed under the License is distributed on an "AS IS"
 * basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
 * the License for the specific language governing rights and limitations
 * under the License.
 * 
 * %CopyrightEnd%
 */
using System;
using System.Threading;

namespace Erlang.NET
{
    /**
     * This class implements a generic FIFO queue. There is no upper bound on the
     * length of the queue, items are linked.
     */

    public class GenericQueue
    {
        private const int open = 0;
        private const int closing = 1;
        private const int closed = 2;

        private int status;
        private Bucket head;
        private Bucket tail;
        private int count;

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static long currentTimeMillis()
        {
            return (long)(DateTime.UtcNow - Epoch).TotalMilliseconds;
        }

        private void init()
        {
            head = null;
            tail = null;
            count = 0;
        }

        /** Create an empty queue */
        public GenericQueue()
        {
            init();
            status = open;
        }

        /** Clear a queue */
        public void flush()
        {
            init();
        }

        public void close()
        {
            status = closing;
        }

        /**
         * Add an object to the tail of the queue.
         * 
         * @param o
         *                Object to insert in the queue
         */
        public void put(Object o)
        {
            Monitor.Enter(this);
            try
            {
                Bucket b = new Bucket(o);

                if (tail != null)
                {
                    tail.setNext(b);
                    tail = b;
                }
                else
                {
                    // queue was empty but has one element now
                    head = tail = b;
                }
                count++;

                // notify any waiting tasks
                Monitor.Pulse(this);
            }
            finally
            {
                Monitor.Exit(this);
            }
        }

        /**
         * Retrieve an object from the head of the queue, or block until one
         * arrives.
         * 
         * @return The object at the head of the queue.
         */
        public Object get()
        {
            Monitor.Enter(this);
            try
            {
                Object o = null;

                while ((o = tryGet()) == null)
                {
                    try
                    {
                        Monitor.Wait(this);
                    }
                    catch (ThreadInterruptedException)
                    {
                    }
                }
                return o;
            }
            finally
            {
                Monitor.Exit(this);
            }
        }

        /**
         * Retrieve an object from the head of the queue, blocking until one arrives
         * or until timeout occurs.
         * 
         * @param timeout
         *                Maximum time to block on queue, in ms. Use 0 to poll the
         *                queue.
         * 
         * @exception InterruptedException
         *                    if the operation times out.
         * 
         * @return The object at the head of the queue, or null if none arrived in
         *         time.
         */
        public Object get(long timeout)
        {
            Monitor.Enter(this);
            try
            {
                if (status == closed)
                {
                    return null;
                }

                long currentTime = currentTimeMillis();
                long stopTime = currentTime + timeout;
                Object o = null;

                while (true)
                {
                    if ((o = tryGet()) != null)
                    {
                        return o;
                    }

                    currentTime = currentTimeMillis();
                    if (stopTime <= currentTime)
                    {
                        throw new ThreadInterruptedException("Get operation timed out");
                    }

                    try
                    {
                        Monitor.Wait(this, (int)(stopTime - currentTime));
                    }
                    catch (ThreadInterruptedException)
                    {
                        // ignore, but really should retry operation instead
                    }
                }
            }
            finally
            {
                Monitor.Exit(this);
            }
        }

        // attempt to retrieve message from queue head
        public Object tryGet()
        {
            Object o = null;

            Monitor.Enter(this);
            try
            {
                if (head != null)
                {
                    o = head.getContents();
                    head = head.getNext();
                    count--;

                    if (head == null)
                    {
                        tail = null;
                        count = 0;
                    }
                }
            }
            finally
            {
                Monitor.Exit(this);
            }

            return o;
        }

        public int getCount()
        {
            Monitor.Enter(this);
            try
            {
                return count;
            }
            finally
            {
                Monitor.Exit(this);
            }
        }

        /*
         * The Bucket class. The queue is implemented as a linked list of Buckets.
         * The container holds the queued object and a reference to the next Bucket.
         */
        class Bucket
        {
            private Bucket next;
            private Object contents;

            public Bucket(Object o)
            {
                next = null;
                contents = o;
            }

            public void setNext(Bucket newNext)
            {
                next = newNext;
            }

            public Bucket getNext()
            {
                return next;
            }

            public Object getContents()
            {
                return contents;
            }
        }
    }
}
