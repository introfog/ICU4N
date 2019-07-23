﻿using ICU4N.Impl;
using ICU4N.Lang;
using ICU4N.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace ICU4N.Text
{
    /// <summary>
    /// A transliterator that performs locale-sensitive ToLower()
    /// case mapping.
    /// </summary>
    internal class LowercaseTransliterator : Transliterator
    {
        /// <summary>
        /// Package accessible ID.
        /// </summary>
        internal static readonly string _ID = "Any-Lower";

        // TODO: Add variants for tr/az, lt, default = default locale: ICU ticket #12720

        /// <summary>
        /// System registration hook.
        /// </summary>
        internal static void Register()
        {
            Transliterator.RegisterFactory(_ID, new Transliterator.Factory(getInstance: (id) =>
            {
                return new LowercaseTransliterator(ULocale.US);
            }));

            Transliterator.RegisterSpecialInverse("Lower", "Upper", true);
        }

        private readonly ULocale locale;

        private readonly UCaseProps csp;
        private ReplaceableContextIterator iter;
        private StringBuilder result;
        private int caseLocale;

        /// <summary>
        /// Constructs a transliterator.
        /// </summary>
        public LowercaseTransliterator(ULocale loc)
                : base(_ID, null)
        {
            locale = loc;
            csp = UCaseProps.Instance;
            iter = new ReplaceableContextIterator();
            result = new StringBuilder();
            caseLocale = UCaseProps.GetCaseLocale(locale);
        }

        /// <summary>
        /// Implements <see cref="Transliterator.HandleTransliterate(IReplaceable, TransliterationPosition, bool)"/>.
        /// </summary>
        protected override void HandleTransliterate(IReplaceable text,
                                           TransliterationPosition offsets, bool isIncremental)
        {
            lock (this)
            {
                if (csp == null)
                {
                    return;
                }

                if (offsets.Start >= offsets.Limit)
                {
                    return;
                }

                iter.SetText(text);
                result.Length = 0;
                int c, delta;

                // Walk through original string
                // If there is a case change, modify corresponding position in replaceable

                iter.SetIndex(offsets.Start);
                iter.SetLimit(offsets.Limit);
                iter.SetContextLimits(offsets.ContextStart, offsets.ContextLimit);
                while ((c = iter.NextCaseMapCP()) >= 0)
                {
                    c = csp.ToFullLower(c, iter, result, caseLocale);

                    if (iter.DidReachLimit && isIncremental)
                    {
                        // the case mapping function tried to look beyond the context limit
                        // wait for more input
                        offsets.Start = iter.CaseMapCPStart;
                        return;
                    }

                    /* decode the result */
                    if (c < 0)
                    {
                        /* c mapped to itself, no change */
                        continue;
                    }
                    else if (c <= UCaseProps.MAX_STRING_LENGTH)
                    {
                        /* replace by the mapping string */
                        delta = iter.Replace(result.ToString());
                        result.Length = 0;
                    }
                    else
                    {
                        /* replace by single-code point mapping */
                        delta = iter.Replace(UTF16.ValueOf(c));
                    }

                    if (delta != 0)
                    {
                        offsets.Limit += delta;
                        offsets.ContextLimit += delta;
                    }
                }
                offsets.Start = offsets.Limit;
            }
        }

        // NOTE: normally this would be static, but because the results vary by locale....
        SourceTargetUtility sourceTargetUtility = null;

        /// <seealso cref="Transliterator.AddSourceTargetSet(UnicodeSet, UnicodeSet, UnicodeSet)"/>.
#pragma warning disable 672
        public override void AddSourceTargetSet(UnicodeSet inputFilter, UnicodeSet sourceSet, UnicodeSet targetSet)
#pragma warning restore 672
        {
            lock (this)
            {
                if (sourceTargetUtility == null)
                {
                    sourceTargetUtility = new SourceTargetUtility(new StringTransform(transform: (source) =>
                    {
                        return UCharacter.ToLower(locale, source);
                    }));
                }
            }
            sourceTargetUtility.AddSourceTargetSet(this, inputFilter, sourceSet, targetSet);
        }
    }
}
