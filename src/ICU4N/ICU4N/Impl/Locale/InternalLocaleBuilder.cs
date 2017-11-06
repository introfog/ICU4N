﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ICU4N.Impl.Locale
{
    public sealed class InternalLocaleBuilder
    {
        private static readonly bool JDKIMPL = false;

        private string _language = "";
        private string _script = "";
        private string _region = "";
        private string _variant = "";

        private static readonly CaseInsensitiveChar PRIVUSE_KEY = new CaseInsensitiveChar(LanguageTag.PRIVATEUSE[0]);

        private IDictionary<CaseInsensitiveChar, string> _extensions;
        private ISet<CaseInsensitiveString> _uattributes;
        private IDictionary<CaseInsensitiveString, string> _ukeywords;


        public InternalLocaleBuilder()
        {
        }

        public InternalLocaleBuilder SetLanguage(string language)
        {
            if (language == null || language.Length == 0)
            {
                _language = "";
            }
            else
            {
                if (!LanguageTag.IsLanguage(language))
                {
                    throw new FormatException("Ill-formed language: " + language/*, 0*/);
                }
                _language = language;
            }
            return this;
        }

        public InternalLocaleBuilder SetScript(string script)
        {
            if (script == null || script.Length == 0)
            {
                _script = "";
            }
            else
            {
                if (!LanguageTag.IsScript(script))
                {
                    throw new FormatException("Ill-formed script: " + script/*, 0*/);
                }
                _script = script;
            }
            return this;
        }

        public InternalLocaleBuilder SetRegion(string region)
        {
            if (region == null || region.Length == 0)
            {
                _region = "";
            }
            else
            {
                if (!LanguageTag.IsRegion(region))
                {
                    throw new FormatException("Ill-formed region: " + region/*, 0*/);
                }
                _region = region;
            }
            return this;
        }

        public InternalLocaleBuilder SetVariant(string variant)
        {
            if (variant == null || variant.Length == 0)
            {
                _variant = "";
            }
            else
            {
                // normalize separators to "_"
                string var = variant.Replace(LanguageTag.SEP, BaseLocale.SEP);
                int errIdx = CheckVariants(var, BaseLocale.SEP);
                if (errIdx != -1)
                {
                    throw new FormatException("Ill-formed variant: " + variant /*, errIdx*/);
                }
                _variant = var;
            }
            return this;
        }

        public InternalLocaleBuilder AddUnicodeLocaleAttribute(string attribute)
        {
            if (attribute == null || !UnicodeLocaleExtension.IsAttribute(attribute))
            {
                throw new CultureNotFoundException("Ill-formed Unicode locale attribute: " + attribute);
            }
            // Use case insensitive string to prevent duplication
            if (_uattributes == null)
            {
                _uattributes = new HashSet<CaseInsensitiveString>(/*4*/);
            }
            _uattributes.Add(new CaseInsensitiveString(attribute));
            return this;
        }

        public InternalLocaleBuilder RemoveUnicodeLocaleAttribute(string attribute)
        {
            if (attribute == null || !UnicodeLocaleExtension.IsAttribute(attribute))
            {
                throw new FormatException("Ill-formed Unicode locale attribute: " + attribute);
            }
            if (_uattributes != null)
            {
                _uattributes.Remove(new CaseInsensitiveString(attribute));
            }
            return this;
        }

        public InternalLocaleBuilder SetUnicodeLocaleKeyword(string key, string type)
        {
            if (!UnicodeLocaleExtension.IsKey(key))
            {
                throw new FormatException("Ill-formed Unicode locale keyword key: " + key);
            }

            CaseInsensitiveString cikey = new CaseInsensitiveString(key);
            if (type == null)
            {
                if (_ukeywords != null)
                {
                    // null type is used for remove the key
                    _ukeywords.Remove(cikey);
                }
            }
            else
            {
                if (type.Length != 0)
                {
                    // normalize separator to "-"
                    string tp = type.Replace(BaseLocale.SEP, LanguageTag.SEP);
                    // validate
                    StringTokenIterator itr = new StringTokenIterator(tp, LanguageTag.SEP);
                    while (!itr.IsDone)
                    {
                        string s = itr.Current;
                        if (!UnicodeLocaleExtension.IsTypeSubtag(s))
                        {
                            throw new FormatException("Ill-formed Unicode locale keyword type: " + type /*, itr.CurrentStart*/);
                        }
                        itr.Next();
                    }
                }
                if (_ukeywords == null)
                {
                    _ukeywords = new Dictionary<CaseInsensitiveString, string>(4);
                }
                _ukeywords[cikey] = type;
            }
            return this;
        }

        public InternalLocaleBuilder SetExtension(char singleton, string value)
        {
            // validate key
            bool isBcpPrivateuse = LanguageTag.IsPrivateusePrefixChar(singleton);
            if (!isBcpPrivateuse && !LanguageTag.IsExtensionSingletonChar(singleton))
            {
                throw new FormatException("Ill-formed extension key: " + singleton);
            }

            bool remove = (value == null || value.Length == 0);
            CaseInsensitiveChar key = new CaseInsensitiveChar(singleton);

            if (remove)
            {
                if (UnicodeLocaleExtension.IsSingletonChar(key.Value))
                {
                    // clear entire Unicode locale extension
                    if (_uattributes != null)
                    {
                        _uattributes.Clear();
                    }
                    if (_ukeywords != null)
                    {
                        _ukeywords.Clear();
                    }
                }
                else
                {
                    if (_extensions != null && _extensions.ContainsKey(key))
                    {
                        _extensions.Remove(key);
                    }
                }
            }
            else
            {
                // validate value
                string val = value.Replace(BaseLocale.SEP, LanguageTag.SEP);
                StringTokenIterator itr = new StringTokenIterator(val, LanguageTag.SEP);
                while (!itr.IsDone)
                {
                    string s = itr.Current;
                    bool validSubtag;
                    if (isBcpPrivateuse)
                    {
                        validSubtag = LanguageTag.IsPrivateuseSubtag(s);
                    }
                    else
                    {
                        validSubtag = LanguageTag.IsExtensionSubtag(s);
                    }
                    if (!validSubtag)
                    {
                        throw new FormatException("Ill-formed extension value: " + s /*, itr.CurrentStart*/);
                    }
                    itr.Next();
                }

                if (UnicodeLocaleExtension.IsSingletonChar(key.Value))
                {
                    SetUnicodeLocaleExtension(val);
                }
                else
                {
                    if (_extensions == null)
                    {
                        _extensions = new Dictionary<CaseInsensitiveChar, string>(4);
                    }
                    _extensions[key] = val;
                }
            }
            return this;
        }

        /*
         * Set extension/private subtags in a single string representation
         */
        public InternalLocaleBuilder SetExtensions(string subtags)
        {
            if (subtags == null || subtags.Length == 0)
            {
                ClearExtensions();
                return this;
            }
            subtags = subtags.Replace(BaseLocale.SEP, LanguageTag.SEP);
            StringTokenIterator itr = new StringTokenIterator(subtags, LanguageTag.SEP);

            List<string> extensions = null;
            string privateuse = null;

            int parsed = 0;
            int start;

            // Make a list of extension subtags
            while (!itr.IsDone)
            {
                string s = itr.Current;
                if (LanguageTag.IsExtensionSingleton(s))
                {
                    start = itr.CurrentStart;
                    string singleton = s;
                    StringBuilder sb = new StringBuilder(singleton);

                    itr.Next();
                    while (!itr.IsDone)
                    {
                        s = itr.Current;
                        if (LanguageTag.IsExtensionSubtag(s))
                        {
                            sb.Append(LanguageTag.SEP).Append(s);
                            parsed = itr.CurrentEnd;
                        }
                        else
                        {
                            break;
                        }
                        itr.Next();
                    }

                    if (parsed < start)
                    {
                        throw new FormatException("Incomplete extension '" + singleton + "'"/*, start*/);
                    }

                    if (extensions == null)
                    {
                        extensions = new List<String>(4);
                    }
                    extensions.Add(sb.ToString());
                }
                else
                {
                    break;
                }
            }
            if (!itr.IsDone)
            {
                string s = itr.Current;
                if (LanguageTag.IsPrivateusePrefix(s))
                {
                    start = itr.CurrentStart;
                    StringBuilder sb = new StringBuilder(s);

                    itr.Next();
                    while (!itr.IsDone)
                    {
                        s = itr.Current;
                        if (!LanguageTag.IsPrivateuseSubtag(s))
                        {
                            break;
                        }
                        sb.Append(LanguageTag.SEP).Append(s);
                        parsed = itr.CurrentEnd;

                        itr.Next();
                    }
                    if (parsed <= start)
                    {
                        throw new FormatException("Incomplete privateuse:" + subtags.Substring(start) /*, start*/);
                    }
                    else
                    {
                        privateuse = sb.ToString();
                    }
                }
            }

            if (!itr.IsDone)
            {
                throw new FormatException("Ill-formed extension subtags:" + subtags.Substring(itr.CurrentStart)/*, itr.CurrentStart*/);
            }

            return SetExtensions(extensions, privateuse);
        }

        /*
         * Set a list of BCP47 extensions and private use subtags
         * BCP47 extensions are already validated and well-formed, but may contain duplicates
         */
        private InternalLocaleBuilder SetExtensions(IList<String> bcpExtensions, string privateuse)
        {
            ClearExtensions();

            if (bcpExtensions != null && bcpExtensions.Count > 0)
            {
                HashSet<CaseInsensitiveChar> processedExtensions = new HashSet<CaseInsensitiveChar>(/*bcpExtensions.Count*/);
                foreach (string bcpExt in bcpExtensions)
                {
                    CaseInsensitiveChar key = new CaseInsensitiveChar(bcpExt[0]);
                    // ignore duplicates
                    if (!processedExtensions.Contains(key))
                    {
                        // each extension string contains singleton, e.g. "a-abc-def"
                        if (UnicodeLocaleExtension.IsSingletonChar(key.Value))
                        {
                            SetUnicodeLocaleExtension(bcpExt.Substring(2));
                        }
                        else
                        {
                            if (_extensions == null)
                            {
                                _extensions = new Dictionary<CaseInsensitiveChar, String>(4);
                            }
                            _extensions[key] = bcpExt.Substring(2);
                        }
                    }
                }
            }
            if (privateuse != null && privateuse.Length > 0)
            {
                // privateuse string contains prefix, e.g. "x-abc-def"
                if (_extensions == null)
                {
                    _extensions = new Dictionary<CaseInsensitiveChar, string>(1);
                }
                _extensions[new CaseInsensitiveChar(privateuse[0])] = privateuse.Substring(2);
            }

            return this;
        }

        /*
         * Reset Builder's internal state with the given language tag
         */
        public InternalLocaleBuilder SetLanguageTag(LanguageTag langtag)
        {
            Clear();
            if (langtag.GetExtlangs().Count > 0)
            {
                _language = langtag.GetExtlangs()[0];
            }
            else
            {
                string language = langtag.GetLanguage();
                if (!language.Equals(LanguageTag.UNDETERMINED))
                {
                    _language = language;
                }
            }
            _script = langtag.GetScript();
            _region = langtag.GetRegion();

            IList<String> bcpVariants = langtag.GetVariants();
            if (bcpVariants.Count > 0)
            {
                StringBuilder var = new StringBuilder(bcpVariants[0]);
                for (int i = 1; i < bcpVariants.Count; i++)
                {
                    var.Append(BaseLocale.SEP).Append(bcpVariants[i]);
                }
                _variant = var.ToString();
            }

            SetExtensions(langtag.GetExtensions(), langtag.GetPrivateuse());

            return this;
        }

        public InternalLocaleBuilder SetLocale(BaseLocale @base, LocaleExtensions extensions)
        {
            string language = @base.GetLanguage();
            string script = @base.GetScript();
            string region = @base.GetRegion();
            string variant = @base.GetVariant();

            if (JDKIMPL)
            {
                // Special backward compatibility support

                // Exception 1 - ja_JP_JP
                if (language.Equals("ja") && region.Equals("JP") && variant.Equals("JP"))
                {
                    // When locale ja_JP_JP is created, ca-japanese is always there.
                    // The builder ignores the variant "JP"
                    Debug.Assert("japanese".Equals(extensions.GetUnicodeLocaleType("ca")));
                    variant = "";
                }
                // Exception 2 - th_TH_TH
                else if (language.Equals("th") && region.Equals("TH") && variant.Equals("TH"))
                {
                    // When locale th_TH_TH is created, nu-thai is always there.
                    // The builder ignores the variant "TH"
                    Debug.Assert("thai".Equals(extensions.GetUnicodeLocaleType("nu")));
                    variant = "";
                }
                // Exception 3 - no_NO_NY
                else if (language.Equals("no") && region.Equals("NO") && variant.Equals("NY")) // ICU4N TODO: Fix this handling for .NET (no-NO is not reliable across platforms)
                {
                    // no_NO_NY is a valid locale and used by Java 6 or older versions.
                    // The build ignores the variant "NY" and change the language to "nn".
                    language = "nn";
                    variant = "";
                }
            }

            // Validate base locale fields before updating internal state.
            // LocaleExtensions always store validated/canonicalized values,
            // so no checks are necessary.
            if (language.Length > 0 && !LanguageTag.IsLanguage(language))
            {
                throw new FormatException("Ill-formed language: " + language);
            }

            if (script.Length > 0 && !LanguageTag.IsScript(script))
            {
                throw new FormatException("Ill-formed script: " + script);
            }

            if (region.Length > 0 && !LanguageTag.IsRegion(region))
            {
                throw new FormatException("Ill-formed region: " + region);
            }

            if (variant.Length > 0)
            {
                int errIdx = CheckVariants(variant, BaseLocale.SEP);
                if (errIdx != -1)
                {
                    throw new FormatException("Ill-formed variant: " + variant/*, errIdx*/);
                }
            }

            // The input locale is validated at this point.
            // Now, updating builder's internal fields.
            _language = language;
            _script = script;
            _region = region;
            _variant = variant;
            ClearExtensions();

            var extKeys = (extensions == null) ? null : extensions.Keys;
            if (extKeys != null)
            {
                // map extensions back to builder's internal format
                foreach (char key in extKeys)
                {
                    Extension e = extensions.GetExtension(key);
                    if (e is UnicodeLocaleExtension)
                    {
                        UnicodeLocaleExtension ue = (UnicodeLocaleExtension)e;
                        foreach (string uatr in ue.GetUnicodeLocaleAttributes())
                        {
                            if (_uattributes == null)
                            {
                                _uattributes = new HashSet<CaseInsensitiveString>(/*4*/);
                            }
                            _uattributes.Add(new CaseInsensitiveString(uatr));
                        }
                        foreach (string ukey in ue.GetUnicodeLocaleKeys())
                        {
                            if (_ukeywords == null)
                            {
                                _ukeywords = new Dictionary<CaseInsensitiveString, string>(4);
                            }
                            _ukeywords[new CaseInsensitiveString(ukey)] = ue.GetUnicodeLocaleType(ukey);
                        }
                    }
                    else
                    {
                        if (_extensions == null)
                        {
                            _extensions = new Dictionary<CaseInsensitiveChar, string>(4);
                        }
                        _extensions[new CaseInsensitiveChar(key)] = e.Value;
                    }
                }
            }
            return this;
        }

        public InternalLocaleBuilder Clear()
        {
            _language = "";
            _script = "";
            _region = "";
            _variant = "";
            ClearExtensions();
            return this;
        }

        public InternalLocaleBuilder ClearExtensions()
        {
            if (_extensions != null)
            {
                _extensions.Clear();
            }
            if (_uattributes != null)
            {
                _uattributes.Clear();
            }
            if (_ukeywords != null)
            {
                _ukeywords.Clear();
            }
            return this;
        }

        public BaseLocale GetBaseLocale()
        {
            string language = _language;
            string script = _script;
            string region = _region;
            string variant = _variant;

            // Special private use subtag sequence identified by "lvariant" will be
            // interpreted as Java variant.
            if (_extensions != null)
            {
                string privuse;
                if (_extensions.TryGetValue(PRIVUSE_KEY, out privuse) && privuse != null)
                {
                    StringTokenIterator itr = new StringTokenIterator(privuse, LanguageTag.SEP);
                    bool sawPrefix = false;
                    int privVarStart = -1;
                    while (!itr.IsDone)
                    {
                        if (sawPrefix)
                        {
                            privVarStart = itr.CurrentStart;
                            break;
                        }
                        if (AsciiUtil.CaseIgnoreMatch(itr.Current, LanguageTag.PRIVUSE_VARIANT_PREFIX))
                        {
                            sawPrefix = true;
                        }
                        itr.Next();
                    }
                    if (privVarStart != -1)
                    {
                        StringBuilder sb = new StringBuilder(variant);
                        if (sb.Length != 0)
                        {
                            sb.Append(BaseLocale.SEP);
                        }
                        sb.Append(privuse.Substring(privVarStart).Replace(LanguageTag.SEP, BaseLocale.SEP));
                        variant = sb.ToString();
                    }
                }
            }

            return BaseLocale.GetInstance(language, script, region, variant);
        }

        public LocaleExtensions GetLocaleExtensions()
        {
            if ((_extensions == null || _extensions.Count == 0)
                    && (_uattributes == null || _uattributes.Count == 0)
                    && (_ukeywords == null || _ukeywords.Count == 0))
            {
                return LocaleExtensions.EMPTY_EXTENSIONS;
            }

            return new LocaleExtensions(_extensions, _uattributes, _ukeywords);
        }

        /*
         * Remove special private use subtag sequence identified by "lvariant"
         * and return the rest. Only used by LocaleExtensions
         */
        internal static string RemovePrivateuseVariant(string privuseVal)
        {
            StringTokenIterator itr = new StringTokenIterator(privuseVal, LanguageTag.SEP);

            // Note: privateuse value "abc-lvariant" is unchanged
            // because no subtags after "lvariant".

            int prefixStart = -1;
            bool sawPrivuseVar = false;
            while (!itr.IsDone)
            {
                if (prefixStart != -1)
                {
                    // Note: privateuse value "abc-lvariant" is unchanged
                    // because no subtags after "lvariant".
                    sawPrivuseVar = true;
                    break;
                }
                if (AsciiUtil.CaseIgnoreMatch(itr.Current, LanguageTag.PRIVUSE_VARIANT_PREFIX))
                {
                    prefixStart = itr.CurrentStart;
                }
                itr.Next();
            }
            if (!sawPrivuseVar)
            {
                return privuseVal;
            }

            Debug.Assert(prefixStart == 0 || prefixStart > 1);
            return (prefixStart == 0) ? null : privuseVal.Substring(0, prefixStart - 1);
        }

        /*
         * Check if the given variant subtags separated by the given
         * separator(s) are valid
         */
        private int CheckVariants(String variants, String sep)
        {
            StringTokenIterator itr = new StringTokenIterator(variants, sep);
            while (!itr.IsDone)
            {
                string s = itr.Current;
                if (!LanguageTag.IsVariant(s))
                {
                    return itr.CurrentStart;
                }
                itr.Next();
            }
            return -1;
        }

        /*
         * Private methods parsing Unicode Locale Extension subtags.
         * Duplicated attributes/keywords will be ignored.
         * The input must be a valid extension subtags (excluding singleton).
         */
        private void SetUnicodeLocaleExtension(string subtags)
        {
            // wipe out existing attributes/keywords
            if (_uattributes != null)
            {
                _uattributes.Clear();
            }
            if (_ukeywords != null)
            {
                _ukeywords.Clear();
            }

            StringTokenIterator itr = new StringTokenIterator(subtags, LanguageTag.SEP);

            // parse attributes
            while (!itr.IsDone)
            {
                if (!UnicodeLocaleExtension.IsAttribute(itr.Current))
                {
                    break;
                }
                if (_uattributes == null)
                {
                    _uattributes = new HashSet<CaseInsensitiveString>(/*4*/);
                }
                _uattributes.Add(new CaseInsensitiveString(itr.Current));
                itr.Next();
            }

            // parse keywords
            CaseInsensitiveString key = null;
            String type;
            int typeStart = -1;
            int typeEnd = -1;
            while (!itr.IsDone)
            {
                if (key != null)
                {
                    if (UnicodeLocaleExtension.IsKey(itr.Current))
                    {
                        // next keyword - emit previous one
                        Debug.Assert(typeStart == -1 || typeEnd != -1);
                        type = (typeStart == -1) ? "" : subtags.Substring(typeStart, typeEnd - typeStart);
                        if (_ukeywords == null)
                        {
                            _ukeywords = new Dictionary<CaseInsensitiveString, String>(4);
                        }
                        _ukeywords[key] = type;

                        // reset keyword info
                        CaseInsensitiveString tmpKey = new CaseInsensitiveString(itr.Current);
                        key = _ukeywords.ContainsKey(tmpKey) ? null : tmpKey;
                        typeStart = typeEnd = -1;
                    }
                    else
                    {
                        if (typeStart == -1)
                        {
                            typeStart = itr.CurrentStart;
                        }
                        typeEnd = itr.CurrentEnd;
                    }
                }
                else if (UnicodeLocaleExtension.IsKey(itr.Current))
                {
                    // 1. first keyword or
                    // 2. next keyword, but previous one was duplicate
                    key = new CaseInsensitiveString(itr.Current);
                    if (_ukeywords != null && _ukeywords.ContainsKey(key))
                    {
                        // duplicate
                        key = null;
                    }
                }

                if (!itr.HasNext())
                {
                    if (key != null)
                    {
                        // last keyword
                        Debug.Assert(typeStart == -1 || typeEnd != -1);
                        type = (typeStart == -1) ? "" : subtags.Substring(typeStart, typeEnd - typeStart);
                        if (_ukeywords == null)
                        {
                            _ukeywords = new Dictionary<CaseInsensitiveString, String>(4);
                        }
                        _ukeywords[key] = type;
                    }
                    break;
                }

                itr.Next();
            }
        }

        internal class CaseInsensitiveString
        {
            private string _s;

            internal CaseInsensitiveString(string s)
            {
                _s = s;
            }

            public string Value
            {
                get { return _s; }
            }

            public override int GetHashCode()
            {
                return AsciiUtil.ToLowerString(_s).GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }
                if (!(obj is CaseInsensitiveString))
                {
                    return false;
                }
                return AsciiUtil.CaseIgnoreMatch(_s, ((CaseInsensitiveString)obj).Value);
            }
        }

        internal class CaseInsensitiveChar
        {
            private char _c;

            internal CaseInsensitiveChar(char c)
            {
                _c = c;
            }

            public char Value
            {
                get { return _c; }
            }

            public override int GetHashCode()
            {
                return AsciiUtil.ToLower(_c);
            }

            public override bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }
                if (!(obj is CaseInsensitiveChar))
                {
                    return false;
                }
                return _c == AsciiUtil.ToLower(((CaseInsensitiveChar)obj).Value);
            }

        }
    }
}
