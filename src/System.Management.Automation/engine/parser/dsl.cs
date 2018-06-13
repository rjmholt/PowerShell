using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace System.Management.Automation.Language
{
    /// <summary>
    /// Describes a DslKeyword
    /// </summary>
    public class DslKeyword
    {
        private static DslKeywordTable s_keywordTable = new DslKeywordTable();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool IsDefined(string name)
        {
            return s_keywordTable.ContainsKeyword(name);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="keyword"></param>
        /// <returns></returns>
        public static bool TryGetKeyword(string name, out DslKeyword keyword)
        {
            return s_keywordTable.TryGetKeyword(name, out keyword);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keywords"></param>
        public static void AddKeywords(IEnumerable<DslKeyword> keywords)
        {
            s_keywordTable.AddKeywords(keywords);
        }

        /// <summary>
        /// Create a new DslKeyword instance.
        /// </summary>
        /// <param name="name">The name of this keyword.</param>
        /// <param name="kind">The kind of body this keyword uses.</param>
        public DslKeyword(string name, DslKeywordBodyKind kind)
        {
            Name = name;
            Kind = kind;
        }

        /// <summary>
        /// The name of the keyword -- its bareword string form.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The kind of body this keyword uses.
        /// </summary>
        public DslKeywordBodyKind Kind { get; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class DslKeywordTable
    {
        private ConcurrentDictionary<string, DslKeyword> _dslKeywords;

        /// <summary>
        /// 
        /// </summary>
        public DslKeywordTable()
        {
            _dslKeywords = new ConcurrentDictionary<string, DslKeyword>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool ContainsKeyword(string name)
        {
            return _dslKeywords.ContainsKey(name);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="keyword"></param>
        /// <returns></returns>
        public bool TryGetKeyword(string name, out DslKeyword keyword)
        {
            return _dslKeywords.TryGetValue(name, out keyword);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keyword"></param>
        public void AddKeyword(DslKeyword keyword)
        {
            _dslKeywords[keyword.Name] = keyword;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keywords"></param>
        public void AddKeywords(IEnumerable<DslKeyword> keywords)
        {
            foreach (DslKeyword keyword in keywords)
            {
                _dslKeywords[keyword.Name] = keyword;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public DslKeyword GetKeyword(string name)
        {
            if (!_dslKeywords.ContainsKey(name))
            {
                throw new KeyNotFoundException($"Unable to find keyword '{name}'");
            }

            return _dslKeywords[name];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerable<DslKeyword> GetKeywords()
        {
            return _dslKeywords.Values;
        }
    }

    /// <summary>
    /// The kinds of body a DSL keyword can take
    /// </summary>
    public enum DslKeywordBodyKind
    {
        /// <summary>
        /// Keyword takes no body, only parameters: Keyword -Param1 Something -Switch Param PositionalParam
        /// </summary>
        Command,

        /// <summary>
        /// Keyword uses a scriptblock body: Keyword { foreach ($i in 1..10) { $i }; Write-Output "Hello" }
        /// </summary>
        ScriptBlock,

        /// <summary>
        /// Keyword uses a key/value body: Keyword { key = value; key2 = value2 }
        /// </summary>
        HashTable
    }
}
