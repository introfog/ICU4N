﻿using ICU4N.Support.Collections;
using ICU4N.Text;
using ICU4N.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using CollatorFactory = ICU4N.Text.Collator.CollatorFactory; // ICU4N TODO: API - de-nest?

namespace ICU4N.Dev.Test.Collate
{
    public class CollationServiceTest : TestFmwk
    {
        [Test]
        public void TestRegister()
        {
            // register a singleton
            Collator frcol = Collator.GetInstance(ULocale.FRANCE);
            Collator uscol = Collator.GetInstance(ULocale.US);

            { // try override en_US collator
                Object key = Collator.RegisterInstance(frcol, ULocale.US);
                Collator ncol = Collator.GetInstance(ULocale.US);
                if (!frcol.Equals(ncol))
                {
                    Errln("register of french collator for en_US failed");
                }

                // coverage
                Collator test = Collator.GetInstance(ULocale.GERMANY); // CollatorFactory.handleCreate
                if (!test.GetLocale(ULocale.VALID_LOCALE).Equals(ULocale.GERMAN))
                {
                    Errln("Collation from Germany is really " + test.GetLocale(ULocale.VALID_LOCALE));
                }

                if (!Collator.Unregister(key))
                {
                    Errln("failed to unregister french collator");
                }
                ncol = Collator.GetInstance(ULocale.US);
                if (!uscol.Equals(ncol))
                {
                    Errln("collator after unregister does not match original");
                }
            }

            ULocale fu_FU = new ULocale("fu_FU_FOO");

            { // try create collator for new locale
                Collator fucol = Collator.GetInstance(fu_FU);
                Object key = Collator.RegisterInstance(frcol, fu_FU);
                Collator ncol = Collator.GetInstance(fu_FU);
                if (!frcol.Equals(ncol))
                {
                    Errln("register of fr collator for fu_FU failed");
                }

                ULocale[] locales = Collator.GetAvailableULocales();
                bool found = false;
                for (int i = 0; i < locales.Length; ++i)
                {
                    if (locales[i].Equals(fu_FU))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Errln("new locale fu_FU not reported as supported locale");
                }
                try
                {
                    String name = Collator.GetDisplayName(fu_FU);
                    if (!"fu (FU, FOO)".Equals(name)
                            && !"fu_FU_FOO".Equals(name) /* no LocaleDisplayNamesImpl */)
                    {
                        Errln("found " + name + " for fu_FU");
                    }
                }
                catch (MissingManifestResourceException ex)
                {
                    Warnln("Could not load locale data.");
                }
                try
                {
                    String name = Collator.GetDisplayName(fu_FU, fu_FU);
                    if (!"fu (FU, FOO)".Equals(name)
                            && !"fu_FU_FOO".Equals(name) /* no LocaleDisplayNamesImpl */)
                    {
                        Errln("found " + name + " for fu_FU");
                    }
                }
                catch (MissingManifestResourceException ex)
                {
                    Warnln("Could not load locale data.");
                }

                if (!Collator.Unregister(key))
                {
                    Errln("failed to unregister french collator");
                }
                ncol = Collator.GetInstance(fu_FU);
                if (!fucol.Equals(ncol))
                {
                    Errln("collator after unregister does not match original fu_FU");
                }
            }

            {
                // coverage after return to default
                ULocale[] locales = Collator.GetAvailableULocales();

                for (int i = 0; i < locales.Length; ++i)
                {
                    if (locales[i].Equals(fu_FU))
                    {
                        Errln("new locale fu_FU not reported as supported locale");
                        break;
                    }
                }

                Collator ncol = Collator.GetInstance(ULocale.US);
                if (!ncol.GetLocale(ULocale.VALID_LOCALE).Equals(ULocale.US))
                {
                    Errln("Collation from US is really " + ncol.GetLocale(ULocale.VALID_LOCALE));
                }
            }
        }

        private class CollatorInfo
        {
            internal ULocale locale;
            internal Collator collator;
            internal IDictionary<object, object> displayNames; // locale -> string

            internal CollatorInfo(ULocale locale, Collator collator, IDictionary<object, object> displayNames)
            {
                this.locale = locale;
                this.collator = collator;
                this.displayNames = displayNames;
            }

            internal String GetDisplayName(ULocale displayLocale)
            {
                String name = null;
                if (displayNames != null)
                {
                    name = (String)displayNames.Get(displayLocale);
                }
                if (name == null)
                {
                    name = locale.GetDisplayName(displayLocale);
                }
                return name;
            }
        }

        private class TestFactory : CollatorFactory
        {
            private IDictionary<object, object> map;
            private ISet<string> ids;

            internal TestFactory(CollatorInfo[] info)
            {
                map = new Dictionary<object, object>();
                for (int i = 0; i < info.Length; ++i)
                {
                    CollatorInfo ci = info[i];
                    map[ci.locale] = ci;
                }
            }

            public override Collator CreateCollator(ULocale loc)
            {
                CollatorInfo ci = (CollatorInfo)map.Get(loc);
                if (ci != null)
                {
                    return ci.collator;
                }
                return null;
            }

            public override String GetDisplayName(ULocale objectLocale, ULocale displayLocale)
            {
                CollatorInfo ci = (CollatorInfo)map.Get(objectLocale);
                if (ci != null)
                {
                    return ci.GetDisplayName(displayLocale);
                }
                return null;
            }

            public override ICollection<string> GetSupportedLocaleIDs()
            {
                if (ids == null)
                {
                    HashSet<string> set = new HashSet<string>();
                    using (var iter = map.Keys.GetEnumerator())
                    {
                        while (iter.MoveNext())
                        {
                            ULocale locale = (ULocale)iter.Current;
                            String id = locale.ToString();
                            set.Add(id);
                        }
                        ids = (set).ToUnmodifiableSet();
                    }
                }
                return ids;
            }
        }

        private class TestFactoryWrapper : CollatorFactory
        {
            internal CollatorFactory @delegate;

            internal TestFactoryWrapper(CollatorFactory @delegate)
            {
                this.@delegate = @delegate;
            }

            public override Collator CreateCollator(ULocale loc)
            {
                return @delegate.CreateCollator(loc);
            }

            // use CollatorFactory getDisplayName(ULocale, ULocale) for coverage

            public override ICollection<string> GetSupportedLocaleIDs()
            {
                return @delegate.GetSupportedLocaleIDs();
            }
        }

        [Test]
        public void TestRegisterFactory()
        {

            ULocale fu_FU = new ULocale("fu_FU");
            ULocale fu_FU_FOO = new ULocale("fu_FU_FOO");

            IDictionary<object, object> fuFUNames = new Dictionary<object, object>
            {
                { fu_FU, "ze leetle bunny Fu-Fu" },
                { fu_FU_FOO, "zee leetel bunny Foo-Foo" },
                { ULocale.US, "little bunny Foo Foo" }
            };

            Collator frcol = Collator.GetInstance(ULocale.FRANCE);
            /* Collator uscol = */
            Collator.GetInstance(ULocale.US);
            Collator gecol = Collator.GetInstance(ULocale.GERMANY);
            Collator jpcol = Collator.GetInstance(ULocale.JAPAN);
            Collator fucol = Collator.GetInstance(fu_FU);

            CollatorInfo[] info = {
                new CollatorInfo(ULocale.US, frcol, null),
                new CollatorInfo(ULocale.FRANCE, gecol, null),
                new CollatorInfo(fu_FU, jpcol, fuFUNames),
            };
            TestFactory factory = null;
            try
            {
                factory = new TestFactory(info);
            }
            catch (MissingManifestResourceException ex)
            {
                Warnln("Could not load locale data.");
            }
            // coverage
            {
                TestFactoryWrapper wrapper = new TestFactoryWrapper(factory); // in java, gc lets us easily multiply reference!
                Object key = Collator.RegisterFactory(wrapper);
                String name = null;
                try
                {
                    name = Collator.GetDisplayName(fu_FU, fu_FU_FOO);
                }
                catch (MissingManifestResourceException ex)
                {
                    Warnln("Could not load locale data.");
                }
                Logln("*** default name: " + name);
                Collator.Unregister(key);

                ULocale bar_BAR = new ULocale("bar_BAR");
                Collator col = Collator.GetInstance(bar_BAR);
                ULocale valid = col.GetLocale(ULocale.VALID_LOCALE);
                String validName = valid.GetName();
                if (validName.Length != 0 && !validName.Equals("root"))
                {
                    Errln("Collation from bar_BAR is really \"" + validName + "\" but should be root");
                }
            }

            int n1 = checkAvailable("before registerFactory");

            {
                Object key = Collator.RegisterFactory(factory);

                int n2 = checkAvailable("after registerFactory");

                Collator ncol = Collator.GetInstance(ULocale.US);
                if (!frcol.Equals(ncol))
                {
                    Errln("frcoll for en_US failed");
                }

                ncol = Collator.GetInstance(fu_FU_FOO);
                if (!jpcol.Equals(ncol))
                {
                    Errln("jpcol for fu_FU_FOO failed, got: " + ncol);
                }

                ULocale[] locales = Collator.GetAvailableULocales();
                bool found = false;
                for (int i = 0; i < locales.Length; ++i)
                {
                    if (locales[i].Equals(fu_FU))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Errln("new locale fu_FU not reported as supported locale");
                }

                String name = Collator.GetDisplayName(fu_FU);
                if (!"little bunny Foo Foo".Equals(name))
                {
                    Errln("found " + name + " for fu_FU");
                }

                name = Collator.GetDisplayName(fu_FU, fu_FU_FOO);
                if (!"zee leetel bunny Foo-Foo".Equals(name))
                {
                    Errln("found " + name + " for fu_FU in fu_FU_FOO");
                }

                if (!Collator.Unregister(key))
                {
                    Errln("failed to unregister factory");
                }

                int n3 = checkAvailable("after unregister");
                assertTrue("register increases count", n2 > n1);
                assertTrue("unregister restores count", n3 == n1);

                ncol = Collator.GetInstance(fu_FU);
                if (!fucol.Equals(ncol))
                {
                    Errln("collator after unregister does not match original fu_FU");
                }
            }
        }

        /**
         * Check the integrity of the results of Collator.getAvailableULocales().
         * Return the number of items returned.
         */
        internal int checkAvailable(String msg)
        {
            CultureInfo[] locs = Collator.GetAvailableLocales();
            if (!assertTrue("getAvailableLocales != null", locs != null)) return -1;
            CheckArray(msg, locs, null);
            ULocale[] ulocs = Collator.GetAvailableULocales();
            if (!assertTrue("getAvailableULocales != null", ulocs != null)) return -1;
            CheckArray(msg, ulocs, null);
            // This is not true because since ULocale objects with script code cannot be
            // converted to Locale objects
            //assertTrue("getAvailableLocales().Length == getAvailableULocales().Length", locs.Length == ulocs.Length);
            return locs.Length;
        }

        private static readonly String[] KW = {
            "collation"
        };

        private static readonly String[] KWVAL = {
            "phonebook",
            "stroke"
        };

        [Test]
        public void TestSeparateTrees()
        {
            var kw = Collator.Keywords.ToArray();
            if (!assertTrue("getKeywords != null", kw != null)) return;
            CheckArray("getKeywords", kw, KW);

            String[] kwval = Collator.GetKeywordValues(KW[0]);
            if (!assertTrue("getKeywordValues != null", kwval != null)) return;
            CheckArray("getKeywordValues", kwval, KWVAL);

            bool[] isAvailable = new bool[1];
            ULocale equiv = Collator.GetFunctionalEquivalent(KW[0],
                                                             new ULocale("de"),
                                                             isAvailable);
            if (assertTrue("getFunctionalEquivalent(de)!=null", equiv != null))
            {
                assertEquals("getFunctionalEquivalent(de)", "root", equiv.ToString());
            }
            assertTrue("getFunctionalEquivalent(de).isAvailable==true",
                       isAvailable[0] == true);

            equiv = Collator.GetFunctionalEquivalent(KW[0],
                                                     new ULocale("de_DE"),
                                                     isAvailable);
            if (assertTrue("getFunctionalEquivalent(de_DE)!=null", equiv != null))
            {
                assertEquals("getFunctionalEquivalent(de_DE)", "root", equiv.ToString());
            }
            assertTrue("getFunctionalEquivalent(de_DE).isAvailable==false",
                       isAvailable[0] == false);

            equiv = Collator.GetFunctionalEquivalent(KW[0], new ULocale("zh_Hans"));
            if (assertTrue("getFunctionalEquivalent(zh_Hans)!=null", equiv != null))
            {
                assertEquals("getFunctionalEquivalent(zh_Hans)", "zh", equiv.ToString());
            }
        }

        [Test]
        public void TestGetFunctionalEquivalent()
        {
            var kw = Collator.Keywords;
            String[] DATA = {
                          "sv", "sv", "t",
                          "sv@collation=direct", "sv", "t",
                          "sv@collation=traditional", "sv", "t",
                          "sv@collation=gb2312han", "sv", "t",
                          "sv@collation=stroke", "sv", "t",
                          "sv@collation=pinyin", "sv", "t",
                          "sv@collation=standard", "sv@collation=standard", "t",
                          "sv@collation=reformed", "sv", "t",
                          "sv@collation=big5han", "sv", "t",
                          "sv_FI", "sv", "f",
                          "sv_FI@collation=direct", "sv", "f",
                          "sv_FI@collation=traditional", "sv", "f",
                          "sv_FI@collation=gb2312han", "sv", "f",
                          "sv_FI@collation=stroke", "sv", "f",
                          "sv_FI@collation=pinyin", "sv", "f",
                          "sv_FI@collation=standard", "sv@collation=standard", "f",
                          "sv_FI@collation=reformed", "sv", "f",
                          "sv_FI@collation=big5han", "sv", "f",
                          "nl", "root", "t",
                          "nl@collation=direct", "root", "t",
                          "nl_BE", "root", "f",
                          "nl_BE@collation=direct", "root", "f",
                          "nl_BE@collation=traditional", "root", "f",
                          "nl_BE@collation=gb2312han", "root", "f",
                          "nl_BE@collation=stroke", "root", "f",
                          "nl_BE@collation=pinyin", "root", "f",
                          "nl_BE@collation=big5han", "root", "f",
                          "nl_BE@collation=phonebook", "root", "f",
                          "en_US_VALLEYGIRL","root","f"
                        };
            int DATA_COUNT = (DATA.Length / 3);

            for (int i = 0; i < DATA_COUNT; i++)
            {
                bool[] isAvailable = new bool[1];
                ULocale input = new ULocale(DATA[(i * 3) + 0]);
                ULocale expect = new ULocale(DATA[(i * 3) + 1]);
                bool expectAvailable = DATA[(i * 3) + 2].Equals("t");
                ULocale actual = Collator.GetFunctionalEquivalent(kw[0], input, isAvailable);
                if (!actual.Equals(expect) || (expectAvailable != isAvailable[0]))
                {
                    Errln("#" + i + ": Collator.getFunctionalEquivalent(" + input + ")=" + actual + ", avail " + isAvailable[0] + ", " +
                            "expected " + expect + " avail " + expectAvailable);
                }
                else
                {
                    Logln("#" + i + ": Collator.getFunctionalEquivalent(" + input + ")=" + actual + ", avail " + isAvailable[0]);
                }
            }
        }

        //    public void PrintFunctionalEquivalentList() {
        //        ULocale[] locales = Collator.getAvailableULocales();
        //        String[] keywords = Collator.getKeywords();
        //        logln("Collation");
        //        logln("Possible keyword=values pairs:");
        //        for (int i = 0; i < Collator.getKeywords().Length; ++i) {
        //                String[] values = Collator.getKeywordValues(keywords[i]);
        //                for (int j = 0; j < values.Length; ++j) {
        //                        System.out.println(keywords[i] + "=" + values[j]);
        //                }
        //        }
        //        logln("Differing Collators:");
        //        bool[] isAvailable = {true};
        //        for (int k = 0; k < locales.Length; ++k) {
        //                logln(locales[k].getDisplayName(ULocale.ENGLISH) + " [" +locales[k] + "]");
        //                for (int i = 0; i < Collator.getKeywords().Length; ++i) {
        //                        ULocale base = Collator.getFunctionalEquivalent(keywords[i],locales[k]);
        //                        String[] values = Collator.getKeywordValues(keywords[i]);
        //                        for (int j = 0; j < Collator.getKeywordValues(keywords[i]).Length;++j) {
        //                                ULocale other = Collator.getFunctionalEquivalent(keywords[i],
        //                                        new ULocale(locales[k] + "@" + keywords[i] + "=" + values[j]),
        //                                        isAvailable);
        //                                if (isAvailable[0] && !other.Equals(base)) {
        //                                        logln("\t" + keywords[i] + "=" + values[j] + ";\t" + base + ";\t" + other);
        //                                }
        //                        }
        //                }
        //        }
        //    }

        private static bool arrayContains(String[] array, String s)
        {
            for (int i = 0; i < array.Length; ++i)
            {
                if (s.Equals(array[i]))
                {
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void TestGetKeywordValues()
        {
            String[][] PREFERRED = {
                new string[] {"und",             "standard", "eor", "search"},
                new string[] {"en_US",           "standard", "eor", "search"},
                new string[] {"en_029",          "standard", "eor", "search"},
                new string[] {"de_DE",           "standard", "phonebook", "search", "eor"},
                new string[] {"de_Latn_DE",      "standard", "phonebook", "search", "eor"},
                new string[] {"zh",              "pinyin", "stroke", "eor", "search", "standard"},
                new string[] {"zh_Hans",         "pinyin", "stroke", "eor", "search", "standard"},
                new string[] {"zh_CN",           "pinyin", "stroke", "eor", "search", "standard"},
                new string[] {"zh_Hant",         "stroke", "pinyin", "eor", "search", "standard"},
                new string[] {"zh_TW",           "stroke", "pinyin", "eor", "search", "standard"},
                new string[] {"zh__PINYIN",      "pinyin", "stroke", "eor", "search", "standard"},
                new string[] {"es_ES",           "standard", "search", "traditional", "eor"},
                new string[] {"es__TRADITIONAL", "traditional", "search", "standard", "eor"},
                new string[] {"und@collation=phonebook",     "standard", "eor", "search"},
                new string[] {"de_DE@collation=big5han",     "standard", "phonebook", "search", "eor"},
                new string[] {"zzz@collation=xxx",           "standard", "eor", "search"},
            };

            for (int i = 0; i < PREFERRED.Length; i++)
            {
                String locale = PREFERRED[i][0];
                ULocale loc = new ULocale(locale);
                String[] expected = PREFERRED[i];
                String[] pref = Collator.GetKeywordValuesForLocale("collation", loc, true);
                for (int j = 1; j < expected.Length; ++j)
                {
                    if (!arrayContains(pref, expected[j]))
                    {
                        Errln("Keyword value " + expected[j] + " missing for locale: " + locale);
                    }
                }

                // Collator.getKeywordValues return the same contents for both commonlyUsed
                // true and false.
                String[] all = Collator.GetKeywordValuesForLocale("collation", loc, false);
                bool matchAll = false;
                if (pref.Length == all.Length)
                {
                    matchAll = true;
                    for (int j = 0; j < pref.Length; j++)
                    {
                        bool foundMatch = false;
                        for (int k = 0; k < all.Length; k++)
                        {
                            if (pref[j].Equals(all[k]))
                            {
                                foundMatch = true;
                                break;
                            }
                        }
                        if (!foundMatch)
                        {
                            matchAll = false;
                            break;
                        }
                    }
                }
                if (!matchAll)
                {
                    Errln("FAIL: All values for locale " + loc
                            + " got:" + Arrays.ToString(all) + " expected:" + Arrays.ToString(pref));
                }
            }
        }
    }
}