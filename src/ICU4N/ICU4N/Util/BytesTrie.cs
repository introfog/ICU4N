﻿using ICU4N.Support.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections;
using System.Linq;

namespace ICU4N.Util
{
    /**
 * Return values for BytesTrie.next(), CharsTrie.next() and similar methods.
 * @stable ICU 4.8
 */
    public enum BytesTrieResult
    {
        /**
         * The input unit(s) did not continue a matching string.
         * Once current()/next() return NO_MATCH,
         * all further calls to current()/next() will also return NO_MATCH,
         * until the trie is reset to its original state or to a saved state.
         * @stable ICU 4.8
         */
        NO_MATCH, // ICU4N TODO: API - change case for .NET
        /**
         * The input unit(s) continued a matching string
         * but there is no value for the string so far.
         * (It is a prefix of a longer string.)
         * @stable ICU 4.8
         */
        NO_VALUE,
        /**
         * The input unit(s) continued a matching string
         * and there is a value for the string so far.
         * This value will be returned by getValue().
         * No further input byte/unit can continue a matching string.
         * @stable ICU 4.8
         */
        FINAL_VALUE,
        /**
         * The input unit(s) continued a matching string
         * and there is a value for the string so far.
         * This value will be returned by getValue().
         * Another input byte/unit can continue a matching string.
         * @stable ICU 4.8
         */
        INTERMEDIATE_VALUE

        // Note: The following methods assume the particular order
        // of enum constants, treating the ordinal() values like bit sets.
        // Do not reorder the enum constants!   
    }

    public static class BytesTrieResultExtensions
    {
        /**
         * Same as (result!=NO_MATCH).
         * @return true if the input bytes/units so far are part of a matching string/byte sequence.
         * @stable ICU 4.8
         */
        public static bool Matches(this BytesTrieResult result) { return result != BytesTrieResult.NO_MATCH; }

        /**
         * Equivalent to (result==INTERMEDIATE_VALUE || result==FINAL_VALUE).
         * @return true if there is a value for the input bytes/units so far.
         * @see #getValue
         * @stable ICU 4.8
         */
        public static bool HasValue(this BytesTrieResult result) { return (int)result >= 2; }

        /**
         * Equivalent to (result==NO_VALUE || result==INTERMEDIATE_VALUE).
         * @return true if another input byte/unit can continue a matching string.
         * @stable ICU 4.8
         */
        public static bool HasNext(this BytesTrieResult result) { return ((int)result & 1) != 0; }
    }

    /// <summary>
    /// Light-weight, non-const reader class for a BytesTrie.
    /// Traverses a byte-serialized data structure with minimal state,
    /// for mapping byte sequences to non-negative integer values.
    /// <para/>
    /// This class is not intended for public subclassing.
    /// </summary>
    /// <stable>ICU 4.8</stable>
    /// <author>Markus W. Scherer</author>
    public sealed class BytesTrie : IEnumerable<BytesTrie.Entry>
#if FEATURE_CLONEABLE
        , ICloneable
#endif
    {
        /**
     * Constructs a BytesTrie reader instance.
     *
     * <p>The array must contain a copy of a byte sequence from the BytesTrieBuilder,
     * with the offset indicating the first byte of that sequence.
     * The BytesTrie object will not read more bytes than
     * the BytesTrieBuilder generated in the corresponding build() call.
     *
     * <p>The array is not copied/cloned and must not be modified while
     * the BytesTrie object is in use.
     *
     * @param trieBytes Bytes array that contains the serialized trie.
     * @param offset Root offset of the trie in the array.
     * @stable ICU 4.8
     */
        public BytesTrie(byte[] trieBytes, int offset)
        {
            bytes_ = trieBytes;
            pos_ = root_ = offset;
            remainingMatchLength_ = -1;
        }

        /**
         * Clones this trie reader object and its state,
         * but not the byte array which will be shared.
         * @return A shallow clone of this trie.
         * @stable ICU 4.8
         */
        public object Clone()
        {
            return base.MemberwiseClone();  // A shallow copy is just what we need.
        }

        /**
         * Resets this trie to its initial state.
         * @return this
         * @stable ICU 4.8
         */
        public BytesTrie Reset()
        {
            pos_ = root_;
            remainingMatchLength_ = -1;
            return this;
        }

        /**
         * BytesTrie state object, for saving a trie's current state
         * and resetting the trie back to this state later.
         * @stable ICU 4.8
         */
        public sealed class State // ICU4N TODO: API De-nest?
        {
            /**
             * Constructs an empty State.
             * @stable ICU 4.8
             */
            public State() { }
            internal byte[] Bytes { get; set; }
            internal int Root { get; set; }
            internal int Pos { get; set; }
            internal int RemainingMatchLength { get; set; }
        }

        /**
         * Saves the state of this trie.
         * @param state The State object to hold the trie's state.
         * @return this
         * @see #resetToState
         * @stable ICU 4.8
         */
        public BytesTrie SaveState(State state) /*const*/
        {
            state.Bytes = bytes_;
            state.Root = root_;
            state.Pos = pos_;
            state.RemainingMatchLength = remainingMatchLength_;
            return this;
        }

        /**
         * Resets this trie to the saved state.
         * @param state The State object which holds a saved trie state.
         * @return this
         * @throws IllegalArgumentException if the state object contains no state,
         *         or the state of a different trie
         * @see #saveState
         * @see #reset
         * @stable ICU 4.8
         */
        public BytesTrie ResetToState(State state)
        {
            if (bytes_ == state.Bytes && bytes_ != null && root_ == state.Root)
            {
                pos_ = state.Pos;
                remainingMatchLength_ = state.RemainingMatchLength;
            }
            else
            {
                throw new ArgumentException("incompatible trie state");
            }
            return this;
        }

        // ICU4N specific - de-nested Result and renamed BytesTrieResult



        /**
         * Determines whether the byte sequence so far matches, whether it has a value,
         * and whether another input byte can continue a matching byte sequence.
         * @return The match/value Result.
         * @stable ICU 4.8
         */
        public BytesTrieResult Current() /*const*/
        {
            int pos = pos_;
            if (pos < 0)
            {
                return BytesTrieResult.NO_MATCH;
            }
            else
            {
                int node;
                return (remainingMatchLength_ < 0 && (node = bytes_[pos] & 0xff) >= kMinValueLead) ?
                        valueResults_[node & kValueIsFinal] : BytesTrieResult.NO_VALUE;
            }
        }

        /**
         * Traverses the trie from the initial state for this input byte.
         * Equivalent to reset().next(inByte).
         * @param inByte Input byte value. Values -0x100..-1 are treated like 0..0xff.
         *               Values below -0x100 and above 0xff will never match.
         * @return The match/value Result.
         * @stable ICU 4.8
         */
        public BytesTrieResult First(int inByte)
        {
            remainingMatchLength_ = -1;
            if (inByte < 0)
            {
                inByte += 0x100;
            }
            return NextImpl(root_, inByte);
        }

        /**
         * Traverses the trie from the current state for this input byte.
         * @param inByte Input byte value. Values -0x100..-1 are treated like 0..0xff.
         *               Values below -0x100 and above 0xff will never match.
         * @return The match/value Result.
         * @stable ICU 4.8
         */
        public BytesTrieResult Next(int inByte)
        {
            int pos = pos_;
            if (pos < 0)
            {
                return BytesTrieResult.NO_MATCH;
            }
            if (inByte < 0)
            {
                inByte += 0x100;
            }
            int length = remainingMatchLength_;  // Actual remaining match length minus 1.
            if (length >= 0)
            {
                // Remaining part of a linear-match node.
                if (inByte == (bytes_[pos++] & 0xff))
                {
                    remainingMatchLength_ = --length;
                    pos_ = pos;
                    int node;
                    return (length < 0 && (node = bytes_[pos] & 0xff) >= kMinValueLead) ?
                            valueResults_[node & kValueIsFinal] : BytesTrieResult.NO_VALUE;
                }
                else
                {
                    Stop();
                    return BytesTrieResult.NO_MATCH;
                }
            }
            return NextImpl(pos, inByte);
        }

        /**
         * Traverses the trie from the current state for this byte sequence.
         * Equivalent to
         * <pre>
         * Result result=current();
         * for(each c in s)
         *   if(!result.hasNext()) return Result.NO_MATCH;
         *   result=next(c);
         * return result;
         * </pre>
         * @param s Contains a string or byte sequence.
         * @param sIndex The start index of the byte sequence in s.
         * @param sLimit The (exclusive) end index of the byte sequence in s.
         * @return The match/value Result.
         * @stable ICU 4.8
         */
        public BytesTrieResult Next(byte[] s, int sIndex, int sLimit)
        {
            if (sIndex >= sLimit)
            {
                // Empty input.
                return Current();
            }
            int pos = pos_;
            if (pos < 0)
            {
                return BytesTrieResult.NO_MATCH;
            }
            int length = remainingMatchLength_;  // Actual remaining match length minus 1.
            for (; ; )
            {
                // Fetch the next input byte, if there is one.
                // Continue a linear-match node.
                byte inByte;
                for (; ; )
                {
                    if (sIndex == sLimit)
                    {
                        remainingMatchLength_ = length;
                        pos_ = pos;
                        int node;
                        return (length < 0 && (node = (bytes_[pos] & 0xff)) >= kMinValueLead) ?
                                valueResults_[node & kValueIsFinal] : BytesTrieResult.NO_VALUE;
                    }
                    inByte = s[sIndex++];
                    if (length < 0)
                    {
                        remainingMatchLength_ = length;
                        break;
                    }
                    if (inByte != bytes_[pos])
                    {
                        Stop();
                        return BytesTrieResult.NO_MATCH;
                    }
                    ++pos;
                    --length;
                }
                for (; ; )
                {
                    int node = bytes_[pos++] & 0xff;
                    if (node < kMinLinearMatch)
                    {
                        BytesTrieResult result = BranchNext(pos, node, inByte & 0xff);
                        if (result == BytesTrieResult.NO_MATCH)
                        {
                            return BytesTrieResult.NO_MATCH;
                        }
                        // Fetch the next input byte, if there is one.
                        if (sIndex == sLimit)
                        {
                            return result;
                        }
                        if (result == BytesTrieResult.FINAL_VALUE)
                        {
                            // No further matching bytes.
                            Stop();
                            return BytesTrieResult.NO_MATCH;
                        }
                        inByte = s[sIndex++];
                        pos = pos_;  // branchNext() advanced pos and wrote it to pos_ .
                    }
                    else if (node < kMinValueLead)
                    {
                        // Match length+1 bytes.
                        length = node - kMinLinearMatch;  // Actual match length minus 1.
                        if (inByte != bytes_[pos])
                        {
                            Stop();
                            return BytesTrieResult.NO_MATCH;
                        }
                        ++pos;
                        --length;
                        break;
                    }
                    else if ((node & kValueIsFinal) != 0)
                    {
                        // No further matching bytes.
                        Stop();
                        return BytesTrieResult.NO_MATCH;
                    }
                    else
                    {
                        // Skip intermediate value.
                        pos = SkipValue(pos, node);
                        // The next node must not also be a value node.
                        Debug.Assert((bytes_[pos] & 0xff) < kMinValueLead);
                    }
                }
            }
        }

        /**
         * Returns a matching byte sequence's value if called immediately after
         * current()/first()/next() returned Result.INTERMEDIATE_VALUE or Result.FINAL_VALUE.
         * getValue() can be called multiple times.
         *
         * Do not call getValue() after Result.NO_MATCH or Result.NO_VALUE!
         * @return The value for the byte sequence so far.
         * @stable ICU 4.8
         */
        public int GetValue() /*const*/
        {
            int pos = pos_;
            int leadByte = bytes_[pos++] & 0xff;
            Debug.Assert(leadByte >= kMinValueLead);
            return ReadValue(bytes_, pos, leadByte >> 1);
        }

        /**
         * Determines whether all byte sequences reachable from the current state
         * map to the same value, and if so, returns that value.
         * @return The unique value in bits 32..1 with bit 0 set,
         *         if all byte sequences reachable from the current state
         *         map to the same value; otherwise returns 0.
         * @stable ICU 4.8
         */
        public long GetUniqueValue() /*const*/
        {
            int pos = pos_;
            if (pos < 0)
            {
                return 0;
            }
            // Skip the rest of a pending linear-match node.
            long uniqueValue = FindUniqueValue(bytes_, pos + remainingMatchLength_ + 1, 0);
            // Ignore internally used bits 63..33; extend the actual value's sign bit from bit 32.
            return (uniqueValue << 31) >> 31;
        }

        /**
         * Finds each byte which continues the byte sequence from the current state.
         * That is, each byte b for which it would be next(b)!=Result.NO_MATCH now.
         * @param out Each next byte is 0-extended to a char and appended to this object.
         *            (Only uses the out.append(c) method.)
         * @return The number of bytes which continue the byte sequence from here.
         * @stable ICU 4.8
         */
        public int GetNextBytes(StringBuilder output) /*const*/
        {
            int pos = pos_;
            if (pos < 0)
            {
                return 0;
            }
            if (remainingMatchLength_ >= 0)
            {
                Append(output, bytes_[pos] & 0xff);  // Next byte of a pending linear-match node.
                return 1;
            }
            int node = bytes_[pos++] & 0xff;
            if (node >= kMinValueLead)
            {
                if ((node & kValueIsFinal) != 0)
                {
                    return 0;
                }
                else
                {
                    pos = SkipValue(pos, node);
                    node = bytes_[pos++] & 0xff;
                    Debug.Assert(node < kMinValueLead);
                }
            }
            if (node < kMinLinearMatch)
            {
                if (node == 0)
                {
                    node = bytes_[pos++] & 0xff;
                }
                GetNextBranchBytes(bytes_, pos, ++node, output);
                return node;
            }
            else
            {
                // First byte of the linear-match node.
                Append(output, bytes_[pos] & 0xff);
                return 1;
            }
        }

        /**
         * Iterates from the current state of this trie.
         * @return A new BytesTrie.Iterator.
         * @stable ICU 4.8
         */
        //@Override
        //    public Iterator iterator()
        //{
        //    return new Iterator(bytes_, pos_, remainingMatchLength_, 0);
        //}

        public IEnumerator<Entry> GetEnumerator()
        {
            return new Enumerator(bytes_, pos_, remainingMatchLength_, 0);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }



        /**
         * Iterates from the current state of this trie.
         * @param maxStringLength If 0, the iterator returns full strings/byte sequences.
         *                        Otherwise, the iterator returns strings with this maximum length.
         * @return A new BytesTrie.Iterator.
         * @stable ICU 4.8
         */
        public Enumerator GetEnumerator(int maxStringLength)
        {
            return new Enumerator(bytes_, pos_, remainingMatchLength_, maxStringLength);
        }

        /**
         * Iterates from the root of a byte-serialized BytesTrie.
         * @param trieBytes Bytes array that contains the serialized trie.
         * @param offset Root offset of the trie in the array.
         * @param maxStringLength If 0, the iterator returns full strings/byte sequences.
         *                        Otherwise, the iterator returns strings with this maximum length.
         * @return A new BytesTrie.Iterator.
         * @stable ICU 4.8
         */
        public static Enumerator GetEnumerator(byte[] trieBytes, int offset, int maxStringLength)
        {
            return new Enumerator(trieBytes, offset, -1, maxStringLength);
        }

        /**
         * Return value type for the Iterator.
         * @stable ICU 4.8
         */
        public sealed class Entry // ICU4N TODO: API De-nest?
        {
            internal Entry(int capacity)
            {
                bytes = new byte[capacity];
            }

            /**
             * @return The length of the byte sequence.
             * @stable ICU 4.8
             */
            public int BytesLength { get { return length; } }
            /**
             * Returns a byte of the byte sequence.
             * @param index An index into the byte sequence.
             * @return The index-th byte sequence byte.
             * @stable ICU 4.8
             */
            public byte ByteAt(int index) { return bytes[index]; }
            /**
             * Copies the byte sequence into a byte array.
             * @param dest Destination byte array.
             * @param destOffset Starting offset to where in dest the byte sequence is copied.
             * @stable ICU 4.8
             */
            public void CopyBytesTo(byte[] dest, int destOffset)
            {
                System.Array.Copy(bytes, 0, dest, destOffset, length);
            }
            /**
             * @return The byte sequence as a read-only ByteBuffer.
             * @stable ICU 4.8
             */
            public ByteBuffer BytesAsByteBuffer()
            {
                return ByteBuffer.Wrap(bytes, 0, length).AsReadOnlyBuffer();
            }

            /**
             * The value associated with the byte sequence.
             * @stable ICU 4.8
             */
            public int value;

            private void EnsureCapacity(int len)
            {
                if (bytes.Length < len)
                {
                    byte[] newBytes = new byte[Math.Min(2 * bytes.Length, 2 * len)];
                    System.Array.Copy(bytes, 0, newBytes, 0, length);
                    bytes = newBytes;
                }
            }
            internal void Append(byte b)
            {
                EnsureCapacity(length + 1);
                bytes[length++] = b;
            }
            internal void Append(byte[] b, int off, int len)
            {
                EnsureCapacity(length + len);
                System.Array.Copy(b, off, bytes, length, len);
                length += len;
            }
            internal void TruncateString(int newLength) { length = newLength; }

            private byte[] bytes;
            private int length;

            internal int Length { get { return length; } }
        }

        /**
         * Iterator for all of the (byte sequence, value) pairs in a BytesTrie.
         * @stable ICU 4.8
         */
        public sealed class Enumerator : IEnumerator<Entry>
        {
            private Entry current = null;

            internal Enumerator(byte[] trieBytes, int offset, int remainingMatchLength, int maxStringLength)
            {
                bytes_ = trieBytes;
                pos_ = initialPos_ = offset;
                remainingMatchLength_ = initialRemainingMatchLength_ = remainingMatchLength;
                maxLength_ = maxStringLength;
                entry_ = new Entry(maxLength_ != 0 ? maxLength_ : 32);
                int length = remainingMatchLength_;  // Actual remaining match length minus 1.
                if (length >= 0)
                {
                    // Pending linear-match node, append remaining bytes to entry_.
                    ++length;
                    if (maxLength_ > 0 && length > maxLength_)
                    {
                        length = maxLength_;  // This will leave remainingMatchLength>=0 as a signal.
                    }
                    entry_.Append(bytes_, pos_, length);
                    pos_ += length;
                    remainingMatchLength_ -= length;
                }
            }

            /**
             * Resets this iterator to its initial state.
             * @return this
             * @stable ICU 4.8
             */
            public void Reset() // ICU4N specific - removed return parameter for .NET compatibility
            {
                pos_ = initialPos_;
                remainingMatchLength_ = initialRemainingMatchLength_;
                int length = remainingMatchLength_ + 1;  // Remaining match length.
                if (maxLength_ > 0 && length > maxLength_)
                {
                    length = maxLength_;
                }
                entry_.TruncateString(length);
                pos_ += length;
                remainingMatchLength_ -= length;
                stack_.Clear();
            }

            /**
             * @return true if there are more elements.
             * @stable ICU 4.8
             */
            private bool HasNext() /*const*/ { return pos_ >= 0 || stack_.Any(); }

            /**
             * Finds the next (byte sequence, value) pair if there is one.
             *
             * If the byte sequence is truncated to the maximum length and does not
             * have a real value, then the value is set to -1.
             * In this case, this "not a real value" is indistinguishable from
             * a real value of -1.
             * @return An Entry with the string and value of the next element.
             * @throws NoSuchElementException - iteration has no more elements.
             * @stable ICU 4.8
             */
            private Entry Next()
            {
                int pos = pos_;
                if (pos < 0)
                {
                    //if (!stack_.Any())
                    //{
                    //    throw new NoSuchElementException();
                    //}
                    // Pop the state off the stack and continue with the next outbound edge of
                    // the branch node.
                    //long top = stack_.remove(stack_.size() - 1);
                    long top = stack_.Pop();
                    int length = (int)top;
                    pos = (int)(top >> 32);
                    entry_.TruncateString(length & 0xffff);
                    //length >>>= 16;
                    length = (int)((uint)length >> 16);
                    if (length > 1)
                    {
                        pos = BranchNext(pos, length);
                        if (pos < 0)
                        {
                            return entry_;  // Reached a final value.
                        }
                    }
                    else
                    {
                        entry_.Append(bytes_[pos++]);
                    }
                }
                if (remainingMatchLength_ >= 0)
                {
                    // We only get here if we started in a pending linear-match node
                    // with more than maxLength remaining bytes.
                    return TruncateAndStop();
                }
                for (; ; )
                {
                    int node = bytes_[pos++] & 0xff;
                    if (node >= kMinValueLead)
                    {
                        // Deliver value for the byte sequence so far.
                        bool isFinal = (node & kValueIsFinal) != 0;
                        entry_.value = ReadValue(bytes_, pos, node >> 1);
                        if (isFinal || (maxLength_ > 0 && entry_.Length == maxLength_))
                        {
                            pos_ = -1;
                        }
                        else
                        {
                            pos_ = SkipValue(pos, node);
                        }
                        return entry_;
                    }
                    if (maxLength_ > 0 && entry_.Length == maxLength_)
                    {
                        return TruncateAndStop();
                    }
                    if (node < kMinLinearMatch)
                    {
                        if (node == 0)
                        {
                            node = bytes_[pos++] & 0xff;
                        }
                        pos = BranchNext(pos, node + 1);
                        if (pos < 0)
                        {
                            return entry_;  // Reached a final value.
                        }
                    }
                    else
                    {
                        // Linear-match node, append length bytes to entry_.
                        int length = node - kMinLinearMatch + 1;
                        if (maxLength_ > 0 && entry_.Length + length > maxLength_)
                        {
                            entry_.Append(bytes_, pos, maxLength_ - entry_.Length);
                            return TruncateAndStop();
                        }
                        entry_.Append(bytes_, pos, length);
                        pos += length;
                    }
                }
            }

            ///**
            // * Iterator.remove() is not supported.
            // * @throws UnsupportedOperationException (always)
            // * @stable ICU 4.8
            // */
            //        public void remove()
            //{
            //    throw new UnsupportedOperationException();
            //}

            private Entry TruncateAndStop()
            {
                pos_ = -1;
                entry_.value = -1;  // no real value for str
                return entry_;
            }

            private int BranchNext(int pos, int length)
            {
                while (length > kMaxBranchLinearSubNodeLength)
                {
                    ++pos;  // ignore the comparison byte
                            // Push state for the greater-or-equal edge.
                    stack_.Push((SkipDelta(bytes_, pos) << 32) | ((length - (length >> 1)) << 16) | entry_.Length);
                    // Follow the less-than edge.
                    length >>= 1;
                    pos = JumpByDelta(bytes_, pos);
                }
                // List of key-value pairs where values are either final values or jump deltas.
                // Read the first (key, value) pair.
                byte trieByte = bytes_[pos++];
                int node = bytes_[pos++] & 0xff;
                bool isFinal = (node & kValueIsFinal) != 0;
                int value = ReadValue(bytes_, pos, node >> 1);
                pos = SkipValue(pos, node);
                stack_.Push((pos << 32) | ((length - 1) << 16) | entry_.Length);
                entry_.Append(trieByte);
                if (isFinal)
                {
                    pos_ = -1;
                    entry_.value = value;
                    return -1;
                }
                else
                {
                    return pos + value;
                }
            }

            public Entry Current
            {
                get { return current; }
            }

            object IEnumerator.Current
            {
                get { return current; }
            }

            public bool MoveNext()
            {
                if (!HasNext())
                    return false;
                current = Next();
                return (current != null);
            }

            public void Dispose()
            {
                // Nothing to do
            }

            private byte[] bytes_;
            private int pos_;
            private int initialPos_;
            private int remainingMatchLength_;
            private int initialRemainingMatchLength_;

            private int maxLength_;
            private Entry entry_;

            // The stack stores longs for backtracking to another
            // outbound edge of a branch node.
            // Each long has the offset from bytes_ in bits 62..32,
            // the entry_.stringLength() from before the node in bits 15..0,
            // and the remaining branch length in bits 24..16. (Bits 31..25 are unused.)
            // (We could store the remaining branch length minus 1 in bits 23..16 and not use bits 31..24,
            // but the code looks more confusing that way.)
            private Stack<long> stack_ = new Stack<long>();
        }

        private void Stop()
        {
            pos_ = -1;
        }

        // Reads a compact 32-bit integer.
        // pos is already after the leadByte, and the lead byte is already shifted right by 1.
        private static int ReadValue(byte[] bytes, int pos, int leadByte)
        {
            int value;
            if (leadByte < kMinTwoByteValueLead)
            {
                value = leadByte - kMinOneByteValueLead;
            }
            else if (leadByte < kMinThreeByteValueLead)
            {
                value = ((leadByte - kMinTwoByteValueLead) << 8) | (bytes[pos] & 0xff);
            }
            else if (leadByte < kFourByteValueLead)
            {
                value = ((leadByte - kMinThreeByteValueLead) << 16) | ((bytes[pos] & 0xff) << 8) | (bytes[pos + 1] & 0xff);
            }
            else if (leadByte == kFourByteValueLead)
            {
                value = ((bytes[pos] & 0xff) << 16) | ((bytes[pos + 1] & 0xff) << 8) | (bytes[pos + 2] & 0xff);
            }
            else
            {
                value = (bytes[pos] << 24) | ((bytes[pos + 1] & 0xff) << 16) | ((bytes[pos + 2] & 0xff) << 8) | (bytes[pos + 3] & 0xff);
            }
            return value;
        }
        private static int SkipValue(int pos, int leadByte)
        {
            Debug.Assert(leadByte >= kMinValueLead);
            if (leadByte >= (kMinTwoByteValueLead << 1))
            {
                if (leadByte < (kMinThreeByteValueLead << 1))
                {
                    ++pos;
                }
                else if (leadByte < (kFourByteValueLead << 1))
                {
                    pos += 2;
                }
                else
                {
                    pos += 3 + ((leadByte >> 1) & 1);
                }
            }
            return pos;
        }
        private static int SkipValue(byte[] bytes, int pos)
        {
            int leadByte = bytes[pos++] & 0xff;
            return SkipValue(pos, leadByte);
        }

        // Reads a jump delta and jumps.
        private static int JumpByDelta(byte[] bytes, int pos)
        {
            int delta = bytes[pos++] & 0xff;
            if (delta < kMinTwoByteDeltaLead)
            {
                // nothing to do
            }
            else if (delta < kMinThreeByteDeltaLead)
            {
                delta = ((delta - kMinTwoByteDeltaLead) << 8) | (bytes[pos++] & 0xff);
            }
            else if (delta < kFourByteDeltaLead)
            {
                delta = ((delta - kMinThreeByteDeltaLead) << 16) | ((bytes[pos] & 0xff) << 8) | (bytes[pos + 1] & 0xff);
                pos += 2;
            }
            else if (delta == kFourByteDeltaLead)
            {
                delta = ((bytes[pos] & 0xff) << 16) | ((bytes[pos + 1] & 0xff) << 8) | (bytes[pos + 2] & 0xff);
                pos += 3;
            }
            else
            {
                delta = (bytes[pos] << 24) | ((bytes[pos + 1] & 0xff) << 16) | ((bytes[pos + 2] & 0xff) << 8) | (bytes[pos + 3] & 0xff);
                pos += 4;
            }
            return pos + delta;
        }

        private static int SkipDelta(byte[] bytes, int pos)
        {
            int delta = bytes[pos++] & 0xff;
            if (delta >= kMinTwoByteDeltaLead)
            {
                if (delta < kMinThreeByteDeltaLead)
                {
                    ++pos;
                }
                else if (delta < kFourByteDeltaLead)
                {
                    pos += 2;
                }
                else
                {
                    pos += 3 + (delta & 1);
                }
            }
            return pos;
        }

        private static BytesTrieResult[] valueResults_ = { BytesTrieResult.INTERMEDIATE_VALUE, BytesTrieResult.FINAL_VALUE };

        // Handles a branch node for both next(byte) and next(string).
        private BytesTrieResult BranchNext(int pos, int length, int inByte)
        {
            // Branch according to the current byte.
            if (length == 0)
            {
                length = bytes_[pos++] & 0xff;
            }
            ++length;
            // The length of the branch is the number of bytes to select from.
            // The data structure encodes a binary search.
            while (length > kMaxBranchLinearSubNodeLength)
            {
                if (inByte < (bytes_[pos++] & 0xff))
                {
                    length >>= 1;
                    pos = JumpByDelta(bytes_, pos);
                }
                else
                {
                    length = length - (length >> 1);
                    pos = SkipDelta(bytes_, pos);
                }
            }
            // Drop down to linear search for the last few bytes.
            // length>=2 because the loop body above sees length>kMaxBranchLinearSubNodeLength>=3
            // and divides length by 2.
            do
            {
                if (inByte == (bytes_[pos++] & 0xff))
                {
                    BytesTrieResult result;
                    int node = bytes_[pos] & 0xff;
                    Debug.Assert(node >= kMinValueLead);
                    if ((node & kValueIsFinal) != 0)
                    {
                        // Leave the final value for getValue() to read.
                        result = BytesTrieResult.FINAL_VALUE;
                    }
                    else
                    {
                        // Use the non-final value as the jump delta.
                        ++pos;
                        // int delta=readValue(pos, node>>1);
                        node >>= 1;
                        int delta;
                        if (node < kMinTwoByteValueLead)
                        {
                            delta = node - kMinOneByteValueLead;
                        }
                        else if (node < kMinThreeByteValueLead)
                        {
                            delta = ((node - kMinTwoByteValueLead) << 8) | (bytes_[pos++] & 0xff);
                        }
                        else if (node < kFourByteValueLead)
                        {
                            delta = ((node - kMinThreeByteValueLead) << 16) | ((bytes_[pos] & 0xff) << 8) | (bytes_[pos + 1] & 0xff);
                            pos += 2;
                        }
                        else if (node == kFourByteValueLead)
                        {
                            delta = ((bytes_[pos] & 0xff) << 16) | ((bytes_[pos + 1] & 0xff) << 8) | (bytes_[pos + 2] & 0xff);
                            pos += 3;
                        }
                        else
                        {
                            delta = (bytes_[pos] << 24) | ((bytes_[pos + 1] & 0xff) << 16) | ((bytes_[pos + 2] & 0xff) << 8) | (bytes_[pos + 3] & 0xff);
                            pos += 4;
                        }
                        // end readValue()
                        pos += delta;
                        node = bytes_[pos] & 0xff;
                        result = node >= kMinValueLead ? valueResults_[node & kValueIsFinal] : BytesTrieResult.NO_VALUE;
                    }
                    pos_ = pos;
                    return result;
                }
                --length;
                pos = SkipValue(bytes_, pos);
            } while (length > 1);
            if (inByte == (bytes_[pos++] & 0xff))
            {
                pos_ = pos;
                int node = bytes_[pos] & 0xff;
                return node >= kMinValueLead ? valueResults_[node & kValueIsFinal] : BytesTrieResult.NO_VALUE;
            }
            else
            {
                Stop();
                return BytesTrieResult.NO_MATCH;
            }
        }

        // Requires remainingLength_<0.
        private BytesTrieResult NextImpl(int pos, int inByte)
        {
            for (; ; )
            {
                int node = bytes_[pos++] & 0xff;
                if (node < kMinLinearMatch)
                {
                    return BranchNext(pos, node, inByte);
                }
                else if (node < kMinValueLead)
                {
                    // Match the first of length+1 bytes.
                    int length = node - kMinLinearMatch;  // Actual match length minus 1.
                    if (inByte == (bytes_[pos++] & 0xff))
                    {
                        remainingMatchLength_ = --length;
                        pos_ = pos;
                        return (length < 0 && (node = bytes_[pos] & 0xff) >= kMinValueLead) ?
                                valueResults_[node & kValueIsFinal] : BytesTrieResult.NO_VALUE;
                    }
                    else
                    {
                        // No match.
                        break;
                    }
                }
                else if ((node & kValueIsFinal) != 0)
                {
                    // No further matching bytes.
                    break;
                }
                else
                {
                    // Skip intermediate value.
                    pos = SkipValue(pos, node);
                    // The next node must not also be a value node.
                    Debug.Assert((bytes_[pos] & 0xff) < kMinValueLead);
                }
            }
            Stop();
            return BytesTrieResult.NO_MATCH;
        }

        // Helper functions for getUniqueValue().
        // Recursively finds a unique value (or whether there is not a unique one)
        // from a branch.
        // uniqueValue: On input, same as for getUniqueValue()/findUniqueValue().
        // On return, if not 0, then bits 63..33 contain the updated non-negative pos.
        private static long FindUniqueValueFromBranch(byte[] bytes, int pos, int length,
                                                      long uniqueValue)
        {
            while (length > kMaxBranchLinearSubNodeLength)
            {
                ++pos;  // ignore the comparison byte
                uniqueValue = FindUniqueValueFromBranch(bytes, JumpByDelta(bytes, pos), length >> 1, uniqueValue);
                if (uniqueValue == 0)
                {
                    return 0;
                }
                length = length - (length >> 1);
                pos = SkipDelta(bytes, pos);
            }
            do
            {
                ++pos;  // ignore a comparison byte
                        // handle its value
                int node = bytes[pos++] & 0xff;
                bool isFinal = (node & kValueIsFinal) != 0;
                int value = ReadValue(bytes, pos, node >> 1);
                pos = SkipValue(pos, node);
                if (isFinal)
                {
                    if (uniqueValue != 0)
                    {
                        if (value != (int)(uniqueValue >> 1))
                        {
                            return 0;
                        }
                    }
                    else
                    {
                        uniqueValue = ((long)value << 1) | 1;
                    }
                }
                else
                {
                    uniqueValue = FindUniqueValue(bytes, pos + value, uniqueValue);
                    if (uniqueValue == 0)
                    {
                        return 0;
                    }
                }
            } while (--length > 1);
            // ignore the last comparison byte
            return ((long)(pos + 1) << 33) | (uniqueValue & 0x1ffffffffL);
        }
        // Recursively finds a unique value (or whether there is not a unique one)
        // starting from a position on a node lead byte.
        // uniqueValue: If there is one, then bits 32..1 contain the value and bit 0 is set.
        // Otherwise, uniqueValue is 0. Bits 63..33 are ignored.
        private static long FindUniqueValue(byte[] bytes, int pos, long uniqueValue)
        {
            for (; ; )
            {
                int node = bytes[pos++] & 0xff;
                if (node < kMinLinearMatch)
                {
                    if (node == 0)
                    {
                        node = bytes[pos++] & 0xff;
                    }
                    uniqueValue = FindUniqueValueFromBranch(bytes, pos, node + 1, uniqueValue);
                    if (uniqueValue == 0)
                    {
                        return 0;
                    }
                    pos = (int)((uint)uniqueValue >> 33);
                }
                else if (node < kMinValueLead)
                {
                    // linear-match node
                    pos += node - kMinLinearMatch + 1;  // Ignore the match bytes.
                }
                else
                {
                    bool isFinal = (node & kValueIsFinal) != 0;
                    int value = ReadValue(bytes, pos, node >> 1);
                    if (uniqueValue != 0)
                    {
                        if (value != (int)(uniqueValue >> 1))
                        {
                            return 0;
                        }
                    }
                    else
                    {
                        uniqueValue = ((long)value << 1) | 1;
                    }
                    if (isFinal)
                    {
                        return uniqueValue;
                    }
                    pos = SkipValue(pos, node);
                }
            }
        }

        // Helper functions for getNextBytes().
        // getNextBytes() when pos is on a branch node.
        private static void GetNextBranchBytes(byte[] bytes, int pos, int length, StringBuilder output)
        {
            while (length > kMaxBranchLinearSubNodeLength)
            {
                ++pos;  // ignore the comparison byte
                GetNextBranchBytes(bytes, JumpByDelta(bytes, pos), length >> 1, output);
                length = length - (length >> 1);
                pos = SkipDelta(bytes, pos);
            }
            do
            {
                Append(output, bytes[pos++] & 0xff);
                pos = SkipValue(bytes, pos);
            } while (--length > 1);
            Append(output, bytes[pos] & 0xff);
        }
        private static void Append(StringBuilder output, int c)
        {
            try
            {
                output.Append((char)c);
            }
            catch (IOException e)
            {
                throw new ICUUncheckedIOException(e);
            }
        }



        // BytesTrie data structure
        //
        // The trie consists of a series of byte-serialized nodes for incremental
        // string/byte sequence matching. The root node is at the beginning of the trie data.
        //
        // Types of nodes are distinguished by their node lead byte ranges.
        // After each node, except a final-value node, another node follows to
        // encode match values or continue matching further bytes.
        //
        // Node types:
        //  - Value node: Stores a 32-bit integer in a compact, variable-length format.
        //    The value is for the string/byte sequence so far.
        //    One node bit indicates whether the value is final or whether
        //    matching continues with the next node.
        //  - Linear-match node: Matches a number of bytes.
        //  - Branch node: Branches to other nodes according to the current input byte.
        //    The node byte is the length of the branch (number of bytes to select from)
        //    minus 1. It is followed by a sub-node:
        //    - If the length is at most kMaxBranchLinearSubNodeLength, then
        //      there are length-1 (key, value) pairs and then one more comparison byte.
        //      If one of the key bytes matches, then the value is either a final value for
        //      the string/byte sequence so far, or a "jump" delta to the next node.
        //      If the last byte matches, then matching continues with the next node.
        //      (Values have the same encoding as value nodes.)
        //    - If the length is greater than kMaxBranchLinearSubNodeLength, then
        //      there is one byte and one "jump" delta.
        //      If the input byte is less than the sub-node byte, then "jump" by delta to
        //      the next sub-node which will have a length of length/2.
        //      (The delta has its own compact encoding.)
        //      Otherwise, skip the "jump" delta to the next sub-node
        //      which will have a length of length-length/2.

        // Node lead byte values.

        // 00..0f: Branch node. If node!=0 then the length is node+1, otherwise
        // the length is one more than the next byte.

        // For a branch sub-node with at most this many entries, we drop down
        // to a linear search.
        /*package*/
        internal static readonly int kMaxBranchLinearSubNodeLength = 5;

        // 10..1f: Linear-match node, match 1..16 bytes and continue reading the next node.
        /*package*/
        internal static readonly int kMinLinearMatch = 0x10;
        /*package*/
        internal static readonly int kMaxLinearMatchLength = 0x10;

        // 20..ff: Variable-length value node.
        // If odd, the value is final. (Otherwise, intermediate value or jump delta.)
        // Then shift-right by 1 bit.
        // The remaining lead byte value indicates the number of following bytes (0..4)
        // and contains the value's top bits.
        /*package*/
        internal static readonly int kMinValueLead = kMinLinearMatch + kMaxLinearMatchLength;  // 0x20
                                                                                               // It is a final value if bit 0 is set.
        private static readonly int kValueIsFinal = 1;

        // Compact value: After testing bit 0, shift right by 1 and then use the following thresholds.
        /*package*/
        internal static readonly int kMinOneByteValueLead = kMinValueLead / 2;  // 0x10
                                                                                /*package*/
        internal static readonly int kMaxOneByteValue = 0x40;  // At least 6 bits in the first byte.

        /*package*/
        internal static readonly int kMinTwoByteValueLead = kMinOneByteValueLead + kMaxOneByteValue + 1;  // 0x51
                                                                                                          /*package*/
        internal static readonly int kMaxTwoByteValue = 0x1aff;

        /*package*/
        internal static readonly int kMinThreeByteValueLead = kMinTwoByteValueLead + (kMaxTwoByteValue >> 8) + 1;  // 0x6c
                                                                                                                   /*package*/
        internal static readonly int kFourByteValueLead = 0x7e;

        // A little more than Unicode code points. (0x11ffff)
        /*package*/
        internal static readonly int kMaxThreeByteValue = ((kFourByteValueLead - kMinThreeByteValueLead) << 16) - 1;

        /*package*/
        internal static readonly int kFiveByteValueLead = 0x7f;

        // Compact delta integers.
        /*package*/
        internal static readonly int kMaxOneByteDelta = 0xbf;
        /*package*/
        internal static readonly int kMinTwoByteDeltaLead = kMaxOneByteDelta + 1;  // 0xc0
                                                                                   /*package*/
        internal static readonly int kMinThreeByteDeltaLead = 0xf0;
        /*package*/
        internal static readonly int kFourByteDeltaLead = 0xfe;
        /*package*/
        internal static readonly int kFiveByteDeltaLead = 0xff;

        /*package*/
        internal static readonly int kMaxTwoByteDelta = ((kMinThreeByteDeltaLead - kMinTwoByteDeltaLead) << 8) - 1;  // 0x2fff
                                                                                                                     /*package*/
        internal static readonly int kMaxThreeByteDelta = ((kFourByteDeltaLead - kMinThreeByteDeltaLead) << 16) - 1;  // 0xdffff

        // Fixed value referencing the BytesTrie bytes.
        private byte[] bytes_;
        private int root_;

        // Iterator variables.

        // Index of next trie byte to read. Negative if no more matches.
        private int pos_;
        // Remaining length of a linear-match node, minus 1. Negative if not in such a node.
        private int remainingMatchLength_;
    }
}
