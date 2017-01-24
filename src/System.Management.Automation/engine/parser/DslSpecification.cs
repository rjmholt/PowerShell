/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using Microsoft.PowerShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Collections.Immutable;
using System.Text;

namespace System.Management.Automation.Language
{
    // TODO:
    //   - Check ParseError errorId strings
    //   - Check ParseError Extents

    /// <summary>
    /// Specifies the semantic properties/actions that a
    /// DSL keyword must provide to the PowerShell runtime. This must
    /// be subclassed to instantiate a new PowerShell keyword
    /// </summary>
    public abstract class Keyword
    {
        /// <summary>
        /// Create a fresh empty instance of a keyword
        /// </summary>
        protected Keyword()
        {
            PreParse = null;
            PostParse = null;
            SemanticCheck = null;
        }

        /// <summary>
        /// Specifies the action to execute before the parser hits
        /// the body of a keyword
        /// </summary>
        public Func<DynamicKeyword, ParseError[]> PreParse
        {
            get; set;
        }

        /// <summary>
        /// Specifies the action to execute after the parser
        /// has processed the body of a keyword
        /// </summary>
        public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
        {
            get; set;
        }

        /// <summary>
        /// Specifies the specific semantic checking to validate a keyword
        /// invocation after parsing
        /// </summary>
        public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
        {
            get; set;
        }

        /// <summary>
        /// A default, convenience implementation for parameter resolution. Provided
        /// so that keyword implementors do not need to always reimplement most likely
        /// functionality.
        /// </summary>
        /// <param name="kwAst">dynamic keyword statement ast node</param>
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
                errorList.Add(new ParseError(boundValue.Value.Extent, e.GetType().ToString(), e.Message));
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

        /// <summary>
        /// Construct a KeywordAttribute with default options set
        /// </summary>
        public KeywordAttribute()
        {
            this.bodyMode = DynamicKeywordBodyMode.Command;
            this.useMode = DynamicKeywordUseMode.OptionalMany;
        }

        /// <summary>
        /// Specifies the body syntax expected after a keyword
        /// </summary>
        public DynamicKeywordBodyMode Body
        {
            get { return bodyMode; }
            set { bodyMode = value; }
        }

        /// <summary>
        /// Specifies the number of times a keyword may be used
        /// in a scope/block
        /// </summary>
        public DynamicKeywordUseMode Use
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

        /// <summary>
        /// Constructs a KeywordParamterAttribute with default options set
        /// </summary>
        public KeywordParameterAttribute()
        {
            this.mandatory = false;
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

    /// <summary>
    /// Denotes a property for a keyword specification. Currently this
    /// would be a key in a hashmap body
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class KeywordPropertyAttribute : System.Attribute
    {
        private bool mandatory;

        /// <summary>
        /// Constructs a KeywordPropertyAttribute with the default options set
        /// </summary>
        public KeywordPropertyAttribute()
        {
            mandatory = false;
        }
        
        /// <summary>
        /// Specifies whether a property must be given for the keyword
        /// </summary>
        public bool Mandatory
        {
            get { return mandatory; }
            set { mandatory = value; }
        }
    }

    #endregion /* DSL definition attributes */

    #region DSL Metadata Reading

    /// <summary>
    /// A class that wraps a PSModuleInfo object and reads from the module it points
    /// to in order to read in a PowerShell DSL definition. This should fail politely
    /// if the module given to it is not a DLL or the DLL does not define any PowerShell
    /// DynamicKeywords
    /// </summary>
    internal class DslDllModuleMetadataReader
    {
        private readonly PSModuleInfo _moduleInfo;

        private MetadataReader _metadataReader;

        /// <summary>
        /// Construct a DLL reader from a PSModuleInfo object -- assuming it contains all needed info
        /// </summary>
        /// <param name="moduleInfo">the PSModuleInfo object describing the DLL module to be parsed</param>
        public DslDllModuleMetadataReader(PSModuleInfo moduleInfo)
        {
            _moduleInfo = moduleInfo;
        }

        /// <summary>
        /// Reads a DSL specification from the PSModuleInfo object this holds and
        /// spits out an array of the top level keywords defined in it
        /// </summary>
        /// <returns>an array of the top level DynamicKeywords defined in the module</returns>
        public IEnumerable<DynamicKeyword> ReadDslSpecification()
        {
            // TODO: Ensure the module is a DLL, else return null

            using (var stream = File.OpenRead(_moduleInfo.Path))
            using (var peReader = new PEReader(stream))
            {
                if (!peReader.HasMetadata)
                {
                    return null;
                }

                _metadataReader = peReader.GetMetadataReader();

                try
                {
                    return ReadGlobalDynamicKeywords();
                }
                catch (Exception e)
                {
                    throw new RuntimeException(e.GetType().ToString() + "|" + e.Message + "|" + e.StackTrace, e);
                }
            }
        }

        private TypingTypeProvider DslReaderTypeProvider
        {
            get
            {
                return s_dslReaderTypeProvider ??
                    (s_dslReaderTypeProvider = new TypingTypeProvider());
            }
        }
        private static TypingTypeProvider s_dslReaderTypeProvider;

        /// <summary>
        /// Reads the top level dynamic keywords in a DLL using the metadata
        /// reader for that DLL. This constructs all nested keywords in the same DLL
        /// by recursive descent.
        /// </summary>
        /// <returns>an array of the top level keywords defined in the DLL</returns>
        private IEnumerable<DynamicKeyword> ReadGlobalDynamicKeywords()
        {
            var globalKeywordList = new List<DynamicKeyword>();

            foreach (var typeDefHandle in _metadataReader.TypeDefinitions)
            {
                var typeDef = _metadataReader.GetTypeDefinition(typeDefHandle);
                var declaringType = typeDef.GetDeclaringType();
                if (declaringType.IsNil)
                {
                    globalKeywordList.Add(ReadKeywordSpecification(typeDef));
                }
            }

            return globalKeywordList;
        }

        /// <summary>
        /// Reads a single keyword specification from the DLL, reading all attributes, parameters and
        /// any nested keywords below this one. Constructs all keywords nested below the current one by
        /// recursive descent.
        /// </summary>
        /// <param name="typeDef">the type definition object for the keyword class to be parsed</param>
        /// <returns>the constructed DynamicKeyword from the parsed specification</returns>
        private DynamicKeyword ReadKeywordSpecification(TypeDefinition typeDef)
        {
            var genericTypeParameters = typeDef.GetGenericParameters()
                .Select(h => Type.GetType(_metadataReader.GetString(_metadataReader.GetGenericParameter(h).Name))).ToImmutableArray();

            // Read in all parameters defined as class properties
            var keywordParameters = new List<DynamicKeywordParameter>();
            foreach (var propertyHandle in typeDef.GetProperties())
            {
                var property = _metadataReader.GetPropertyDefinition(propertyHandle);
                foreach (var attributeHandle in property.GetCustomAttributes())
                {
                    var keywordParameterAttribute = _metadataReader.GetCustomAttribute(attributeHandle);
                    if (IsKeywordParameterAttribute(keywordParameterAttribute))
                    {
                        keywordParameters.Add(ReadParameterSpecification(property, keywordParameterAttribute));
                        break;
                    }
                }
            }

            // Read in all nested keywords below this one
            List<DynamicKeyword> innerKeywords = new List<DynamicKeyword>();
            foreach (var innerTypeHandle in typeDef.GetNestedTypes())
            {
                var innerTypeDef = _metadataReader.GetTypeDefinition(innerTypeHandle);
                innerKeywords.Add(ReadKeywordSpecification(innerTypeDef));
            }

            // Read the custom keyword properties
            DynamicKeywordBodyMode bodyMode = DynamicKeywordBodyMode.Command;
            DynamicKeywordUseMode useMode = DynamicKeywordUseMode.OptionalMany;
            foreach (var keywordAttributeHandle in typeDef.GetCustomAttributes())
            {
                var keywordAttribute = _metadataReader.GetCustomAttribute(keywordAttributeHandle);
                if (IsKeywordAttribute(keywordAttribute))
                {
                    SetKeywordAttributeParameters(keywordAttribute, out bodyMode, out useMode);
                    break;
                }
            }

            // Set all the properties for the keyword itself
            string keywordName = _metadataReader.GetString(typeDef.Name);
            var keyword = new DynamicKeyword()
            {
                ImplementingModule = _moduleInfo.Name,
                Keyword = keywordName,
                BodyMode = bodyMode,
                UseMode = useMode,
            };
            foreach (var keywordParameter in keywordParameters)
            {
                keyword.Parameters.Add(keywordParameter.Name, keywordParameter);
            }

            return keyword;
        }

        private bool IsKeywordParameterAttribute(CustomAttribute keywordParameterAttribute)
        {
            return IsAttributeOfType(keywordParameterAttribute, typeof(KeywordParameterAttribute));
        }

        /// <summary>
        /// Read in the specification for a parameter for a DynamicKeyword. This involves recording the name and type of the
        /// corresponding property, as well as reading in position/mandatory properties from the KeywordParameter attribute.
        /// </summary>
        /// <param name="property">the property representing the DynamicKeyword parameter</param>
        /// <param name="keywordParameterAttribute">the attribute on the property declaring the parameter's properties (position, mandatory)</param>
        /// <returns></returns>
        private DynamicKeywordParameter ReadParameterSpecification(PropertyDefinition property, CustomAttribute keywordParameterAttribute)
        {
            string parameterName = _metadataReader.GetString(property.Name);
            string parameterType = property.DecodeSignature(DslReaderTypeProvider, null).ReturnType.ToString();

            CustomAttributeValue<Type> paramAttrValue = keywordParameterAttribute.DecodeValue(DslReaderTypeProvider);
            int position = -1;
            bool mandatory = false;
            foreach (var paramProperty in paramAttrValue.NamedArguments)
            {
                switch (paramProperty.Name)
                {
                    case "Position":
                        position = (int)paramProperty.Value;
                        break;

                    case "Mandatory":
                        mandatory = (bool)paramProperty.Value;
                        break;
                }
            }

            return new DynamicKeywordParameter()
            {
                Name = parameterName,
                TypeConstraint = parameterType,
                Position = position,
                Mandatory = mandatory
            };
        }

        private bool IsKeywordAttribute(CustomAttribute keywordAttribute)
        {
            return IsAttributeOfType(keywordAttribute, typeof(KeywordAttribute));
        }

        private bool IsAttributeOfType(CustomAttribute attribute, Type type)
        {
            switch (attribute.Constructor.Kind)
            {
                case HandleKind.MethodDefinition:
                    // System.Reflection.Metadata does not present the Parent of a MethodDefinition
                    // However, this only applies when an attribute is defined in the same file as its use
                    return false;

                case HandleKind.MemberReference:
                    MemberReference member = _metadataReader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                    StringHandle typeName;
                    StringHandle typeNamespace;
                    switch (member.Parent.Kind)
                    {
                        case HandleKind.TypeReference:
                            TypeReference typeRef = _metadataReader.GetTypeReference((TypeReferenceHandle)member.Parent);
                            typeName = typeRef.Name;
                            typeNamespace = typeRef.Namespace;
                            break;

                        case HandleKind.TypeDefinition:
                            TypeDefinition typeDef = _metadataReader.GetTypeDefinition((TypeDefinitionHandle)member.Parent);
                            typeName = typeDef.Name;
                            typeNamespace = typeDef.Namespace;
                            break;

                        default:
                            return false;
                    }
                    return _metadataReader.GetString(typeName) == type.Name && _metadataReader.GetString(typeNamespace) == type.Namespace;

                default:
                    return false;
            }
        }

        private void SetKeywordAttributeParameters(CustomAttribute keywordAttribute, out DynamicKeywordBodyMode bodyMode, out DynamicKeywordUseMode useMode)
        {
            var keywordValue = keywordAttribute.DecodeValue(DslReaderTypeProvider);
            bodyMode = DynamicKeywordBodyMode.Command;
            useMode = DynamicKeywordUseMode.OptionalMany;

            foreach (var attributeArgument in keywordValue.NamedArguments)
            {
                if (attributeArgument.Type == typeof(DynamicKeywordBodyMode))
                {
                    bodyMode = (DynamicKeywordBodyMode)attributeArgument.Value;
                }
                else if (attributeArgument.Type == typeof(DynamicKeywordUseMode))
                {
                    useMode = (DynamicKeywordUseMode)attributeArgument.Value;
                }
            }
        }

        private class TypingGenericContext
        {
            public TypingGenericContext(ImmutableArray<Type> typeParameters, ImmutableArray<Type> methodParamters)
            {
                MethodParameters = methodParamters;
                TypeParameters = typeParameters;
            }

            public ImmutableArray<Type> MethodParameters { get; }
            public ImmutableArray<Type> TypeParameters { get; }
        }

        /// <summary>
        /// Type provider to translate the MetadataReader's decoded type into a Type
        /// </summary>
        private class TypingTypeProvider : ISignatureTypeProvider<Type, TypingGenericContext>, ICustomAttributeTypeProvider<Type>
        {
            public Type GetArrayType(Type elementType, ArrayShape shape)
            {
                throw new NotImplementedException();
            }

            public Type GetByReferenceType(Type elementType)
            {
                return elementType;
            }

            public Type GetFunctionPointerType(MethodSignature<Type> signature)
            {
                throw new NotImplementedException();
            }

            public Type GetGenericInstantiation(Type genericType, ImmutableArray<Type> typeArguments)
            {
                string typeName = genericType.ToString() + "<" + String.Join(",", typeArguments.Select(t => t.ToString())) + ">";

                return Type.GetType(typeName);
            }

            public Type GetGenericMethodParameter(TypingGenericContext genericContext, int index)
            {
                return genericContext.MethodParameters[index];
            }

            public Type GetGenericTypeParameter(TypingGenericContext genericContext, int index)
            {
                return genericContext.TypeParameters[index];
            }

            public Type GetModifiedType(Type modifier, Type unmodifiedType, bool isRequired)
            {
                return unmodifiedType;
            }

            public Type GetPinnedType(Type elementType)
            {
                return elementType;
            }

            public Type GetPointerType(Type elementType)
            {
                return elementType;
            }

            /// <summary>
            /// Get the Type representation corresponding to a primitive type code.
            /// TypedReferences are not supported in dotnetCore and will fail
            /// </summary>
            /// <param name="typeCode">the cil metadata type code of the value</param>
            /// <returns>a C# type corresponding to the given type code</returns>
            public Type GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                switch (typeCode)
                {
                    case PrimitiveTypeCode.Boolean:
                        return typeof(bool);

                    case PrimitiveTypeCode.Byte:
                        return typeof(byte);

                    case PrimitiveTypeCode.Char:
                        return typeof(char);

                    case PrimitiveTypeCode.Double:
                        return typeof(double);

                    case PrimitiveTypeCode.Int16:
                        return typeof(short);

                    case PrimitiveTypeCode.Int32:
                        return typeof(int);

                    case PrimitiveTypeCode.Int64:
                        return typeof(long);

                    case PrimitiveTypeCode.IntPtr:
                        return typeof(IntPtr);

                    case PrimitiveTypeCode.Object:
                        return typeof(object);

                    case PrimitiveTypeCode.SByte:
                        return typeof(sbyte);

                    case PrimitiveTypeCode.Single:
                        return typeof(float);

                    case PrimitiveTypeCode.String:
                        return typeof(string);

                    case PrimitiveTypeCode.TypedReference:
                        throw new NotImplementedException("TypedReference not supported in dotnetCore");

                    case PrimitiveTypeCode.UInt16:
                        return typeof(ushort);

                    case PrimitiveTypeCode.UInt32:
                        return typeof(uint);

                    case PrimitiveTypeCode.UInt64:
                        return typeof(ulong);

                    case PrimitiveTypeCode.UIntPtr:
                        return typeof(UIntPtr);

                    case PrimitiveTypeCode.Void:
                        return typeof(void);

                    default:
                        throw new ArgumentOutOfRangeException("Unrecognized primitive type: " + typeCode.ToString());
                }
            }

            /// <summary>
            /// Get the Type representation of System.Type
            /// </summary>
            /// <returns></returns>
            public Type GetSystemType()
            {
                return typeof(Type);
            }

            /// <summary>
            /// </summary>
            /// <param name="elementType"></param>
            /// <returns></returns>
            public Type GetSZArrayType(Type elementType)
            {
                return Type.GetType(elementType.ToString() + "[]");
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="reader"></param>
            /// <param name="handle"></param>
            /// <param name="rawTypeKind"></param>
            /// <returns></returns>
            public Type GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind=0)
            {
                TypeDefinition typeDef = reader.GetTypeDefinition(handle);

                string typeDefName = reader.GetString(typeDef.Name);

                // Check if type definition is nested
                // This will be typeDef.Attributes.IsNested() in later releases
                if (typeDef.Attributes.HasFlag((System.Reflection.TypeAttributes)0x00000006))
                {
                    TypeDefinitionHandle declaringTypeHandle = typeDef.GetDeclaringType();
                    Type enclosingType = GetTypeFromDefinition(reader, declaringTypeHandle);
                    return Type.GetType(Assembly.CreateQualifiedName(enclosingType.AssemblyQualifiedName, enclosingType.ToString() + "+" + typeDefName));
                }

                string typeDefNamespace = reader.GetString(typeDef.Namespace);
                return Type.GetType(Assembly.CreateQualifiedName(typeDefNamespace, typeDefName));
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="reader"></param>
            /// <param name="handle"></param>
            /// <param name="rawTypeKind"></param>
            /// <returns></returns>
            public Type GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind=0)
            {
                TypeReference typeRef = reader.GetTypeReference(handle);
                string typeRefName = reader.GetString(typeRef.Name);
                if (typeRef.Namespace.IsNil)
                {
                    return Type.GetType(typeRefName);
                }
                string typeRefNamespace = reader.GetString(typeRef.Namespace);
                return Type.GetType(Assembly.CreateQualifiedName(typeRefNamespace, typeRefName));
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="name"></param>
            /// <returns></returns>
            public Type GetTypeFromSerializedName(string name)
            {
                return Type.GetType(name);
            }

            public Type GetTypeFromSpecification(MetadataReader reader, TypingGenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="type"></param>
            /// <returns></returns>
            public PrimitiveTypeCode GetUnderlyingEnumType(Type type)
            {
                if (type == typeof(DynamicKeywordBodyMode) || type == typeof(DynamicKeywordUseMode))
                {
                    return PrimitiveTypeCode.Int32;
                }

                throw new ArgumentOutOfRangeException("Not a known parameter enum type");
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="type"></param>
            /// <returns></returns>
            public bool IsSystemType(Type type)
            {
                return type == typeof(Type);
            }
        }
    }
    #endregion /* DSL Metadata Reading */
}