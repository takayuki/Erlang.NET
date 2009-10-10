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

namespace Erlang.NET
{
    // package scope
    public class Links
    {
        private Link[] links;
        private int count;

        public Links()
            : this(10)
        {
        }

        public Links(int initialSize)
        {
            links = new Link[initialSize];
            count = 0;
        }

        public void addLink(OtpErlangPid local, OtpErlangPid remote)
        {
            lock (this)
            {
                if (find(local, remote) == -1)
                {
                    if (count >= links.Length)
                    {
                        Link[] tmp = new Link[count * 2];
                        Array.Copy(links, 0, tmp, 0, count);
                        links = tmp;
                    }
                    links[count++] = new Link(local, remote);
                }
            }
        }

        public void removeLink(OtpErlangPid local, OtpErlangPid remote)
        {
            lock (this)
            {
                int i;

                if ((i = find(local, remote)) != -1)
                {
                    count--;
                    links[i] = links[count];
                    links[count] = null;
                }
            }
        }

        public bool exists(OtpErlangPid local, OtpErlangPid remote)
        {
            lock (this)
            {
                return find(local, remote) != -1;
            }
        }

        public int find(OtpErlangPid local, OtpErlangPid remote)
        {
            lock (this)
            {
                for (int i = 0; i < count; i++)
                {
                    if (links[i].equals(local, remote))
                    {
                        return i;
                    }
                }
                return -1;
            }
        }

        public int Count
        {
            get { return count; }
        }

        /* all local pids get notified about broken connection */
        public OtpErlangPid[] localPids()
        {
            lock (this)
            {
                OtpErlangPid[] ret = null;
                if (count != 0)
                {
                    ret = new OtpErlangPid[count];
                    for (int i = 0; i < count; i++)
                    {
                        ret[i] = links[i].Local;
                    }
                }
                return ret;
            }
        }

        /* all remote pids get notified about failed pid */
        public OtpErlangPid[] remotePids()
        {
            lock (this)
            {
                OtpErlangPid[] ret = null;
                if (count != 0)
                {
                    ret = new OtpErlangPid[count];
                    for (int i = 0; i < count; i++)
                    {
                        ret[i] = links[i].Remote;
                    }
                }
                return ret;
            }
        }

        /* clears the link table, returns a copy */
        public Link[] clearLinks()
        {
            lock (this)
            {
                Link[] ret = null;
                if (count != 0)
                {
                    ret = new Link[count];
                    for (int i = 0; i < count; i++)
                    {
                        ret[i] = links[i];
                        links[i] = null;
                    }
                    count = 0;
                }
                return ret;
            }
        }

        /* returns a copy of the link table */
        public Link[] GetLinks
        {
            get
            {
                lock (this)
                {
                    Link[] ret = null;
                    if (count != 0)
                    {
                        ret = new Link[count];
                        Array.Copy(links, 0, ret, 0, count);
                    }
                    return ret;
                }
            }
        }
    }
}
