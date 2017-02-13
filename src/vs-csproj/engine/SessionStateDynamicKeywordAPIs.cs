using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation.Internal;

namespace System.Management.Automation
{
    internal sealed partial class SessionStateInternal
    {
        /// <summary>
        /// Looks up a DynamicKeyword from the Dynamic Keyword table
        /// </summary>
        /// <param name="keywordName">the name of the keyword to find</param>
        /// <returns>the KeywordInfo representing the Dynamic Keyword</returns>
        internal KeywordInfo GetKeyword(string keywordName)
        {
            if (String.IsNullOrEmpty(keywordName))
            {
                return null;
            }

            KeywordInfo result = null;
            var scopeEnumerator = new SessionStateScopeEnumerator(_currentScope);

            foreach(SessionStateScope scope in scopeEnumerator)
            {
                result = scope.GetDynamicKeyword(keywordName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        internal KeywordInfo GetKeywordAtScope(string keywordName, string scopeID)
        {
            if (String.IsNullOrEmpty(keywordName))
            {
                return null;
            }

            SessionStateScope scope = GetScopeByID(scopeID);
            return scope.GetDynamicKeyword(keywordName);
        }

        /// <summary>
        /// Get an IEnumerable for the Dynamic Keyword table
        /// </summary>
        /// <returns></returns>
        internal IDictionary<string, List<KeywordInfo>> GetKeywordTable()
        {
            var result = new Dictionary<string, List<KeywordInfo>>(StringComparer.OrdinalIgnoreCase);

            var scopeEnumerator = new SessionStateScopeEnumerator(_currentScope);

            foreach (SessionStateScope scope in scopeEnumerator)
            {
                foreach (KeyValuePair<string, List<KeywordInfo>> entry in scope.DynamicKeywordTable)
                {
                    if (!result.ContainsKey(entry.Key))
                    {
                        result.Add(entry.Key, entry.Value);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get an IEnumerable for the Dynamic Keyword table in the given scope
        /// </summary>
        /// <param name="scopeID"></param>
        /// <returns></returns>
        internal IDictionary<string, List<KeywordInfo>> GetKeywordTableAtScope(string scopeID)
        {
            var result = new Dictionary<string, List<KeywordInfo>>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<KeywordInfo>> entry in GetScopeByID(scopeID).DynamicKeywordTable)
            {
                result.Add(entry.Key, entry.Value);
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="index"></param>
        /// <param name="force"></param>
        internal void RemoveKeyword(string name, int index, bool force = false)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw PSTraceSource.NewArgumentNullException(nameof(name));
            }

            var scopeEnumerator = new SessionStateScopeEnumerator(_currentScope);

            foreach (SessionStateScope scope in scopeEnumerator)
            {
                KeywordInfo keywordInfo = scope.GetDynamicKeyword(name);

                if (keywordInfo != null)
                {
                    scope.RemoveDynamicKeyword(name, index, force);
                    break;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="force"></param>
        internal void RemoveKeywordEntry(string name, bool force = false)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw PSTraceSource.NewArgumentNullException(nameof(name));
            }

            var scopeEnumerator = new SessionStateScopeEnumerator(_currentScope);
            foreach (SessionStateScope scope in scopeEnumerator)
            {
                KeywordInfo keywordInfo = scope.GetDynamicKeyword(name);

                if (keywordInfo != null)
                {
                    scope.RemoveDynamicKeywordEntry(name, force: force);
                    break;
                }
            }
        }
    }
}
