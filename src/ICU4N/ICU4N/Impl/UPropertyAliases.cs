﻿using ICU4N.Lang;
using ICU4N.Support;
using ICU4N.Support.IO;
using ICU4N.Support.Text;
using ICU4N.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Resources;
using System.Text;


namespace ICU4N.Impl
{
    public sealed class UPropertyAliases
    {
        // Byte offsets from the start of the data, after the generic header.
        private const int IX_VALUE_MAPS_OFFSET = 0;
        private const int IX_BYTE_TRIES_OFFSET = 1;
        private const int IX_NAME_GROUPS_OFFSET = 2;
        private const int IX_RESERVED3_OFFSET = 3;
        // private const int IX_RESERVED4_OFFSET=4;
        // private const int IX_TOTAL_SIZE=5;

        // Other values.
        // private const int IX_MAX_NAME_LENGTH=6;
        // private const int IX_RESERVED7=7;
        // private const int IX_COUNT=8;

        //----------------------------------------------------------------
        // Runtime data.  This is an unflattened representation of the
        // data in pnames.icu.

        private int[] valueMaps;
        private byte[] bytesTries;
        private String nameGroups;

        private sealed class IsAcceptable : IAuthenticate
        {
            // @Override when we switch to Java 6
            public bool IsDataVersionAcceptable(byte[] version)
            {
                return version[0] == 2;
            }
        }
        private static readonly IsAcceptable IS_ACCEPTABLE = new IsAcceptable();
        private static readonly int DATA_FORMAT = 0x706E616D;  // "pnam"

        private void Load(ByteBuffer bytes)
        {
            //dataVersion=ICUBinary.readHeaderAndDataVersion(bytes, DATA_FORMAT, IS_ACCEPTABLE);
            ICUBinary.ReadHeader(bytes, DATA_FORMAT, IS_ACCEPTABLE);
            int indexesLength = bytes.GetInt32() / 4;  // inIndexes[IX_VALUE_MAPS_OFFSET]/4
            if (indexesLength < 8)
            {  // formatVersion 2 initially has 8 indexes
                throw new IOException("pnames.icu: not enough indexes");
            }
            int[]
            inIndexes = new int[indexesLength];
            inIndexes[0] = indexesLength * 4;
            for (int i = 1; i < indexesLength; ++i)
            {
                inIndexes[i] = bytes.GetInt32();
            }

            // Read the valueMaps.
            int offset = inIndexes[IX_VALUE_MAPS_OFFSET];
            int nextOffset = inIndexes[IX_BYTE_TRIES_OFFSET];
            int numInts = (nextOffset - offset) / 4;
            valueMaps = ICUBinary.GetInts(bytes, numInts, 0);

            // Read the bytesTries.
            offset = nextOffset;
            nextOffset = inIndexes[IX_NAME_GROUPS_OFFSET];
            int numBytes = nextOffset - offset;
            bytesTries = new byte[numBytes];
            bytes.Get(bytesTries);

            // Read the nameGroups and turn them from ASCII bytes into a Java String.
            offset = nextOffset;
            nextOffset = inIndexes[IX_RESERVED3_OFFSET];
            numBytes = nextOffset - offset;
            StringBuilder sb = new StringBuilder(numBytes);
            for (int i = 0; i < numBytes; ++i)
            {
                sb.Append((char)bytes.Get());
            }
            nameGroups = sb.ToString();
        }

        private UPropertyAliases()
        {
            ByteBuffer bytes = ICUBinary.GetRequiredData("pnames.icu");
            Load(bytes);
        }

        private int FindProperty(int property)
        {
            int i = 1;  // valueMaps index, initially after numRanges
            for (int numRanges = valueMaps[0]; numRanges > 0; --numRanges)
            {
                // Read and skip the start and limit of this range.
                int start = valueMaps[i];
                int limit = valueMaps[i + 1];
                i += 2;
                if (property < start)
                {
                    break;
                }
                if (property < limit)
                {
                    return i + (property - start) * 2;
                }
                i += (limit - start) * 2;  // Skip all entries for this range.
            }
            return 0;
        }

        private int FindPropertyValueNameGroup(int valueMapIndex, int value)
        {
            if (valueMapIndex == 0)
            {
                return 0;  // The property does not have named values.
            }
            ++valueMapIndex;  // Skip the BytesTrie offset.
            int numRanges = valueMaps[valueMapIndex++];
            if (numRanges < 0x10)
            {
                // Ranges of values.
                for (; numRanges > 0; --numRanges)
                {
                    // Read and skip the start and limit of this range.
                    int start = valueMaps[valueMapIndex];
                    int limit = valueMaps[valueMapIndex + 1];
                    valueMapIndex += 2;
                    if (value < start)
                    {
                        break;
                    }
                    if (value < limit)
                    {
                        return valueMaps[valueMapIndex + value - start];
                    }
                    valueMapIndex += limit - start;  // Skip all entries for this range.
                }
            }
            else
            {
                // List of values.
                int valuesStart = valueMapIndex;
                int nameGroupOffsetsStart = valueMapIndex + numRanges - 0x10;
                do
                {
                    int v = valueMaps[valueMapIndex];
                    if (value < v)
                    {
                        break;
                    }
                    if (value == v)
                    {
                        return valueMaps[nameGroupOffsetsStart + valueMapIndex - valuesStart];
                    }
                } while (++valueMapIndex < nameGroupOffsetsStart);
            }
            return 0;
        }

        private string GetName(int nameGroupsIndex, int nameIndex)
        {
            int numNames = nameGroups[nameGroupsIndex++];
            if (nameIndex < 0 || numNames <= nameIndex)
            {
                throw new IcuArgumentException("Invalid property (value) name choice");
            }
            // Skip nameIndex names.
            for (; nameIndex > 0; --nameIndex)
            {
                while (0 != nameGroups[nameGroupsIndex++]) { }
            }
            // Find the end of this name.
            int nameStart = nameGroupsIndex;
            while (0 != nameGroups[nameGroupsIndex])
            {
                ++nameGroupsIndex;
            }
            if (nameStart == nameGroupsIndex)
            {
                return null;  // no name (Property[Value]Aliases.txt has "n/a")
            }
            return nameGroups.Substring(nameStart, nameGroupsIndex - nameStart);
        }

        private static int AsciiToLowercase(int c)
        {
            return 'A' <= c && c <= 'Z' ? c + 0x20 : c;
        }

        private bool ContainsName(BytesTrie trie, ICharSequence name)
        {
            BytesTrieResult result = BytesTrieResult.NO_VALUE;
            for (int i = 0; i < name.Length; ++i)
            {
                int c = name[i];
                // Ignore delimiters '-', '_', and ASCII White_Space.
                if (c == '-' || c == '_' || c == ' ' || (0x09 <= c && c <= 0x0d))
                {
                    continue;
                }
                if (!result.HasNext())
                {
                    return false;
                }
                c = AsciiToLowercase(c);
                result = trie.Next(c);
            }
            return result.HasValue();
        }

        //----------------------------------------------------------------
        // Public API

        public static readonly UPropertyAliases INSTANCE;

        static UPropertyAliases()
        {
            try
            {
                INSTANCE = new UPropertyAliases();
            }
            catch (IOException e)
            {
                ///CLOVER:OFF
                MissingManifestResourceException mre = new MissingManifestResourceException(
                        "Could not construct UPropertyAliases. Missing pnames.icu", e);
                throw mre;
                ///CLOVER:ON
            }
        }

        /**
         * Returns a property name given a property enum.
         * Multiple names may be available for each property;
         * the nameChoice selects among them.
         */
        public string GetPropertyName(int property, int nameChoice)
        {
            int valueMapIndex = FindProperty(property);
            if (valueMapIndex == 0)
            {
                throw new ArgumentException(
                        "Invalid property enum " + property + " (0x" + string.Format("{0:x2}", property) + ")");
            }
            return GetName(valueMaps[valueMapIndex], nameChoice);
        }

        /**
         * Returns a value name given a property enum and a value enum.
         * Multiple names may be available for each value;
         * the nameChoice selects among them.
         */
        public string GetPropertyValueName(UnicodeProperty property, int value, NameChoice nameChoice)
        {
            int valueMapIndex = FindProperty((int)property);
            if (valueMapIndex == 0)
            {
                throw new ArgumentException(
                        "Invalid property enum " + property + " (0x" + string.Format("{0:x2}", property) + ")");
            }
            int nameGroupOffset = FindPropertyValueNameGroup(valueMaps[valueMapIndex + 1], value);
            if (nameGroupOffset == 0)
            {
                throw new ArgumentException(
                        "Property " + property + " (0x" + string.Format("{0:x2}", property) +
                        ") does not have named values");
            }
            return GetName(nameGroupOffset, (int)nameChoice);
        }

        private int GetPropertyOrValueEnum(int bytesTrieOffset, ICharSequence alias)
        {
            BytesTrie trie = new BytesTrie(bytesTries, bytesTrieOffset);
            if (ContainsName(trie, alias))
            {
                return trie.GetValue();
            }
            else
            {
                return (int)UnicodeProperty.UNDEFINED;
            }
        }

        /// <summary>
        /// Returns a property enum given one of its property names.
        /// If the property name is not known, this method returns
        /// <see cref="UnicodeProperty.UNDEFINED"/>.
        /// </summary>
        /// <param name="alias"></param>
        /// <returns></returns>
        public int GetPropertyEnum(string alias)
        {
            return GetPropertyEnum(alias.ToCharSequence());
        }

        /// <summary>
        /// Returns a property enum given one of its property names.
        /// If the property name is not known, this method returns
        /// <see cref="UnicodeProperty.UNDEFINED"/>.
        /// </summary>
        /// <param name="alias"></param>
        /// <returns></returns>
        public int GetPropertyEnum(StringBuilder alias)
        {
            return GetPropertyEnum(alias.ToCharSequence());
        }

        /// <summary>
        /// Returns a property enum given one of its property names.
        /// If the property name is not known, this method returns
        /// <see cref="UnicodeProperty.UNDEFINED"/>.
        /// </summary>
        /// <param name="alias"></param>
        /// <returns></returns>
        public int GetPropertyEnum(char[] alias)
        {
            return GetPropertyEnum(alias.ToCharSequence());
        }

        /// <summary>
        /// Returns a property enum given one of its property names.
        /// If the property name is not known, this method returns
        /// <see cref="UnicodeProperty.UNDEFINED"/>.
        /// </summary>
        /// <param name="alias"></param>
        /// <returns></returns>
        internal int GetPropertyEnum(ICharSequence alias)
        {
            return GetPropertyOrValueEnum(0, alias);
        }

        /// <summary>
        /// Returns a value enum given a property enum and one of its value names.
        /// </summary>
        public int GetPropertyValueEnum(int property, string alias)
        {
            return GetPropertyValueEnum(property, alias.ToCharSequence());
        }

        /// <summary>
        /// Returns a value enum given a property enum and one of its value names.
        /// </summary>
        public int GetPropertyValueEnum(int property, StringBuilder alias)
        {
            return GetPropertyValueEnum(property, alias.ToCharSequence());
        }

        /// <summary>
        /// Returns a value enum given a property enum and one of its value names.
        /// </summary>
        public int GetPropertyValueEnum(int property, char[] alias)
        {
            return GetPropertyValueEnum(property, alias.ToCharSequence());
        }

        /// <summary>
        /// Returns a value enum given a property enum and one of its value names.
        /// </summary>
        internal int GetPropertyValueEnum(int property, ICharSequence alias)
        {
            int valueMapIndex = FindProperty(property);
            if (valueMapIndex == 0)
            {
                throw new ArgumentException(
                        "Invalid property enum " + property + " (0x" + string.Format("{0:x2}", property) + ")");
            }
            valueMapIndex = valueMaps[valueMapIndex + 1];
            if (valueMapIndex == 0)
            {
                throw new ArgumentException(
                        "Property " + property + " (0x" + string.Format("{0:x2}", property) +
                        ") does not have named values");
            }
            // valueMapIndex is the start of the property's valueMap,
            // where the first word is the BytesTrie offset.
            return GetPropertyOrValueEnum(valueMaps[valueMapIndex], alias);
        }

        /// <summary>
        /// Returns a value enum given a property enum and one of its value names.
        /// </summary>
        public bool TryGetPropertyValueEnum(int property, string alias, out int result)
        {
            return TryGetPropertyValueEnum(property, alias.ToCharSequence(), out result);
        }

        /// <summary>
        /// Returns a value enum given a property enum and one of its value names.
        /// </summary>
        public bool TryGetPropertyValueEnum(int property, StringBuilder alias, out int result)
        {
            return TryGetPropertyValueEnum(property, alias.ToCharSequence(), out result);
        }

        /// <summary>
        /// Returns a value enum given a property enum and one of its value names.
        /// </summary>
        public bool TryGetPropertyValueEnum(int property, char[] alias, out int result)
        {
            return TryGetPropertyValueEnum(property, alias.ToCharSequence(), out result);
        }

        /// <summary>
        /// Returns a value enum given a property enum and one of its value names.
        /// </summary>
        internal bool TryGetPropertyValueEnum(int property, ICharSequence alias, out int result)
        {
            result = GetPropertyValueEnumNoThrow(property, alias);
            if (result == (int)UnicodeProperty.UNDEFINED)
                return false;
            return true;
        }

        /// <summary>
        /// Returns a value enum given a property enum and one of its value names. Does not throw.
        /// </summary>
        /// <returns>value enum, or UProperty.UNDEFINED if not defined for that property</returns>
        internal int GetPropertyValueEnumNoThrow(int property, ICharSequence alias)
        {
            int valueMapIndex = FindProperty(property);
            if (valueMapIndex == 0)
            {
                return (int)UnicodeProperty.UNDEFINED;
            }
            valueMapIndex = valueMaps[valueMapIndex + 1];
            if (valueMapIndex == 0)
            {
                return (int)UnicodeProperty.UNDEFINED;
            }
            // valueMapIndex is the start of the property's valueMap,
            // where the first word is the BytesTrie offset.
            return GetPropertyOrValueEnum(valueMaps[valueMapIndex], alias);
        }

        /**
         * Compare two property names, returning <0, 0, or >0.  The
         * comparison is that described as "loose" matching in the
         * Property*Aliases.txt files.
         */
        public static int Compare(string stra, string strb)
        {
            // Note: This implementation is a literal copy of
            // uprv_comparePropertyNames.  It can probably be improved.
            int istra = 0, istrb = 0, rc;
            int cstra = 0, cstrb = 0;
            for (; ; )
            {
                /* Ignore delimiters '-', '_', and ASCII White_Space */
                while (istra < stra.Length)
                {
                    cstra = stra[istra];
                    switch (cstra)
                    {
                        case '-':
                        case '_':
                        case ' ':
                        case '\t':
                        case '\n':
                        case 0xb/*\v*/:
                        case '\f':
                        case '\r':
                            ++istra;
                            continue;
                    }
                    break;
                }

                while (istrb < strb.Length)
                {
                    cstrb = strb[istrb];
                    switch (cstrb)
                    {
                        case '-':
                        case '_':
                        case ' ':
                        case '\t':
                        case '\n':
                        case 0xb/*\v*/:
                        case '\f':
                        case '\r':
                            ++istrb;
                            continue;
                    }
                    break;
                }

                /* If we reach the ends of both strings then they match */
                bool endstra = istra == stra.Length;
                bool endstrb = istrb == strb.Length;
                if (endstra)
                {
                    if (endstrb) return 0;
                    cstra = 0;
                }
                else if (endstrb)
                {
                    cstrb = 0;
                }

                rc = AsciiToLowercase(cstra) - AsciiToLowercase(cstrb);
                if (rc != 0)
                {
                    return rc;
                }

                ++istra;
                ++istrb;
            }
        }
    }
}
