/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// TODO:
//   - Check ParseError errorId strings
//   - Check ParseError Extents

namespace System.Management.Automation.Language
{
    /// <summary>
    /// Specifies the semantic properties/actions that a
    /// DSL keyword must provide to the PowerShell runtime. This must
    /// be subclassed to instantiate a new PowerShell keyword
    /// </summary>
    public abstract class Keyword
    {
        protected Keyword()
        {
            PreParse = null;
            PostParse = null;
            SemanticCheck = null;
        }

        public virtual Func<DynamicKeyword, ParseError[]> PreParse
        {
            get;
        }

        public virtual Func<DynamicKeywordStatementAst, ParseError[]> PostParse
        {
            get;
        }

        public virtual Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
        {
            get;
        }

        /// <summary>
        /// A default, convenience implementation for parameter resolution. Provided
        /// so that keyword implementors do not need to always reimplement most likely
        /// functionality.
        /// </summary>
        /// <param name="kwStmtAst">dynamic keyword statement ast node</param>
        /// <returns></returns>
        protected virtual ParseError[] ResolveParameters(DynamicKeywordStatementAst kwAst)
        {
            IEnumerable<PropertyInfo> allKwParams = from p in this.GetType().GetProperties()
                                                    where p.IsDefined(typeof(KeywordParameterAttribute))
                                                    select p;

            return ResolveParameters(kwAst, allKwParams);
        }

        /// <summary>
        /// A parameter resolving method to set all properties in a given list, assuming they use
        /// default property getter/setter implementations.
        /// </summary>
        /// <param name="kwAst">dynamic keyword statement ast node</param>
        /// <param name="autoSetKeywords">the keywords to be set using this unclever method</param>
        /// <returns>a list of errors encountered while parsing, or null on success</returns>
        protected virtual ParseError[] ResolveParameters(DynamicKeywordStatementAst kwAst, IEnumerable<PropertyInfo> autoSetKeywords)
        {
            var errorList = new List<ParseError>();

            var commandElements = Ast.CopyElements(kwAst.CommandElements);
            var commandAst = new CommandAst(kwAst.Extent, commandElements, TokenKind.Unknown, null);
            StaticBindingResult bindingResult = StaticParameterBinder.BindCommand(commandAst);

            // Ensure we have values for the all parameters we are responsible for setting
            if (IsMissingMandatoryParams(autoSetKeywords, bindingResult.BoundParameters, kwAst.Extent, errorList))
            {
                return errorList.ToArray();
            }

            // Try and set all the parameters that were passed in
            foreach (KeyValuePair<string, ParameterBindingResult> binding in bindingResult.BoundParameters)
            {
                if (!TrySetNamedParam(binding, errorList) && !TrySetPositionalParam(autoSetKeywords, binding, errorList))
                {
                    errorList.Add(new ParseError(binding.Value.Value.Extent, "UnknownParameter", "The parameter at position " + binding.Key + " was not recognized"));
                }
            }

            return errorList.ToArray();
        }

        /// <summary>
        /// Checks that all mandatory parameters have been provided
        /// </summary>
        /// <param name="propertiesToSet">the properties the the caller intends to set</param>
        /// <param name="boundParameters">the parameters bound in PowerShell</param>
        /// <param name="extent">the extent of the keyword statement</param>
        /// <param name="errorList">the list of errors so far</param>
        /// <returns></returns>
        protected bool IsMissingMandatoryParams(IEnumerable<PropertyInfo> propertiesToSet, Dictionary<string, ParameterBindingResult> boundParameters, IScriptExtent extent, List<ParseError> errorList)
        {
            bool hasMissing = false;
            foreach (var propInfo in propertiesToSet)
            {
                var paramOptions = propInfo.GetCustomAttribute<KeywordParameterAttribute>();
                if (paramOptions.Mandatory)
                {
                    if (!boundParameters.ContainsKey(propInfo.Name) && !boundParameters.ContainsKey(paramOptions.Postion.ToString()))
                    {
                        hasMissing = true;
                        errorList.Add(new ParseError(extent, "MissingParameter", "The parameter " + propInfo.Name + " was not provided"));
                    }
                }
            }

            return hasMissing;
        }

        /// <summary>
        /// Attempt to set a bound parameter on the assumption it is bound to a position
        /// </summary>
        /// <param name="possibleParams">the parameters the positional parameter could correspond to</param>
        /// <param name="boundParameter">the positional parameter binding</param>
        /// <param name="errorList">the list of errors so far</param>
        /// <returns>true if the value was set correctly, false otherwise</returns>
        protected bool TrySetPositionalParam(IEnumerable<PropertyInfo> possibleParams, KeyValuePair<string, ParameterBindingResult> boundParameter, List<ParseError> errorList)
        {
            // Reject null parameters
            if (possibleParams == null)
            {
                throw new ArgumentNullException("possibleParams");
            }

            if (errorList == null)
            {
                throw new ArgumentNullException("errorList");
            }

            // Attempt to parse the key as an int
            int position;
            if (!Int32.TryParse(boundParameter.Key, out position))
            {
                return false;
            }

            // Find the property corresponding to the position if one exists, and set it
            PropertyInfo propInfo = possibleParams.FirstOrDefault(p => p.GetCustomAttribute<KeywordParameterAttribute>().Postion == position);
            if (propInfo == null)
            {
                return false;
            }

            return TrySetValue(propInfo, boundParameter.Value, errorList);
        }

        /// <summary>
        /// Try to set a named parameter, assuming that the parameter key must be its name
        /// </summary>
        /// <param name="boundParameter">the bound parameter</param>
        /// <param name="errorList">the list of errors so far</param>
        /// <returns>true if the value was set correctly, false otherwise</returns>
        protected bool TrySetNamedParam(KeyValuePair<string, ParameterBindingResult> boundParameter, List<ParseError> errorList)
        {
            // Reject null parameters
            if (errorList == null)
            {
                throw new ArgumentNullException("errorList");
            }

            PropertyInfo propInfo = this.GetType().GetProperty(boundParameter.Key, BindingFlags.IgnoreCase);
            if (propInfo == null)
            {
                return false;
            }

            return TrySetValue(propInfo, boundParameter.Value, errorList);
        }

        /// <summary>
        /// Tries to set the property specified by property value to the
        /// parameter-bound value in boundValue
        /// </summary>
        /// <param name="propInfo">metadata about the property being set</param>
        /// <param name="boundValue">powershell bound parameter containing the value to set</param>
        /// <param name="errorList">list of errors so far, to add to</param>
        /// <returns>true if the value was set correctly, false otherwise</returns>
        protected bool TrySetValue(PropertyInfo propInfo, ParameterBindingResult boundValue, List<ParseError> errorList)
        {
            // Reject null parameters
            if (propInfo == null)
            {
                throw new ArgumentNullException("propInfo");
            }

            if (boundValue == null)
            {
                throw new ArgumentNullException("boundValue");
            }

            if (errorList == null)
            {
                throw new ArgumentNullException("errorList");
            }

            // Ensure the property is writeable
            if (!propInfo.CanWrite)
            {
                errorList.Add(new ParseError(boundValue.Value.Extent, "UnwritableProperty", "The property " + propInfo.Name + " is not able to be written"));
                return false;
            }

            // Try to resolve a value from the argument's Ast node
            object astValue;
            try
            {
                astValue = boundValue.Value.SafeGetValue();
            }
            catch (InvalidOperationException e)
            {
                errorList.Add(new ParseError(boundValue.Value.Extent, "UnsafeValue", "The value " + boundValue.Value.ToString() + " could not be parsed"));
                return false;
            }

            // Try to coerce the value's type to that of the property
            var value = Convert.ChangeType(astValue, propInfo.PropertyType);
            if (value == null)
            {
                errorList.Add(new ParseError(boundValue.Value.Extent, "BadParameterType", "The parameter " + boundValue.Value.ToString() + " was of incorrect type"));
                return false;
            }

            propInfo.SetValue(this, value);
            return true;
        }
    }

    #region DSL definition attributes

    /// <summary>
    /// Specifies the syntax of the body following a keyword.
    /// Default is Command.
    /// </summary>
    public enum KeywordBodyMode
    {
        Command,     // The keyword expects no body; it behaves like a command
        ScriptBlock, // The keyword expects a scriptblock body
        Hashtable,   // The keyword expects a hashtable body
    }

    /// <summary>
    /// Specifies the number of times a keyword may be used in a block.
    /// Defaults to OptionalMany.
    /// </summary>
    public enum KeywordUseMode
    {
        Optional,     // May be used 0-1 times
        OptionalMany, // May be used any number of times
        Required,     // Must be used exactly once
        RequiredMany, // Must be used at least once
    }

    /// <summary>
    /// Specifies that a class denotes a DSL Keyword
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class KeywordAttribute : System.Attribute
    {
        private KeywordBodyMode bodyMode;
        private KeywordUseMode useMode;

        public KeywordAttribute()
        {
            this.bodyMode = KeywordBodyMode.Command;
            this.useMode = KeywordUseMode.OptionalMany;
        }

        public virtual KeywordBodyMode Body
        {
            get { return bodyMode; }
            set { bodyMode = value; }
        }

        public virtual KeywordUseMode Use
        {
            get { return useMode; }
            set { useMode = value;}
        }
    }

    /// <summary>
    /// Specifies a field denoting a keyword argument
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class KeywordParameterAttribute : System.Attribute
    {
        private bool mandatory;
        private int position;

        public KeywordParameterAttribute()
        {
            this.mandatory = true;
            this.position = -1;
        }

        /// <summary>
        /// Specifies whether an argument must be given. If this is false
        /// and Name is null or empty, this should be an error.
        /// </summary>
        public bool Mandatory
        {
            get { return mandatory; }
            set { mandatory = value; }
        }

        /// <summary>
        /// Specifies what position a parameter occurs at if not passed by name.
        /// A value of -1 means this parameter may be passed by name only.
        /// </summary>
        public int Postion
        {
            get { return position; }
            set { position = value; }
        }
    }

    #endregion /* DSL definition attributes */
}