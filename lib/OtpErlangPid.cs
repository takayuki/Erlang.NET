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
    /**
     * Provides a Java representation of Erlang PIDs. PIDs represent Erlang
     * processes and consist of a nodename and a number of integers.
     */
    [Serializable]
    public class OtpErlangPid : OtpErlangObject
    {
        // don't change this!
        internal static readonly new long serialVersionUID = 1664394142301803659L;

        private readonly String node;
        private readonly int id;
        private readonly int serial;
        private readonly int creation;

        /**
         * Create a unique Erlang PID belonging to the local node.
         * 
         * @param self
         *                the local node.
         * 
         * @deprecated use OtpLocalNode:createPid() instead
         */
        [Obsolete]
        public OtpErlangPid(OtpLocalNode self)
        {
            OtpErlangPid p = self.createPid();

            id = p.id;
            serial = p.serial;
            creation = p.creation;
            node = p.node;
        }

        /**
         * Create an Erlang PID from a stream containing a PID encoded in Erlang
         * external format.
         * 
         * @param buf
         *                the stream containing the encoded PID.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang PID.
         */
        public OtpErlangPid(OtpInputStream buf)
        {
            OtpErlangPid p = buf.read_pid();

            node = p.Node;
            id = p.Id;
            serial = p.Serial;
            creation = p.Creation;
        }

        /**
         * Create an Erlang pid from its components.
         * 
         * @param node
         *                the nodename.
         * 
         * @param id
         *                an arbitrary number. Only the low order 15 bits will be
         *                used.
         * 
         * @param serial
         *                another arbitrary number. Only the low order 13 bits will
         *                be used.
         * 
         * @param creation
         *                yet another arbitrary number. Only the low order 2 bits
         *                will be used.
         */
        public OtpErlangPid(String node, int id, int serial, int creation)
        {
            this.node = node;
            this.id = id & 0x7fff; // 15 bits
            this.serial = serial & 0x1fff; // 13 bits
            this.creation = creation & 0x03; // 2 bits
        }

        /**
         * Get the serial number from the PID.
         * 
         * @return the serial number from the PID.
         */
        public int Serial
        {
            get { return serial; }
        }

        /**
         * Get the id number from the PID.
         * 
         * @return the id number from the PID.
         */
        public int Id
        {
            get { return id; }
        }

        /**
         * Get the creation number from the PID.
         * 
         * @return the creation number from the PID.
         */
        public int Creation
        {
            get { return creation; }
        }

        /**
         * Get the node name from the PID.
         * 
         * @return the node name from the PID.
         */
        public String Node
        {
            get { return node; }
        }

        /**
         * Get the string representation of the PID. Erlang PIDs are printed as
         * #Pid&lt;node.id.serial&gt;
         * 
         * @return the string representation of the PID.
         */
        public override String ToString()
        {
            return "#Pid<" + node.ToString() + "." + id + "." + serial + ">";
        }

        /**
         * Convert this PID to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded PID should be
         *                written.
         */
        public override void encode(OtpOutputStream buf)
        {
            buf.write_pid(node, id, serial, creation);
        }

        /**
         * Determine if two PIDs are equal. PIDs are equal if their components are
         * equal.
         * 
         * @param port
         *                the other PID to compare to.
         * 
         * @return true if the PIDs are equal, false otherwise.
         */
        public override bool Equals(Object o)
        {
            if (!(o is OtpErlangPid))
            {
                return false;
            }

            OtpErlangPid pid = (OtpErlangPid)o;

            return creation == pid.creation && serial == pid.serial && id == pid.id
            && node == pid.node;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected override int doHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(5);
            hash.combine(creation, serial);
            hash.combine(id, node.GetHashCode());
            return hash.valueOf();
        }

        public int compareTo(Object o)
        {
            if (!(o is OtpErlangPid))
            {
                return -1;
            }

            OtpErlangPid pid = (OtpErlangPid)o;
            if (creation == pid.creation)
            {
                if (serial == pid.serial)
                {
                    if (id == pid.id)
                    {
                        return node.CompareTo(pid.node);
                    }
                    else
                    {
                        return id - pid.id;
                    }
                }
                else
                {
                    return serial - pid.serial;
                }
            }
            else
            {
                return creation - pid.creation;
            }
        }
    }
}
