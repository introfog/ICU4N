﻿namespace ICU4N.Impl
{
    /// <summary>
    /// Ported code from ICU punycode.c 
    /// </summary>
    /// <author>ram</author>
    public static partial class Punycode // ICU4N specific - made class static since it only has static members
    {
        /* Punycode parameters for Bootstring */
        private const int BASE = 36;
        private const int TMIN = 1;
        private const int TMAX = 26;
        private const int SKEW = 38;
        private const int DAMP = 700;
        private const int INITIAL_BIAS = 72;
        private const int INITIAL_N = 0x80;

        /* "Basic" Unicode/ASCII code points */
        private const char HYPHEN = (char)0x2d;
        private const char DELIMITER = HYPHEN;

        private const int ZERO = 0x30;
        //private const int NINE           = 0x39;

        private const int SMALL_A = 0x61;
        private const int SMALL_Z = 0x7a;

        private const int CAPITAL_A = 0x41;
        private const int CAPITAL_Z = 0x5a;

        private static int AdaptBias(int delta, int length, bool firstTime)
        {
            if (firstTime)
            {
                delta /= DAMP;
            }
            else
            {
                delta /= 2;
            }
            delta += delta / length;

            int count = 0;
            for (; delta > ((BASE - TMIN) * TMAX) / 2; count += BASE)
            {
                delta /= (BASE - TMIN);
            }

            return count + (((BASE - TMIN + 1) * delta) / (delta + SKEW));
        }

        /// <summary>
        /// basicToDigit[] contains the numeric value of a basic code
        /// point (for use in representing integers) in the range 0 to
        /// BASE-1, or -1 if b is does not represent a value.
        /// </summary>
        internal static readonly int[] basicToDigit = new int[]{
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                26, 27, 28, 29, 30, 31, 32, 33, 34, 35, -1, -1, -1, -1, -1, -1,

                -1,  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14,
                15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, -1, -1, -1, -1, -1,

                -1,  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14,
                15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, -1, -1, -1, -1, -1,

                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
            };

        //CLOVER:OFF
        private static char AsciiCaseMap(char b, bool uppercase)
        {
            if (uppercase)
            {
                if (SMALL_A <= b && b <= SMALL_Z)
                {
                    b = (char)(b - (SMALL_A - CAPITAL_A));
                }
            }
            else
            {
                if (CAPITAL_A <= b && b <= CAPITAL_Z)
                {
                    b = (char)(b + (SMALL_A - CAPITAL_A));
                }
            }
            return b;
        }
        //CLOVER:ON
        /// <summary>
        /// <see cref="DigitToBasic(int, bool)"/> returns the basic code point whose value
        /// (when used for representing integers) is d, which must be in the
        /// range 0 to BASE-1. The lowercase form is used unless the uppercase flag is
        /// nonzero, in which case the uppercase form is used.
        /// </summary>
        private static char DigitToBasic(int digit, bool uppercase)
        {
            /*  0..25 map to ASCII a..z or A..Z */
            /* 26..35 map to ASCII 0..9         */
            if (digit < 26)
            {
                if (uppercase)
                {
                    return (char)(CAPITAL_A + digit);
                }
                else
                {
                    return (char)(SMALL_A + digit);
                }
            }
            else
            {
                return (char)((ZERO - 26) + digit);
            }
        }

        // ICU4N specific - Encode(ICharSequence src, bool[] caseFlags) moved to PunycodeExtension.tt

        private static bool IsBasic(int ch)
        {
            return (ch < INITIAL_N);
        }
        //CLOVER:OFF
        private static bool IsBasicUpperCase(int ch)
        {
            return (CAPITAL_A <= ch && ch >= CAPITAL_Z);
        }
        //CLOVER:ON
        private static bool IsSurrogate(int ch)
        {
            return (((ch) & 0xfffff800) == 0xd800);
        }

        // ICU4N specific - Decode(ICharSequence src, bool[] caseFlags) moved to PunycodeExtension.tt
    }
}
