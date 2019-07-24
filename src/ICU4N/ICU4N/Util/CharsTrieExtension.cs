﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using ICU4N.Support.Text;
using System.Text;

namespace ICU4N.Util
{
    public sealed partial class CharsTrie
    {

        /// <summary>
        /// Constructs a CharsTrie reader instance.
        /// </summary>
        /// <remarks>
        /// The <see cref="string"/> must contain a copy of a char sequence from the <see cref="CharsTrieBuilder"/>,
        /// with the offset indicating the first char of that sequence.
        /// The <see cref="CharsTrie"/> object will not read more chars than
        /// the <see cref="CharsTrieBuilder"/> generated in the corresponding 
        /// <see cref="CharsTrieBuilder.Build(StringTrieBuilder.Option)"/> call.
        /// <para/>
        /// The <see cref="string"/> is not copied/cloned and must not be modified while
        /// the <see cref="CharsTrie"/> object is in use.
        /// </remarks>
        /// <param name="trieChars"><see cref="string"/> that contains the serialized trie.</param>
        /// <param name="offset">Root offset of the trie in the <see cref="string"/>.</param>
        /// <stable>ICU 4.8</stable>
        public CharsTrie(string trieChars, int offset) 
        {
            chars_ = trieChars.ToCharSequence();
            pos_ = root_ = offset;
            remainingMatchLength_ = -1;
        }

        /// <summary>
        /// Constructs a CharsTrie reader instance.
        /// </summary>
        /// <remarks>
        /// The <see cref="StringBuilder"/> must contain a copy of a char sequence from the <see cref="CharsTrieBuilder"/>,
        /// with the offset indicating the first char of that sequence.
        /// The <see cref="CharsTrie"/> object will not read more chars than
        /// the <see cref="CharsTrieBuilder"/> generated in the corresponding 
        /// <see cref="CharsTrieBuilder.Build(StringTrieBuilder.Option)"/> call.
        /// <para/>
        /// The <see cref="StringBuilder"/> is not copied/cloned and must not be modified while
        /// the <see cref="CharsTrie"/> object is in use.
        /// </remarks>
        /// <param name="trieChars"><see cref="StringBuilder"/> that contains the serialized trie.</param>
        /// <param name="offset">Root offset of the trie in the <see cref="StringBuilder"/>.</param>
        /// <stable>ICU 4.8</stable>
        public CharsTrie(StringBuilder trieChars, int offset) 
        {
            chars_ = trieChars.ToCharSequence();
            pos_ = root_ = offset;
            remainingMatchLength_ = -1;
        }

        /// <summary>
        /// Constructs a CharsTrie reader instance.
        /// </summary>
        /// <remarks>
        /// The <see cref="T:char[]"/> must contain a copy of a char sequence from the <see cref="CharsTrieBuilder"/>,
        /// with the offset indicating the first char of that sequence.
        /// The <see cref="CharsTrie"/> object will not read more chars than
        /// the <see cref="CharsTrieBuilder"/> generated in the corresponding 
        /// <see cref="CharsTrieBuilder.Build(StringTrieBuilder.Option)"/> call.
        /// <para/>
        /// The <see cref="T:char[]"/> is not copied/cloned and must not be modified while
        /// the <see cref="CharsTrie"/> object is in use.
        /// </remarks>
        /// <param name="trieChars"><see cref="T:char[]"/> that contains the serialized trie.</param>
        /// <param name="offset">Root offset of the trie in the <see cref="T:char[]"/>.</param>
        /// <stable>ICU 4.8</stable>
        public CharsTrie(char[] trieChars, int offset) 
        {
            chars_ = trieChars.ToCharSequence();
            pos_ = root_ = offset;
            remainingMatchLength_ = -1;
        }

        /// <summary>
        /// Constructs a CharsTrie reader instance.
        /// </summary>
        /// <remarks>
        /// The <see cref="ICharSequence"/> must contain a copy of a char sequence from the <see cref="CharsTrieBuilder"/>,
        /// with the offset indicating the first char of that sequence.
        /// The <see cref="CharsTrie"/> object will not read more chars than
        /// the <see cref="CharsTrieBuilder"/> generated in the corresponding 
        /// <see cref="CharsTrieBuilder.Build(StringTrieBuilder.Option)"/> call.
        /// <para/>
        /// The <see cref="ICharSequence"/> is not copied/cloned and must not be modified while
        /// the <see cref="CharsTrie"/> object is in use.
        /// </remarks>
        /// <param name="trieChars"><see cref="ICharSequence"/> that contains the serialized trie.</param>
        /// <param name="offset">Root offset of the trie in the <see cref="ICharSequence"/>.</param>
        /// <stable>ICU 4.8</stable>
        internal CharsTrie(ICharSequence trieChars, int offset) 
        {
            chars_ = trieChars.ToCharSequence();
            pos_ = root_ = offset;
            remainingMatchLength_ = -1;
        }

        /// <summary>
        /// Traverses the trie from the current state for this string.
        /// Equivalent to
        /// <code>
        ///     if(!result.HasNext()) return Result.NoMatch;
        ///     result=Next(c);
        ///     return result;
        /// </code>
        /// </summary>
        /// <param name="s">Contains a string.</param>
        /// <param name="sIndex">The start index of the string in <paramref name="s"/>.</param>
        /// <param name="sLimit">The (exclusive) end index of the string in <paramref name="s"/>.</param>
        /// <returns>The match/value <see cref="Result"/>.</returns>
        /// <stable>ICU 4.8</stable>
        public Result Next(string s, int sIndex, int sLimit)
        {
            if (sIndex >= sLimit)
            {
                // Empty input.
                return Current;
            }
            int pos = pos_;
            if (pos < 0)
            {
                return Result.NoMatch;
            }
            int length = remainingMatchLength_;  // Actual remaining match length minus 1.
            for (; ; )
            {
                // Fetch the next input unit, if there is one.
                // Continue a linear-match node.
                char inUnit;
                for (; ; )
                {
                    if (sIndex == sLimit)
                    {
                        remainingMatchLength_ = length;
                        pos_ = pos;
                        int node2;
                        return (length < 0 && (node2 = chars_[pos]) >= kMinValueLead) ?
                                valueResults_[node2 >> 15] : Result.NoValue;
                    }
                    inUnit = s[sIndex++];
                    if (length < 0)
                    {
                        remainingMatchLength_ = length;
                        break;
                    }
                    if (inUnit != chars_[pos])
                    {
                        Stop();
                        return Result.NoMatch;
                    }
                    ++pos;
                    --length;
                }
                int node = chars_[pos++];
                for (; ; )
                {
                    if (node < kMinLinearMatch)
                    {
                        Result result = BranchNext(pos, node, inUnit);
                        if (result == Result.NoMatch)
                        {
                            return Result.NoMatch;
                        }
                        // Fetch the next input unit, if there is one.
                        if (sIndex == sLimit)
                        {
                            return result;
                        }
                        if (result == Result.FinalValue)
                        {
                            // No further matching units.
                            Stop();
                            return Result.NoMatch;
                        }
                        inUnit = s[sIndex++];
                        pos = pos_;  // branchNext() advanced pos and wrote it to pos_ .
                        node = chars_[pos++];
                    }
                    else if (node < kMinValueLead)
                    {
                        // Match length+1 units.
                        length = node - kMinLinearMatch;  // Actual match length minus 1.
                        if (inUnit != chars_[pos])
                        {
                            Stop();
                            return Result.NoMatch;
                        }
                        ++pos;
                        --length;
                        break;
                    }
                    else if ((node & kValueIsFinal) != 0)
                    {
                        // No further matching units.
                        Stop();
                        return Result.NoMatch;
                    }
                    else
                    {
                        // Skip intermediate value.
                        pos = SkipNodeValue(pos, node);
                        node &= kNodeTypeMask;
                    }
                }
            }
        }

        /// <summary>
        /// Traverses the trie from the current state for this string.
        /// Equivalent to
        /// <code>
        ///     if(!result.HasNext()) return Result.NoMatch;
        ///     result=Next(c);
        ///     return result;
        /// </code>
        /// </summary>
        /// <param name="s">Contains a string.</param>
        /// <param name="sIndex">The start index of the string in <paramref name="s"/>.</param>
        /// <param name="sLimit">The (exclusive) end index of the string in <paramref name="s"/>.</param>
        /// <returns>The match/value <see cref="Result"/>.</returns>
        /// <stable>ICU 4.8</stable>
        public Result Next(StringBuilder s, int sIndex, int sLimit)
        {
            if (sIndex >= sLimit)
            {
                // Empty input.
                return Current;
            }
            int pos = pos_;
            if (pos < 0)
            {
                return Result.NoMatch;
            }
            int length = remainingMatchLength_;  // Actual remaining match length minus 1.
            for (; ; )
            {
                // Fetch the next input unit, if there is one.
                // Continue a linear-match node.
                char inUnit;
                for (; ; )
                {
                    if (sIndex == sLimit)
                    {
                        remainingMatchLength_ = length;
                        pos_ = pos;
                        int node2;
                        return (length < 0 && (node2 = chars_[pos]) >= kMinValueLead) ?
                                valueResults_[node2 >> 15] : Result.NoValue;
                    }
                    inUnit = s[sIndex++];
                    if (length < 0)
                    {
                        remainingMatchLength_ = length;
                        break;
                    }
                    if (inUnit != chars_[pos])
                    {
                        Stop();
                        return Result.NoMatch;
                    }
                    ++pos;
                    --length;
                }
                int node = chars_[pos++];
                for (; ; )
                {
                    if (node < kMinLinearMatch)
                    {
                        Result result = BranchNext(pos, node, inUnit);
                        if (result == Result.NoMatch)
                        {
                            return Result.NoMatch;
                        }
                        // Fetch the next input unit, if there is one.
                        if (sIndex == sLimit)
                        {
                            return result;
                        }
                        if (result == Result.FinalValue)
                        {
                            // No further matching units.
                            Stop();
                            return Result.NoMatch;
                        }
                        inUnit = s[sIndex++];
                        pos = pos_;  // branchNext() advanced pos and wrote it to pos_ .
                        node = chars_[pos++];
                    }
                    else if (node < kMinValueLead)
                    {
                        // Match length+1 units.
                        length = node - kMinLinearMatch;  // Actual match length minus 1.
                        if (inUnit != chars_[pos])
                        {
                            Stop();
                            return Result.NoMatch;
                        }
                        ++pos;
                        --length;
                        break;
                    }
                    else if ((node & kValueIsFinal) != 0)
                    {
                        // No further matching units.
                        Stop();
                        return Result.NoMatch;
                    }
                    else
                    {
                        // Skip intermediate value.
                        pos = SkipNodeValue(pos, node);
                        node &= kNodeTypeMask;
                    }
                }
            }
        }

        /// <summary>
        /// Traverses the trie from the current state for this string.
        /// Equivalent to
        /// <code>
        ///     if(!result.HasNext()) return Result.NoMatch;
        ///     result=Next(c);
        ///     return result;
        /// </code>
        /// </summary>
        /// <param name="s">Contains a string.</param>
        /// <param name="sIndex">The start index of the string in <paramref name="s"/>.</param>
        /// <param name="sLimit">The (exclusive) end index of the string in <paramref name="s"/>.</param>
        /// <returns>The match/value <see cref="Result"/>.</returns>
        /// <stable>ICU 4.8</stable>
        public Result Next(char[] s, int sIndex, int sLimit)
        {
            if (sIndex >= sLimit)
            {
                // Empty input.
                return Current;
            }
            int pos = pos_;
            if (pos < 0)
            {
                return Result.NoMatch;
            }
            int length = remainingMatchLength_;  // Actual remaining match length minus 1.
            for (; ; )
            {
                // Fetch the next input unit, if there is one.
                // Continue a linear-match node.
                char inUnit;
                for (; ; )
                {
                    if (sIndex == sLimit)
                    {
                        remainingMatchLength_ = length;
                        pos_ = pos;
                        int node2;
                        return (length < 0 && (node2 = chars_[pos]) >= kMinValueLead) ?
                                valueResults_[node2 >> 15] : Result.NoValue;
                    }
                    inUnit = s[sIndex++];
                    if (length < 0)
                    {
                        remainingMatchLength_ = length;
                        break;
                    }
                    if (inUnit != chars_[pos])
                    {
                        Stop();
                        return Result.NoMatch;
                    }
                    ++pos;
                    --length;
                }
                int node = chars_[pos++];
                for (; ; )
                {
                    if (node < kMinLinearMatch)
                    {
                        Result result = BranchNext(pos, node, inUnit);
                        if (result == Result.NoMatch)
                        {
                            return Result.NoMatch;
                        }
                        // Fetch the next input unit, if there is one.
                        if (sIndex == sLimit)
                        {
                            return result;
                        }
                        if (result == Result.FinalValue)
                        {
                            // No further matching units.
                            Stop();
                            return Result.NoMatch;
                        }
                        inUnit = s[sIndex++];
                        pos = pos_;  // branchNext() advanced pos and wrote it to pos_ .
                        node = chars_[pos++];
                    }
                    else if (node < kMinValueLead)
                    {
                        // Match length+1 units.
                        length = node - kMinLinearMatch;  // Actual match length minus 1.
                        if (inUnit != chars_[pos])
                        {
                            Stop();
                            return Result.NoMatch;
                        }
                        ++pos;
                        --length;
                        break;
                    }
                    else if ((node & kValueIsFinal) != 0)
                    {
                        // No further matching units.
                        Stop();
                        return Result.NoMatch;
                    }
                    else
                    {
                        // Skip intermediate value.
                        pos = SkipNodeValue(pos, node);
                        node &= kNodeTypeMask;
                    }
                }
            }
        }

        /// <summary>
        /// Traverses the trie from the current state for this string.
        /// Equivalent to
        /// <code>
        ///     if(!result.HasNext()) return Result.NoMatch;
        ///     result=Next(c);
        ///     return result;
        /// </code>
        /// </summary>
        /// <param name="s">Contains a string.</param>
        /// <param name="sIndex">The start index of the string in <paramref name="s"/>.</param>
        /// <param name="sLimit">The (exclusive) end index of the string in <paramref name="s"/>.</param>
        /// <returns>The match/value <see cref="Result"/>.</returns>
        /// <stable>ICU 4.8</stable>
        internal Result Next(ICharSequence s, int sIndex, int sLimit)
        {
            if (sIndex >= sLimit)
            {
                // Empty input.
                return Current;
            }
            int pos = pos_;
            if (pos < 0)
            {
                return Result.NoMatch;
            }
            int length = remainingMatchLength_;  // Actual remaining match length minus 1.
            for (; ; )
            {
                // Fetch the next input unit, if there is one.
                // Continue a linear-match node.
                char inUnit;
                for (; ; )
                {
                    if (sIndex == sLimit)
                    {
                        remainingMatchLength_ = length;
                        pos_ = pos;
                        int node2;
                        return (length < 0 && (node2 = chars_[pos]) >= kMinValueLead) ?
                                valueResults_[node2 >> 15] : Result.NoValue;
                    }
                    inUnit = s[sIndex++];
                    if (length < 0)
                    {
                        remainingMatchLength_ = length;
                        break;
                    }
                    if (inUnit != chars_[pos])
                    {
                        Stop();
                        return Result.NoMatch;
                    }
                    ++pos;
                    --length;
                }
                int node = chars_[pos++];
                for (; ; )
                {
                    if (node < kMinLinearMatch)
                    {
                        Result result = BranchNext(pos, node, inUnit);
                        if (result == Result.NoMatch)
                        {
                            return Result.NoMatch;
                        }
                        // Fetch the next input unit, if there is one.
                        if (sIndex == sLimit)
                        {
                            return result;
                        }
                        if (result == Result.FinalValue)
                        {
                            // No further matching units.
                            Stop();
                            return Result.NoMatch;
                        }
                        inUnit = s[sIndex++];
                        pos = pos_;  // branchNext() advanced pos and wrote it to pos_ .
                        node = chars_[pos++];
                    }
                    else if (node < kMinValueLead)
                    {
                        // Match length+1 units.
                        length = node - kMinLinearMatch;  // Actual match length minus 1.
                        if (inUnit != chars_[pos])
                        {
                            Stop();
                            return Result.NoMatch;
                        }
                        ++pos;
                        --length;
                        break;
                    }
                    else if ((node & kValueIsFinal) != 0)
                    {
                        // No further matching units.
                        Stop();
                        return Result.NoMatch;
                    }
                    else
                    {
                        // Skip intermediate value.
                        pos = SkipNodeValue(pos, node);
                        node &= kNodeTypeMask;
                    }
                }
            }
        }
    }
}