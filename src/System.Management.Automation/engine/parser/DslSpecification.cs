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
using System.Collections;
using System.Collections.Immutable;
using System.Text;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

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
            RuntimeCall = null;
            CompilationStrategy = DefaultCompilationStrategy;
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
        /// Specifies the call to be made at runtime for the keyword invocation
        /// </summary>
        public Func<Dictionary<string, object>, Stack<Dictionary<string, object>>, object> RuntimeCall
        {
            get; set;
        }

        /// <summary>
        /// Allows the specification of a new compilation algorithm, allowing the definition of
        /// arbitrary DynamicKeywordStatementAst semantics
        /// </summary>
        public Func<Compiler, DynamicKeywordStatementAst, Expression> CompilationStrategy
        {
            get; set;
        }

        /// <summary>
        /// Resolves a keyword command into Ast components
        /// </summary>
        /// <param name="kwAst">dynamic keyword statement ast node</param>
        /// <returns></returns>
        protected StaticBindingResult ParameterResolutionHelper(DynamicKeywordStatementAst kwAst)
        {
            var commandElements = Ast.CopyElements(kwAst.CommandElements);
            var commandAst = new CommandAst(kwAst.Extent, commandElements, TokenKind.Unknown, null);
            return StaticParameterBinder.BindCommand(commandAst);
        }

        private static Expression DefaultCompilationStrategy(Compiler compiler, DynamicKeywordStatementAst kwAst)
        {
            if (kwAst.Keyword.RuntimeCall == null)
            {
                return Expression.Empty();
            }

            var args = new Expression[kwAst.CommandElements.Count];
            for (int i = 0; i < kwAst.CommandElements.Count; i++)
            {
                args[i] = compiler.VisitCommandElement(kwAst.CommandElements[i]);
            }

            Expression<Func<Dictionary<string, object>, Stack<Dictionary<string, object>>, object>> runtimeFunc = (vars, state) => kwAst.Keyword.RuntimeCall(vars, state);
            Expression runtimeInvocation = Expression.Invoke(runtimeFunc, Expression.Constant(kwAst));

            // TODO: Add parameter passing and recursive scriptblock compilation

            return compiler.CallAddCurrentPipe(runtimeInvocation);
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
        private Stack<Dictionary<string, List<string>>> _enumDefStack;
        private Stack<HashSet<string>> _keywordDefinitionStack;

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
        public IDictionary<string, DynamicKeyword> ReadDslSpecification()
        {
            // TODO: Ensure the module is a DLL, else return null

            IDictionary<string, DynamicKeyword> globalKeywords;

            using (FileStream stream = File.OpenRead(_moduleInfo.Path))
            using (var peReader = new PEReader(stream))
            {
                if (!peReader.HasMetadata)
                {
                    return null;
                }

                _metadataReader = peReader.GetMetadataReader();
                _enumDefStack = new Stack<Dictionary<string, List<string>>>();
                _keywordDefinitionStack = new Stack<HashSet<string>>();
                globalKeywords =  ReadGlobalDynamicKeywords();
            }

            // TODO: Move this somewhere later in the interpreter
            // TODO: Find out if reusing a readonly FileStream is acceptable/feasibly
            using (FileStream stream = File.OpenRead(_moduleInfo.Path))
            {
                Assembly dslDefinitionAsm = ClrFacade.LoadFrom(stream);
                ReadGlobalKeywordFunctions(globalKeywords, dslDefinitionAsm);
            }

            return globalKeywords;
        }

        private TypingTypeProvider TypeLookupProvider
        {
            get
            {
                return s_typeLookupProvider ??
                    (s_typeLookupProvider = new TypingTypeProvider());
            }
        }
        private static TypingTypeProvider s_typeLookupProvider;

        private TypeNameTypeProvider TypeNameProvider
        {
            get
            {
                return s_typeNameProvider ??
                    (s_typeNameProvider = new TypeNameTypeProvider());
            }
        }
        private static TypeNameTypeProvider s_typeNameProvider;

        /// <summary>
        /// Reads the top level dynamic keywords in a DLL using the metadata
        /// reader for that DLL. This constructs all nested keywords in the same DLL
        /// by recursive descent.
        /// </summary>
        /// <returns>an array of the top level keywords defined in the DLL</returns>
        private IDictionary<string, DynamicKeyword> ReadGlobalDynamicKeywords()
        {
            var globalKeywords = new Dictionary<string, DynamicKeyword>();

            // Read in any defined enums to helpfully resolve types for parameters
            _enumDefStack.Push(ReadEnumDefinitions(_metadataReader.TypeDefinitions));

            _keywordDefinitionStack.Push(new HashSet<string>());
            foreach (var typeDefHandle in _metadataReader.TypeDefinitions)
            {
                var typeDef = _metadataReader.GetTypeDefinition(typeDefHandle);
                // Make sure this keyword is not nested (declared as an inner class)
                var declaringType = typeDef.GetDeclaringType();
                if (declaringType.IsNil && IsKeywordSpecification(typeDef))
                {
                    DynamicKeyword keyword = ReadKeywordSpecification(typeDef);
                    if (keyword.UseMode != DynamicKeywordUseMode.OptionalMany)
                    {
                        // TODO: Make this error user-consumable
                        throw new PSNotSupportedException("Specifying a restrictive use mode for global dynamic keywords is not supported");
                    }
                    globalKeywords.Add(keyword.Keyword, keyword);
                }
            }

            return globalKeywords;
        }

        /// <summary>
        /// Read in any enum definitions at the current type definition level
        /// </summary>
        /// <param name="typeDefinitions"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> ReadEnumDefinitions(IEnumerable<TypeDefinitionHandle> typeDefinitions)
        {
            var enumDefs = new Dictionary<string, List<String>>();
            foreach (var typeDefHandle in _metadataReader.TypeDefinitions)
            {
                var typeDef = _metadataReader.GetTypeDefinition(typeDefHandle);
                switch (typeDef.BaseType.Kind)
                {
                    case HandleKind.TypeReference:
                        var baseType = _metadataReader.GetTypeReference((TypeReferenceHandle)typeDef.BaseType);
                        if (String.Join(".", _metadataReader.GetString(baseType.Namespace), _metadataReader.GetString(baseType.Name)) == "System.Enum")
                        {
                            var enumFields = new List<string>();
                            foreach (FieldDefinitionHandle enumFieldHandle in typeDef.GetFields())
                            {
                                FieldDefinition field = _metadataReader.GetFieldDefinition(enumFieldHandle);
                                string fieldName = _metadataReader.GetString(field.Name);
                                if (fieldName != "value__")
                                {
                                    enumFields.Add(fieldName);
                                }
                            }
                            enumDefs.Add(_metadataReader.GetString(typeDef.Name), enumFields);
                        }
                        break;
                }
            }
            return enumDefs;
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
            // Register the keyword name
            string keywordName = _metadataReader.GetString(typeDef.Name);
            foreach (var enclosingScope in _keywordDefinitionStack)
            {
                if (enclosingScope.Contains(keywordName))
                {
                    // TODO: Make this more PS-compatible for consumption by users
                    throw PSTraceSource.NewNotSupportedException("Cannot define two keywords of the same name in the same scope");
                }
            }
            _keywordDefinitionStack.Peek().Add(keywordName);

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

            // Read in enumerated types at the current level
            _enumDefStack.Push(ReadEnumDefinitions(typeDef.GetNestedTypes()));

            // Set the generic context
            var genericTypeParameters = typeDef.GetGenericParameters()
                .Select(h => _metadataReader.GetString(_metadataReader.GetGenericParameter(h).Name)).ToImmutableArray();
            var genericContext = new TypeNameGenericContext(genericTypeParameters, ImmutableArray<string>.Empty);

            // Read in all parameters and properties defined as class properties
            var keywordParameters = new List<DynamicKeywordParameter>();
            var keywordProperties = new List<DynamicKeywordProperty>();
            foreach (var propertyHandle in typeDef.GetProperties())
            {
                var property = _metadataReader.GetPropertyDefinition(propertyHandle);
                foreach (var attributeHandle in property.GetCustomAttributes())
                {
                    var keywordMemberAttribute = _metadataReader.GetCustomAttribute(attributeHandle);
                    if (IsKeywordParameterAttribute(keywordMemberAttribute))
                    {
                        keywordParameters.Add(ReadParameterSpecification(genericContext, property, keywordMemberAttribute));
                        break;
                    }
                    else if (IsKeywordPropertyAttribute(keywordMemberAttribute))
                    {
                        keywordProperties.Add(ReadPropertySpecification(genericContext, property, keywordMemberAttribute));
                        break;
                    }
                }
            }

            // Read in all nested keywords below this one
            _keywordDefinitionStack.Push(new HashSet<string>());
            List<DynamicKeyword> innerKeywords = new List<DynamicKeyword>();
            foreach (var innerTypeHandle in typeDef.GetNestedTypes())
            {
                var innerTypeDef = _metadataReader.GetTypeDefinition(innerTypeHandle);
                if (IsKeywordSpecification(innerTypeDef))
                {
                    if (bodyMode == DynamicKeywordBodyMode.Command)
                    {
                        // TODO: Make this more user-consumable
                        throw PSTraceSource.NewNotSupportedException("Cannot define keywords underneath a command-body keyword");
                    }
                    innerKeywords.Add(ReadKeywordSpecification(innerTypeDef));
                }
            }
            _keywordDefinitionStack.Pop();

            // Set all the properties for the keyword itself
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
            foreach (var keywordProperty in keywordProperties)
            {
                keyword.Properties.Add(keywordProperty.Name, keywordProperty);
            }
            foreach (var innerKeyword in innerKeywords)
            {
                keyword.InnerKeywords.Add(innerKeyword.Keyword, innerKeyword);
            }

            _enumDefStack.Pop();

            return keyword;
        }

        private bool IsKeywordParameterAttribute(CustomAttribute keywordParameterAttribute)
        {
            return IsAttributeOfType(keywordParameterAttribute, typeof(KeywordParameterAttribute));
        }

        private bool IsKeywordPropertyAttribute(CustomAttribute keywordPropertyAttribute)
        {
            return IsAttributeOfType(keywordPropertyAttribute, typeof(KeywordPropertyAttribute));
        }

        private bool IsKeywordAttribute(CustomAttribute keywordAttribute)
        {
            return IsAttributeOfType(keywordAttribute, typeof(KeywordAttribute));
        }

        private bool IsKeywordSpecification(TypeDefinition typeDef)
        {
            // Ensure the keyword inherits from System.Management.Automation.Language.Keyword
            EntityHandle baseTypeHandle = typeDef.BaseType;
            switch (baseTypeHandle.Kind)
            {
                case HandleKind.TypeReference:
                    TypeReference typeRef = _metadataReader.GetTypeReference((TypeReferenceHandle)baseTypeHandle);
                    if (typeof(Keyword).Name != _metadataReader.GetString(typeRef.Name) && typeof(Keyword).Namespace != _metadataReader.GetString(typeRef.Namespace))
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }

            // Ensure the keyword is annotated with the KeywordAttribute
            foreach (CustomAttributeHandle caHandle in typeDef.GetCustomAttributes())
            {
                CustomAttribute customAttribute = _metadataReader.GetCustomAttribute(caHandle);
                if (IsKeywordAttribute(customAttribute))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Read in the specification for a parameter for a DynamicKeyword. This involves recording the name and type of the
        /// corresponding property, as well as reading in position/mandatory properties from the KeywordParameter attribute.
        /// </summary>
        /// <param name="property">the property representing the DynamicKeyword parameter</param>
        /// <param name="keywordParameterAttribute">the attribute on the property declaring the parameter's properties (position, mandatory)</param>
        /// <param name="genericContext">the generic type context in which the property is used</param>
        /// <returns></returns>
        private DynamicKeywordParameter ReadParameterSpecification(TypeNameGenericContext genericContext, PropertyDefinition property, CustomAttribute keywordParameterAttribute)
        {
            string parameterName = _metadataReader.GetString(property.Name);
            string parameterType = property.DecodeSignature(TypeNameProvider, genericContext).ReturnType.ToString();

            CustomAttributeValue<Type> paramAttrValue = keywordParameterAttribute.DecodeValue(TypeLookupProvider);
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

            var dkParameter = new DynamicKeywordParameter()
            {
                Name = parameterName,
                TypeConstraint = parameterType,
                Position = position,
                Mandatory = mandatory,
            };
            TrySetMemberEnumType(dkParameter);
            return dkParameter;
        }

        private DynamicKeywordProperty ReadPropertySpecification(TypeNameGenericContext genericContext, PropertyDefinition property, CustomAttribute keywordPropertyAttribute)
        {
            string propertyName = _metadataReader.GetString(property.Name);
            string propertyType = property.DecodeSignature(TypeNameProvider, genericContext).ReturnType;

            bool mandatory = false;
            CustomAttributeValue<Type> propertyValue = keywordPropertyAttribute.DecodeValue(TypeLookupProvider);
            foreach (var propertyProperty in propertyValue.NamedArguments)
            {
                switch (propertyProperty.Name)
                {
                    case "Mandatory":
                        mandatory = (bool)propertyProperty.Value;
                        break;
                }
            }

            var dkProperty = new DynamicKeywordProperty()
            {
                Name = propertyName,
                TypeConstraint = propertyType,
                Mandatory = mandatory,
            };
            TrySetMemberEnumType(dkProperty);
            return dkProperty;
        }

        private bool TrySetMemberEnumType(DynamicKeywordProperty keywordProperty)
        {
            string propertyType = keywordProperty.TypeConstraint;
            foreach (var enumScope in _enumDefStack)
            {
                if (enumScope.ContainsKey(propertyType))
                {
                    keywordProperty.Values.AddRange(enumScope[propertyType]);
                    return true;
                }
            }

            return false;
        }

        private bool IsAttributeOfType(CustomAttribute attribute, Type type)
        {
            switch (attribute.Constructor.Kind)
            {
                case HandleKind.MethodDefinition:
                    // System.Reflection.Metadata does not present the Parent of a MethodDefinition
                    // However, this only applies when an attribute is defined in the same file that uses it
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
            var keywordValue = keywordAttribute.DecodeValue(TypeLookupProvider);
            bodyMode = DynamicKeywordBodyMode.Command;
            useMode = DynamicKeywordUseMode.OptionalMany;

            foreach (var attributeArgument in keywordValue.NamedArguments)
            {
                switch (attributeArgument.Name)
                {
                    case "Body":
                        bodyMode = (DynamicKeywordBodyMode)attributeArgument.Value;
                        break;
                    case "Use":
                        useMode = (DynamicKeywordUseMode)attributeArgument.Value;
                        break;
                }
            }
        }

        /// <summary>
        /// Read in semantic calls on globally scoped keywords
        /// </summary>
        /// <param name="globalKeywords">the global keywords to add functions to</param>
        /// <param name="definingAssembly">the assembly that defines the keyword specifications</param>
        private void ReadGlobalKeywordFunctions(IDictionary<string, DynamicKeyword> globalKeywords, Assembly definingAssembly)
        {
            foreach (var typeDef in definingAssembly.DefinedTypes)
            {
                if (globalKeywords.ContainsKey(typeDef.Name))
                {
                    ReadKeywordFunctions(globalKeywords[typeDef.Name], typeDef);
                }
            }
        }

        /// <summary>
        /// Read in the semantic calls on a Keyword specification to attach to a DynamicKeyword
        /// </summary>
        /// <param name="keyword">the DynamicKeyword to add runtime calls to</param>
        /// <param name="typeDefintion">the info class of the Keyword specification to load from</param>
        private void ReadKeywordFunctions(DynamicKeyword keyword, TypeInfo typeDefintion)
        {
            foreach (var innerKeyword in keyword.InnerKeywords.Values)
            {
                TypeInfo nestedType = typeDefintion.GetDeclaredNestedType(innerKeyword.Keyword);
                if (nestedType != null)
                {
                    ReadKeywordFunctions(innerKeyword, nestedType);
                }
                else
                {
                    // TODO: Throw something about expecting a type that was not found
                    PSTraceSource.NewArgumentException("Expected to find keyword definition '{0}'. Has the defining module been modified?", innerKeyword.Keyword);
                }
            }

            Keyword keywordDefinition = (Keyword) Activator.CreateInstance(typeDefintion.AsType());
            keyword.PreParse = keywordDefinition.PreParse;
            keyword.PostParse = keywordDefinition.PostParse;
            keyword.SemanticCheck = keywordDefinition.SemanticCheck;
            keyword.RuntimeCall = keywordDefinition.RuntimeCall;
        }

        private interface IGenericContext<TType>
        {
            ImmutableArray<TType> TypeParameters { get; }
            ImmutableArray<TType> MethodParameters { get; }
        }

        private struct TypingGenericContext : IGenericContext<Type>
        {
            public TypingGenericContext(ImmutableArray<Type> typeParameters, ImmutableArray<Type> methodParamters)
            {
                MethodParameters = methodParamters;
                TypeParameters = typeParameters;
            }

            public ImmutableArray<Type> MethodParameters { get; }
            public ImmutableArray<Type> TypeParameters { get; }
        }

        private struct TypeNameGenericContext : IGenericContext<string>
        {
            public TypeNameGenericContext(ImmutableArray<string> typeParameters, ImmutableArray<string> methodParameters)
            {
                MethodParameters = methodParameters;
                TypeParameters = typeParameters;
            }

            public ImmutableArray<string> MethodParameters { get; }

            public ImmutableArray<string> TypeParameters { get; }
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

        private class TypeNameTypeProvider : ISignatureTypeProvider<string, TypeNameGenericContext>, ICustomAttributeTypeProvider<string>
        {
            public string GetArrayType(string elementType, ArrayShape shape)
            {
                var builder = new StringBuilder();

                builder.Append(elementType);
                builder.Append('[');

                for (int i = 0; i < shape.Rank; i++)
                {
                    int lowerBound = 0;

                    if (i < shape.LowerBounds.Length)
                    {
                        lowerBound = shape.LowerBounds[i];
                        builder.Append(lowerBound);
                    }

                    builder.Append("...");

                    if (i < shape.Sizes.Length)
                    {
                        builder.Append(lowerBound + shape.Sizes[i] - 1);
                    }

                    if (i < shape.Rank - 1)
                    {
                        builder.Append(',');
                    }
                }

                builder.Append(']');
                return builder.ToString();
            }

            public string GetByReferenceType(string elementType)
            {
                return elementType + "&";
            }

            public string GetFunctionPointerType(MethodSignature<string> signature)
            {
                ImmutableArray<string> parameterTypes = signature.ParameterTypes;

                int requiredParameterCount = signature.RequiredParameterCount;

                var builder = new StringBuilder();
                builder.Append("method ");
                builder.Append(signature.ReturnType);
                builder.Append(" *(");

                int i;
                for (i = 0; i < requiredParameterCount; i++)
                {
                    builder.Append(parameterTypes[i]);
                    if (i < parameterTypes.Length - 1)
                    {
                        builder.Append(", ");
                    }
                }

                if (i < parameterTypes.Length)
                {
                    builder.Append("..., ");
                    for (; i < parameterTypes.Length; i++)
                    {
                        builder.Append(parameterTypes[i]);
                        if (i < parameterTypes.Length - 1)
                        {
                            builder.Append(", ");
                        }
                    }
                }

                builder.Append(')');
                return builder.ToString();
            }

            public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            {
                return genericType + "<" + String.Join(",", typeArguments) + ">";
            }

            public string GetGenericMethodParameter(TypeNameGenericContext genericContext, int index)
            {
                return "!!" + genericContext.MethodParameters[index];
            }

            public string GetGenericTypeParameter(TypeNameGenericContext genericContext, int index)
            {
                return "!" + genericContext.TypeParameters[index];
            }

            public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
            {
                return unmodifiedType + (isRequired ? " modreq(" : " modopt(") + modifier + ")";
            }

            public string GetPinnedType(string elementType)
            {
                return elementType + " pinned";
            }

            public string GetPointerType(string elementType)
            {
                return elementType + "*";
            }

            public string GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                switch (typeCode)
                {
                    case PrimitiveTypeCode.Boolean:
                        return "bool";

                    case PrimitiveTypeCode.Byte:
                        return "byte";

                    case PrimitiveTypeCode.Char:
                        return "char";

                    case PrimitiveTypeCode.Double:
                        return "double";

                    case PrimitiveTypeCode.Int16:
                        return "short";

                    case PrimitiveTypeCode.Int32:
                        return "int";

                    case PrimitiveTypeCode.Int64:
                        return "long";

                    case PrimitiveTypeCode.IntPtr:
                        return typeof(IntPtr).ToString();

                    case PrimitiveTypeCode.Object:
                        return "object";

                    case PrimitiveTypeCode.SByte:
                        return "sbyte";

                    case PrimitiveTypeCode.Single:
                        return "float";

                    case PrimitiveTypeCode.String:
                        return "string";

                    case PrimitiveTypeCode.TypedReference:
                        throw new NotImplementedException("dotnet core does not implement TypedReference");

                    case PrimitiveTypeCode.UInt16:
                        return "ushort";

                    case PrimitiveTypeCode.UInt32:
                        return "uint";

                    case PrimitiveTypeCode.UInt64:
                        return "ulong";

                    case PrimitiveTypeCode.UIntPtr:
                        return typeof(UIntPtr).ToString();

                    case PrimitiveTypeCode.Void:
                        return "void";

                    default:
                        throw new ArgumentOutOfRangeException("Unrecognized primitive type");
                }
            }

            public string GetSystemType()
            {
                return typeof(System.Type).ToString();
            }

            public string GetSZArrayType(string elementType)
            {
                return elementType + "[]";
            }

            public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind = 0)
            {
                TypeDefinition typeDef = reader.GetTypeDefinition(handle);

                string name = typeDef.Namespace.IsNil
                    ? reader.GetString(typeDef.Name)
                    : reader.GetString(typeDef.Namespace) + "." + reader.GetString(typeDef.Name);

                // Test if the typedef is nested -- future implementations have typeDef.Attributes.IsNested()
                if (typeDef.Attributes.HasFlag((System.Reflection.TypeAttributes)0x6))
                {
                    TypeDefinitionHandle declaringTypeHandle = typeDef.GetDeclaringType();
                    return GetTypeFromDefinition(reader, declaringTypeHandle) + "/" + name;
                }

                return name;
            }

            public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind = 0)
            {
                TypeReference reference = reader.GetTypeReference(handle);
                Handle scope = reference.ResolutionScope;

                string name = reference.Namespace.IsNil
                    ? reader.GetString(reference.Name)
                    : reader.GetString(reference.Namespace) + "." + reader.GetString(reference.Name);

                switch (scope.Kind)
                {
                    case HandleKind.ModuleReference:
                        return "[.module" + reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)scope).Name) + "]" + name;

                    case HandleKind.AssemblyReference:
                        var assemblyReference = reader.GetAssemblyReference((AssemblyReferenceHandle)scope);
                        return "[" + reader.GetString(assemblyReference.Name) + "]" + name;

                    case HandleKind.TypeReference:
                        return GetTypeFromReference(reader, (TypeReferenceHandle)scope) + "/" + name;

                    default:
                        if (scope == Handle.ModuleDefinition || scope.IsNil)
                        {
                            return name;
                        }
                        throw new ArgumentOutOfRangeException("Unrecognized type handle scope reference");
                }
            }

            public string GetTypeFromSerializedName(string name)
            {
                return name;
            }

            public string GetTypeFromSpecification(MetadataReader reader, TypeNameGenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
            }

            public PrimitiveTypeCode GetUnderlyingEnumType(string type)
            {
                Type runtimeType = Type.GetType(type.Replace('/', '+'));

                if (runtimeType == typeof(DynamicKeywordBodyMode) || runtimeType == typeof(DynamicKeywordUseMode))
                {
                    return PrimitiveTypeCode.Int32;
                }

                throw new ArgumentOutOfRangeException("Unrecognized enumerated type");
            }

            public bool IsSystemType(string type)
            {
                return type == "[System.Runtime]System.Type" || Type.GetType(type) == typeof(Type); 
            }
        }
    }
    #endregion /* DSL Metadata Reading */
}