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
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang lists. Lists are created from zero
     * or more arbitrary Erlang terms.
     * 
     * <p>
     * The arity of the list is the number of elements it contains.
     */
    [Serializable]
    public class OtpErlangList : OtpErlangObject, IEnumerable<OtpErlangObject>
    {
        // don't change this!
        internal static readonly new long serialVersionUID = 5999112769036676548L;

        private static readonly OtpErlangObject[] NO_ELEMENTS = new OtpErlangObject[0];

        private readonly OtpErlangObject[] elems;

        private OtpErlangObject lastTail = null;

        /**
         * Create an empty list.
         */
        public OtpErlangList()
        {
            elems = NO_ELEMENTS;
        }

        /**
         * Create a list of Erlang integers representing Unicode codePoints.
         * This method does not check if the string contains valid code points.
         * 
         * @param str
         *            the characters from which to create the list.
         */
        public OtpErlangList(String str)
        {
            if (str == null || str.Length == 0)
            {
                elems = NO_ELEMENTS;
            }
            else
            {
                int[] codePoints = OtpErlangString.stringToCodePoints(str);
                elems = new OtpErlangObject[codePoints.Length];
                for (int i = 0; i < elems.Length; i++)
                {
                    elems[i] = new OtpErlangInt(codePoints[i]);
                }
            }
        }

        /**
         * Create a list containing one element.
         * 
         * @param elem
         *            the elememet to make the list from.
         */
        public OtpErlangList(OtpErlangObject elem)
        {
            elems = new OtpErlangObject[] { elem };
        }

        /**
         * Create a list from an array of arbitrary Erlang terms.
         * 
         * @param elems
         *            the array of terms from which to create the list.
         */
        public OtpErlangList(OtpErlangObject[] elems)
            : this(elems, 0, elems.Length)
        {
        }

        /**
         * Create a list from an array of arbitrary Erlang terms. Tail can be
         * specified, if not null, the list will not be proper.
         * 
         * @param elems
         *            array of terms from which to create the list
         * @param lastTail
         * @throws OtpErlangException
         */
        public OtpErlangList(OtpErlangObject[] elems, OtpErlangObject lastTail)
            : this(elems, 0, elems.Length)
        {
            if (elems.Length == 0 && lastTail != null)
            {
                throw new OtpErlangException("Bad list, empty head, non-empty tail");
            }
            this.lastTail = lastTail;
        }

        /**
         * Create a list from an array of arbitrary Erlang terms.
         * 
         * @param elems
         *            the array of terms from which to create the list.
         * @param start
         *            the offset of the first term to insert.
         * @param count
         *            the number of terms to insert.
         */
        public OtpErlangList(OtpErlangObject[] elems, int start, int count)
        {
            if (elems != null && count > 0)
            {
                this.elems = new OtpErlangObject[count];
                Array.Copy(elems, start, this.elems, 0, count);
            }
            else
            {
                this.elems = NO_ELEMENTS;
            }
        }

        /**
         * Create a list from a stream containing an list encoded in Erlang external
         * format.
         * 
         * @param buf
         *            the stream containing the encoded list.
         * 
         * @exception OtpErlangDecodeException
         *                if the buffer does not contain a valid external
         *                representation of an Erlang list.
         */
        public OtpErlangList(OtpInputStream buf)
        {
            int arity = buf.read_list_head();
            if (arity > 0)
            {
                elems = new OtpErlangObject[arity];
                for (int i = 0; i < arity; i++)
                {
                    elems[i] = buf.read_any();
                }
                /* discard the terminating nil (empty list) or read tail */
                if (buf.peek1() == OtpExternal.nilTag)
                {
                    buf.read_nil();
                }
                else
                {
                    lastTail = buf.read_any();
                }
            }
            else
            {
                elems = NO_ELEMENTS;
            }
        }

        /**
         * Get the arity of the list.
         * 
         * @return the number of elements contained in the list.
         */
        public virtual int arity()
        {
            return elems.Length;
        }

        /**
         * Get the specified element from the list.
         * 
         * @param i
         *            the index of the requested element. List elements are numbered
         *            as array elements, starting at 0.
         * 
         * @return the requested element, of null if i is not a valid element index.
         */
        public virtual OtpErlangObject elementAt(int i)
        {
            if (i >= arity() || i < 0)
            {
                return null;
            }
            return elems[i];
        }

        /**
         * Get all the elements from the list as an array.
         * 
         * @return an array containing all of the list's elements.
         */
        public virtual OtpErlangObject[] elements()
        {
            if (arity() == 0)
            {
                return NO_ELEMENTS;
            }
            else
            {
                OtpErlangObject[] res = new OtpErlangObject[arity()];
                Array.Copy(elems, 0, res, 0, res.Length);
                return res;
            }
        }

        /**
         * Get the string representation of the list.
         * 
         * @return the string representation of the list.
         */
        public override String ToString()
        {
            return toString(0);
        }

        protected String toString(int start)
        {
            StringBuilder s = new StringBuilder();
            s.Append("[");

            for (int i = start; i < arity(); i++)
            {
                if (i > start)
                {
                    s.Append(",");
                }
                s.Append(elems[i].ToString());
            }
            if (lastTail != null)
            {
                s.Append("|").Append(lastTail.ToString());
            }
            s.Append("]");

            return s.ToString();
        }

        /**
         * Convert this list to the equivalent Erlang external representation. Note
         * that this method never encodes lists as strings, even when it is possible
         * to do so.
         * 
         * @param buf
         *            An output stream to which the encoded list should be written.
         * 
         */
        public override void encode(OtpOutputStream buf)
        {
            encode(buf, 0);
        }

        protected void encode(OtpOutputStream buf, int start)
        {
            int arity = this.arity() - start;

            if (arity > 0)
            {
                buf.write_list_head(arity);

                for (int i = start; i < arity + start; i++)
                {
                    buf.write_any(elems[i]);
                }
            }
            if (lastTail == null)
            {
                buf.write_nil();
            }
            else
            {
                buf.write_any(lastTail);
            }
        }

        /**
         * Determine if two lists are equal. Lists are equal if they have the same
         * arity and all of the elements are equal.
         * 
         * @param o
         *            the list to compare to.
         * 
         * @return true if the lists have the same arity and all the elements are
         *         equal.
         */
        public override bool Equals(Object o)
        {
            /*
             * Be careful to use methods even for "this", so that equals work also
             * for sublists
             */

            if (!(o is OtpErlangList))
            {
                return false;
            }

            OtpErlangList l = (OtpErlangList)o;

            int a = arity();
            if (a != l.arity())
            {
                return false;
            }
            for (int i = 0; i < a; i++)
            {
                if (!elementAt(i).Equals(l.elementAt(i)))
                {
                    return false; // early exit
                }
            }
            OtpErlangObject otherTail = l.getLastTail();
            if (getLastTail() == null && otherTail == null)
            {
                return true;
            }
            if (getLastTail() == null)
            {
                return false;
            }
            return getLastTail().Equals(l.getLastTail());
        }

        public virtual OtpErlangObject getLastTail()
        {
            return lastTail;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected override int doHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(4);
            int a = arity();
            if (a == 0)
            {
                return unchecked((int)3468870702L);
            }
            for (int i = 0; i < a; i++)
            {
                hash.combine(elementAt(i).GetHashCode());
            }
            OtpErlangObject t = getLastTail();
            if (t != null)
            {
                int h = t.GetHashCode();
                hash.combine(h, h);
            }
            return hash.valueOf();
        }

        public override Object Clone()
        {
            try
            {
                return new OtpErlangList(elements(), getLastTail());
            }
            catch (OtpErlangException)
            {
                return null;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<OtpErlangObject> GetEnumerator()
        {
            return iterator(0);
        }

        protected virtual IEnumerator<OtpErlangObject> iterator(int start)
        {
            return new Itr(start, elems);
        }

        /**
         * @return true if the list is proper, i.e. the last tail is nil
         */
        public virtual bool isProper()
        {
            return lastTail == null;
        }

        public virtual OtpErlangObject getHead()
        {
            if (arity() > 0)
            {
                return elems[0];
            }
            return null;
        }

        public virtual OtpErlangObject getTail()
        {
            return getNthTail(1);
        }

        public virtual OtpErlangObject getNthTail(int n)
        {
            int arity = this.arity();
            if (arity >= n)
            {
                if (arity == n && lastTail != null)
                {
                    return lastTail;
                }
                else
                {
                    return new SubList(this, n);
                }
            }
            return null;
        }

        /**
         * Convert a list of integers into a Unicode string,
         * interpreting each integer as a Unicode code point value.
         * 
         * @return A java.lang.String object created through its
         *         constructor String(int[], int, int).
         *
         * @exception OtpErlangException
         *                    for non-proper and non-integer lists.
         *
         * @exception OtpErlangRangeException
         *                    if any integer does not fit into a Java int.
         *
         * @exception java.security.InvalidParameterException
         *                    if any integer is not within the Unicode range.
         *
         * @see String#String(int[], int, int)
         *
         */
        public String stringValue()
        {
            if (!isProper())
            {
                throw new OtpErlangException("Non-proper list: " + this);
            }
            char[] values = new char[arity()];
            for (int i = 0; i < values.Length; ++i)
            {
                OtpErlangObject o = elementAt(i);
                if (!(o is OtpErlangLong))
                {
                    throw new OtpErlangException("Non-integer term: " + o);
                }
                OtpErlangLong l = (OtpErlangLong)o;
                values[i] = (char)l.intValue();
            }
            return new String(values, 0, values.Length);
        }

        private class SubList : OtpErlangList
        {
            internal static readonly new long serialVersionUID = OtpErlangList.serialVersionUID;

            private readonly int start;

            private readonly OtpErlangList parent;

            public SubList(OtpErlangList parent, int start)
                : base()
            {
                this.parent = parent;
                this.start = start;
            }

            public override int arity()
            {
                return parent.arity() - start;
            }

            public override OtpErlangObject elementAt(int i)
            {
                return parent.elementAt(i + start);
            }

            public override OtpErlangObject[] elements()
            {
                int n = parent.arity() - start;
                OtpErlangObject[] res = new OtpErlangObject[n];
                for (int i = 0; i < res.Length; i++)
                {
                    res[i] = parent.elementAt(i + start);
                }
                return res;
            }

            public override bool isProper()
            {
                return parent.isProper();
            }

            public override OtpErlangObject getHead()
            {
                return parent.elementAt(start);
            }

            public override OtpErlangObject getNthTail(int n)
            {
                return parent.getNthTail(n + start);
            }

            public override String ToString()
            {
                return parent.toString(start);
            }

            public override void encode(OtpOutputStream stream)
            {
                parent.encode(stream, start);
            }

            public override OtpErlangObject getLastTail()
            {
                return parent.getLastTail();
            }

            protected override IEnumerator<OtpErlangObject> iterator(int start)
            {
                return parent.iterator(start);
            }
        }

        private class Itr : IEnumerator<OtpErlangObject>
        {
            /**
             * Index of element to be returned by subsequent call to next.
             */
            private int start;
            private int cursor;
            private OtpErlangObject[] elems;

            public Itr(int start, OtpErlangObject[] elems)
            {
                this.start = start;
                this.cursor = -1;
                this.elems = elems;
            }

            public bool MoveNext()
            {
                if (cursor == -1)
                {
                    cursor = start;
                }
                else
                {
                    cursor++;
                }
                
                return cursor < elems.Length;
            }

            public void Reset()
            {
                cursor = -1;
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public OtpErlangObject Current
            {
                get
                {
                    try
                    {
                        return elems[cursor];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        return null;
                    }
                }
            }

            public void Dispose()
            {
            }
        }
    }
}
