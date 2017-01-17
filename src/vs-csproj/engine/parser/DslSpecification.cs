/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

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
                    if (!boundParameters.ContainsKey(propInfo.Name) && !boundParameters.ContainsKey(paramOptions.Position.ToString()))
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
            PropertyInfo propInfo = possibleParams.FirstOrDefault(p => p.GetCustomAttribute<KeywordParameterAttribute>().Position == position);
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
    /// Specifies that a class denotes a DSL Keyword
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class KeywordAttribute : System.Attribute
    {
        private DynamicKeywordBodyMode bodyMode;
        private DynamicKeywordUseMode useMode;

        public KeywordAttribute()
        {
            this.bodyMode = DynamicKeywordBodyMode.Command;
            this.useMode = DynamicKeywordUseMode.OptionalMany;
        }

        public virtual DynamicKeywordBodyMode Body
        {
            get { return bodyMode; }
            set { bodyMode = value; }
        }

        public virtual DynamicKeywordUseMode Use
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
        public int Position
        {
            get { return position; }
            set { position = value; }
        }
    }

    #endregion /* DSL definition attributes */

    /// <summary>
    /// A class that wraps a PSModuleInfo object and reads from the module it points
    /// to in order to read in a PowerShell DSL definition. This should fail politely
    /// if the module given to it is not a DLL or the DLL does not define any PowerShell
    /// DynamicKeywords
    /// </summary>
    internal class DslDllModuleMetadataReader
    {
        private readonly PSModuleInfo _moduleInfo;

        /// <summary>
        /// Construct a DLL reader from a PSModuleInfo object -- assuming it contains all needed info
        /// </summary>
        /// <param name="moduleInfo">the PSModuleInfo object describing the DLL module to be parsed</param>
        internal DslDllModuleMetadataReader(PSModuleInfo moduleInfo)
        {
            _moduleInfo = moduleInfo;
        }

        /// <summary>
        /// Reads a DSL specification from the PSModuleInfo object this holds and
        /// spits out an array of the top level keywords defined in it
        /// </summary>
        /// <returns>an array of the top level DynamicKeywords defined in the module</returns>
        internal DynamicKeyword[] ReadDslSpecification()
        {
            // TODO: Ensure the module is a DLL, else return null

            using (var stream = File.OpenRead(_moduleInfo.Path))
            using (var peReader = new PEReader(stream))
            {
                if (!peReader.HasMetadata)
                {
                    // TODO: Dll has no metadata, so is not a DSL definition. Return null
                    return null;
                }

                MetadataReader metadataReader = peReader.GetMetadataReader();

                return ReadGlobalDynamicKeywords(metadataReader);
            }
        }

        /// <summary>
        /// Reads the top level dynamic keywords in a DLL using the metadata
        /// reader for that DLL. This constructs all nested keywords in the same DLL
        /// by recursive descent.
        /// </summary>
        /// <param name="metadataReader">the metadata reader for the DLL module being parsed</param>
        /// <returns>an array of the top level keywords defined in the DLL</returns>
        private DynamicKeyword[] ReadGlobalDynamicKeywords(MetadataReader metadataReader)
        {
            var globalKeywordList = new List<DynamicKeyword>();

            foreach (var typeDefHandle in metadataReader.TypeDefinitions)
            {
                var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                var declaringType = typeDef.GetDeclaringType();
                if (declaringType.IsNil)
                {
                    globalKeywordList.Add(ReadKeywordSpecification(metadataReader, typeDef));
                }
            }

            return globalKeywordList.ToArray();
        }

        /// <summary>
        /// Reads a single keyword specification from the DLL, reading all attributes, parameters and
        /// any nested keywords below this one. Constructs all keywords nested below the current one by
        /// recursive descent.
        /// </summary>
        /// <param name="metadataReader">the metadata reader for the DLL module being parsed</param>
        /// <param name="typeDef">the type definition object for the keyword class to be parsed</param>
        /// <returns>the constructed DynamicKeyword from the parsed specification</returns>
        private DynamicKeyword ReadKeywordSpecification(MetadataReader metadataReader, TypeDefinition typeDef)
        {
            // Read in all parameters defined as class properties
            var keywordParameters = new List<DynamicKeywordParameter>();
            foreach (var propertyHandle in typeDef.GetProperties())
            {
                var property = metadataReader.GetPropertyDefinition(propertyHandle);
                foreach (var attributeHandle in property.GetCustomAttributes())
                {
                    var keywordParameterAttribute = metadataReader.GetCustomAttribute(attributeHandle);
                    if (IsKeywordParameterAttribute(metadataReader, keywordParameterAttribute))
                    {
                        keywordParameters.Add(ReadParameterSpecification(metadataReader, property, keywordParameterAttribute));
                        break;
                    }
                }
            }

            // Read in all nested keywords below this one
            List<DynamicKeyword> innerKeywords = new List<DynamicKeyword>();
            foreach (var innerTypeHandle in typeDef.GetNestedTypes())
            {
                var innerTypeDef = metadataReader.GetTypeDefinition(innerTypeHandle);
                innerKeywords.Add(ReadKeywordSpecification(metadataReader, innerTypeDef));
            }

            // Read the custom keyword properties
            DynamicKeywordBodyMode bodyMode;
            DynamicKeywordUseMode useMode;
            foreach (var keywordAttributeHandle in typeDef.GetCustomAttributes())
            {
                var keywordAttribute = metadataReader.GetCustomAttribute(keywordAttributeHandle);
                if (IsKeywordAttribute(metadataReader, keywordAttribute))
                {
                    SetKeywordAttributeParameters(metadataReader, keywordAttribute, out bodyMode, out useMode);
                    break;
                }
            }

            // Set all the properties for the keyword itself
            string keywordName = metadataReader.GetString(typeDef.Name);
            var keyword =  new DynamicKeyword()
            {
                ImplementingModule = _moduleInfo.Name,
                Keyword = keywordName,
            };
            foreach (var keywordParameter in keywordParameters)
            {
                keyword.Parameters.Add(keywordParameter.Name, keywordParameter);
            }

            return keyword;
        }

        private bool IsKeywordParameterAttribute(MetadataReader metadataReader, CustomAttribute keywordParameterAttribute)
        {
            switch (keywordParameterAttribute.Constructor.Kind)
            {
                case HandleKind.MethodDefinition:
                    // TODO: Work out how this should operate, preferably using methodDef.DecodeSignature()
                    var methodDef = metadataReader.GetMethodDefinition((MethodDefinitionHandle)keywordParameterAttribute.Constructor);
                    break;
                case HandleKind.MemberReference:
                    return false;
            }
            return false;
        }

        private DynamicKeywordParameter ReadParameterSpecification(MetadataReader metadataReader, PropertyDefinition property, CustomAttribute keywordParameterAttribute)
        {
            return null;
        }

        private bool IsKeywordAttribute(MetadataReader metadataReader, CustomAttribute keywordAttribute)
        {
            return false;
        }

        private void SetKeywordAttributeParameters(MetadataReader metadataReader, CustomAttribute keywordAttribute, out DynamicKeywordBodyMode bodyMode, out DynamicKeywordUseMode useMode)
        {
        }

    }
}