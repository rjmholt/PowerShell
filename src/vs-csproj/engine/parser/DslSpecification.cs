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
        /// <param name="kwStmtAst"></param>
        /// <returns></returns>
        protected virtual ParseError[] ResolveParameters(DynamicKeywordStatementAst kwAst)
        {
            var errorList = new List<ParseError>();

            Type keywordType = this.GetType();

            var commandElements = Ast.CopyElements(kwAst.CommandElements);
            var commandAst = new CommandAst(kwAst.Extent, commandElements, TokenKind.Unknown, null);
            StaticBindingResult bindingResult = StaticParameterBinder.BindCommand(commandAst);

            // Fetch all keyword parameter properties
            IEnumerable<PropertyInfo> kwParams = from p in keywordType.GetProperties()
                                                 where p.IsDefined(typeof(KeywordParameterAttribute))
                                                 select p;

            // First check we have all the parameters we need
            var mandatoryParamQuery = from p in kwParams
                                      where p.GetCustomAttribute<KeywordParameterAttribute>().Mandatory
                                      select new { parameter = p, position = p.GetCustomAttribute<KeywordParameterAttribute>().Postion };

            bool missingParams = false;
            foreach (var paramData in mandatoryParamQuery)
            {
                // Check if the parameter value is specified by name
                if (!bindingResult.BoundParameters.ContainsKey(paramData.parameter.Name))
                {
                    // Otherwise check it was provided positionally
                    if (!bindingResult.BoundParameters.ContainsKey(paramData.position.ToString()))
                    {
                        missingParams = true;
                        errorList.Add(new ParseError(kwAst.Extent, "MissingParameter", "The parameter " + paramData.parameter.Name + " was not provided"));
                    }
                }
            }

            if (missingParams)
            {
                return errorList.ToArray();
            }

            // Now take all the arguments that were bound and fill the instance with them, checking
            // for surplus parameters as we go
            bool badParams = false;
            foreach (KeyValuePair<string, ParameterBindingResult> binding in bindingResult.BoundParameters)
            {
                PropertyInfo propInfo;

                // Deal with the possibility of positional parameters
                int position;
                if (Int32.TryParse(binding.Key, out position))
                {
                    propInfo = kwParams.FirstOrDefault(p => p.GetCustomAttribute<KeywordParameterAttribute>().Postion == position);
                    if (propInfo == null)
                    {
                        badParams = true;
                        errorList.Add(new ParseError(binding.Value.Value.Extent, "UnknownParameter", "The parameter at position " + binding.Key + " was not recognized"));
                    }
                    else
                    {
                        badParams = !TrySetValue(propInfo, binding.Value, errorList);
                    }
                }
                // Deal with named parameters
                else
                {
                    propInfo = keywordType.GetProperty(binding.Key, BindingFlags.IgnoreCase);
                    if (propInfo == null)
                    {
                        badParams = true;
                        errorList.Add(new ParseError(binding.Value.Value.Extent, "UnknownParameter", "The parameter at position " + binding.Key + " was not recognized"));
                    }
                    else
                    {
                        badParams = !TrySetValue(propInfo, binding.Value, errorList);
                    }
                }
            }

            if (badParams)
            {
                return errorList.ToArray();
            }

            // Signal no errors by returning null
            return null;
        }

        /// <summary>
        /// Tries to set the property specified by property value to the
        /// parameter-bound value in boundValue
        /// </summary>
        /// <param name="propInfo">metadata about the property being set</param>
        /// <param name="boundValue">powershell bound parameter containing the value to set</param>
        /// <param name="errorList">list of errors so far, to add to</param>
        /// <returns></returns>
        protected bool TrySetValue(PropertyInfo propInfo, ParameterBindingResult boundValue, List<ParseError> errorList)
        {
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
    /// Defaults to Optional.
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
            this.useMode = KeywordUseMode.Optional;
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