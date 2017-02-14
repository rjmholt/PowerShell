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
using System.Management.Automation.Language;
using System.Management.Automation.Internal;

namespace System.Management.Automation.Internal
{
    #region KeywordInfo

    /// <summary>
    /// Info class representing the runtime type information for a Dynamic Keyword
    /// </summary>
    public class KeywordInfo : CommandInfo
    {
        internal KeywordInfo(DllDefinedDynamicKeyword keywordData, Type definingType, string definition)
            : base(keywordData.Keyword, CommandTypes.DynamicKeyword)
        {
            if (!typeof(Keyword).IsAssignableFrom(definingType))
            {
                throw PSTraceSource.NewArgumentOutOfRangeException(nameof(definingType), definingType);
            }

            if (keywordData == null)
            {
                throw PSTraceSource.NewArgumentNullException(nameof(keywordData));
            }

            if (String.IsNullOrEmpty(definition))
            {
                throw PSTraceSource.NewArgumentNullException(nameof(definition));
            }

            ImplementingType = definingType;
            KeywordData = keywordData;
            _definition = definition;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">the other instance of KeywordInfo</param>
        internal KeywordInfo(KeywordInfo other) : this(other.KeywordData, other.ImplementingType, other.Definition)
        {
            _keywordCtor = other._keywordCtor;
            _keywordDefinitionInstance = other._keywordDefinitionInstance;
            _keywordInfoField = other._keywordInfoField;
        }

        /// <summary>
        /// The source definition of the keyword
        /// </summary>
        public override string Definition
        {
            get
            {
                return _definition;
            }
        }
        private string _definition = String.Empty;

        /// <summary>
        /// The output type of a DynamicKeyword is just object. There may be a way to extract more information though
        /// </summary>
        public override ReadOnlyCollection<PSTypeName> OutputType
        {
            get
            {
                return new ReadOnlyCollection<PSTypeName>(new[] { new PSTypeName(typeof(object)) });
            }
        }

        /// <summary>
        /// Gets/sets scope options for this keyword
        /// </summary>
        public ScopedItemOptions Options
        {
            get
            {
                return _options;
            }

            set
            {
                SetOptions(value, false);
            }
        }
        private ScopedItemOptions _options = ScopedItemOptions.None;

        internal void SetOptions(ScopedItemOptions newOptions, bool force)
        {
            if ((_options & ScopedItemOptions.ReadOnly) != 0)
            {
                throw new SessionStateUnauthorizedAccessException(
                        Name,
                        SessionStateCategory.DynamicKeyword,
                        "DynamicKeywordIsReadOnly",
                        SessionStateStrings.CmdletIsReadOnly);
            }

            _options = newOptions;
        }

        /// <summary>
        /// True if the keyword is nested inside another keyword, false otherwise
        /// </summary>
        public bool IsNested
        {
            get
            {
                return KeywordData.IsNested;
            }
        }

        /// <summary>
        /// An instance of the keyword instantiated to retrieve its delegates
        /// </summary>
        internal Keyword KeywordDefinitionInstance
        {
            get
            {
                if (_keywordDefinitionInstance == null)
                {
                    _keywordDefinitionInstance = (Keyword)KeywordCtor.Invoke(new object[0]);
                    _keywordDefinitionInstance.KeywordInfo = this;
                }
                return _keywordDefinitionInstance;
            }
        }
        private Keyword _keywordDefinitionInstance;

        /// <summary>
        /// The constructor delegate for the keyword
        /// </summary>
        internal ConstructorInfo KeywordCtor
        {
            get
            {
                if (_keywordCtor == null)
                {
                    _keywordCtor = ImplementingType.GetConstructor(new Type[0]);
                }
                return _keywordCtor;
            }
        }
        private ConstructorInfo _keywordCtor;

        /// <summary>
        /// An expression representing the keyword constructor
        /// </summary>
        internal Expression CreateNewKeywordExpression
        {
            get
            {
                // TODO: This could be made to operate at runtime rather than compile time,
                // but doing so is fiddly and error prone.
                Keyword keyword = (Keyword)KeywordCtor.Invoke(new object[0]);
                keyword.KeywordInfo = this;
                return Expression.Constant(keyword);
            }
        }

        internal PropertyInfo KeywordInfoField
        {
            get
            {
                if (_keywordInfoField == null)
                {
                    _keywordInfoField = ImplementingType.GetProperty(nameof(Keyword.KeywordInfo), BindingFlags.NonPublic | BindingFlags.Public);
                }

                return _keywordInfoField;
            }
        }
        private PropertyInfo _keywordInfoField;

        /// <summary>
        /// The CIL type that defines this keyword
        /// </summary>
        internal Type ImplementingType { get; }

        /// <summary>
        /// The metadata about the dynamic keyword
        /// </summary>
        public DllDefinedDynamicKeyword KeywordData { get; }

        /// <summary>
        /// A custom function that gets executed on the DynamicKeyword definition at parsing time before parsing dynamickeyword block
        /// </summary>
        internal Func<DynamicKeyword, ParseError[]> PreParseCall
        {
            get
            {
                return KeywordDefinitionInstance.PreParse;
            }
        }

        /// <summary>
        /// A custom function that gets executed at parsing time after parsing dynamickeyword block
        /// </summary>
        internal Func<DynamicKeywordStatementAst, ParseError[]> PostParseCall
        {
            get
            {
                return KeywordDefinitionInstance.PostParse;
            }
        }

        /// <summary>
        /// A custom function that checks semantic for the given <see cref="DynamicKeywordStatementAst"/>
        /// </summary>
        internal Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheckCall
        {
            get
            {
                return KeywordDefinitionInstance.SemanticCheck;
            }
        }

        /// <summary>
        /// Function defining the algorithm for compiling the keyword
        /// </summary>
        internal Func<Compiler, ParameterExpression, DynamicKeywordStatementAst, Expression> CompilationStrategy
        {
            get
            {
                return KeywordDefinitionInstance.CompilationStrategy;
            }
        }

        /// <summary>
        /// Detect whether this object has overridden RuntimeEnterScope
        /// </summary>
        internal bool HasEnterScopeCall
        {
            get
            {
                if (_hasEnterScopeCall.HasValue)
                {
                    return _hasEnterScopeCall.Value;
                }
                MethodInfo enterScopeInfo = ImplementingType.GetMethod(nameof(Keyword.RuntimeEnterScope), new [] { typeof(IEnumerable<Tuple<Keyword, object>>) });
                bool isOverridden = enterScopeInfo.DeclaringType == ImplementingType;
                isOverridden &= enterScopeInfo.GetBaseDefinition().DeclaringType == typeof(Keyword);
                _hasEnterScopeCall = isOverridden;
                return _hasEnterScopeCall.Value;
            }
        }
        private bool? _hasEnterScopeCall;

        /// <summary>
        /// Detect whether this object has overridden RuntimeLeaveScope
        /// </summary>
        internal bool HasLeaveScopeCall
        {
            get
            {
                if (_hasLeaveScopeCall.HasValue)
                {
                    return _hasLeaveScopeCall.Value;
                }
                MethodInfo leaveScopeInfo = ImplementingType.GetMethod(nameof(Keyword.RuntimeLeaveScope), new [] { typeof(IEnumerable<Tuple<Keyword, object>>), typeof(List<object>) });
                bool isOverridden = leaveScopeInfo.DeclaringType == ImplementingType;
                isOverridden &= leaveScopeInfo.GetBaseDefinition().DeclaringType == typeof(Keyword);
                _hasLeaveScopeCall = isOverridden;
                return _hasLeaveScopeCall.Value;
            }
        }
        private bool? _hasLeaveScopeCall;

        internal override CommandMetadata CommandMetadata
        {
            get
            {
                if (_commandMetadata == null)
                {
                    KeywordDefinitionInstance.Context = Context;
                    _commandMetadata = new CommandMetadata(KeywordDefinitionInstance);
                }
                return _commandMetadata;
            }
        }
        private CommandMetadata _commandMetadata;
    }

    #endregion /* KeywordInfo */
}

namespace System.Management.Automation
{
    #region Keyword Command Instance

    /// <summary>
    /// Specifies the semantic properties/actions that a
    /// DSL keyword must provide to the PowerShell runtime. This must
    /// be subclassed to instantiate a new PowerShell keyword
    /// </summary>
    public abstract class Keyword : InternalCommand
    {
        /// <summary>
        /// Create a fresh empty instance of a keyword
        /// </summary>
        protected Keyword()
        {
            PreParse = null;
            PostParse = null;
            SemanticCheck = null;
            CompilationStrategy = DefaultCompilationStrategy;
        }

        /// <summary>
        /// Defines the runtime information for the keyword
        /// </summary>
        public KeywordInfo KeywordInfo
        {
            get
            {
                var keywordInfo = (KeywordInfo)CommandInfo;
                keywordInfo.Context = Context;
                return (KeywordInfo)CommandInfo;
            }
            internal set
            {
                if (value != null)
                {
                    value.Context = Context;
                }
                CommandInfo = value;
            }
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
        /// Allows the specification of a new compilation algorithm, allowing the definition of
        /// arbitrary DynamicKeywordStatementAst semantics
        /// </summary>
        public Func<Compiler, ParameterExpression, DynamicKeywordStatementAst, Expression> CompilationStrategy
        {
            get;
        }

        /// <summary>
        /// Specifies the call to be made at runtime when the keyword is first executed (before it's children)
        /// </summary>
        public virtual object RuntimeEnterScope(IEnumerable<Tuple<Keyword, object>> parentKeywordSetups)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Specifies the call to be made at runtime by the keyword after its children have been executed
        /// </summary>
        public virtual object RuntimeLeaveScope(IEnumerable<Tuple<Keyword, object>> parentKeywordSetups, List<object> childResults)
        {
            throw new NotImplementedException();
        }

        internal object EnterScope()
        {
            if (Context == null)
            {
                throw PSTraceSource.NewArgumentNullException(nameof(this.Context));
            }
            return Context.EngineSessionState.CurrentScope.DynamicKeywordRuntime.EnterScope(this);
        }

        internal object LeaveScope()
        {
            return Context.EngineSessionState.CurrentScope.DynamicKeywordRuntime.LeaveScope();
        }

        private static Expression DefaultCompilationStrategy(Compiler compiler, ParameterExpression contextVariable, DynamicKeywordStatementAst kwAst)
        {
            var dllKeyword = kwAst.Keyword as DllDefinedDynamicKeyword;
            if (dllKeyword == null)
            {
                throw PSTraceSource.NewInvalidOperationException("Non-DLL-provided keyword '{0}' should not be compiled", kwAst.Keyword);
            }

            // If there is no RuntimeCall, do nothing
            if (!dllKeyword.KeywordInfo.HasEnterScopeCall && !dllKeyword.KeywordInfo.HasLeaveScopeCall)
            {
                return Expression.Empty();
            }

            // Get the keyword constructor by getting the C# compiler to reinterpret the delegate from earlier
            Expression keywordInstantiation = dllKeyword.KeywordInfo.CreateNewKeywordExpression;

            Expression invocationCall = null;
            switch (kwAst.Keyword.BodyMode)
            {
                case DynamicKeywordBodyMode.Command:
                    // Compile any arguments
                    var cmdArgs = new Expression[kwAst.CommandElements.Count - 1];
                    for (int i = 1; i < kwAst.CommandElements.Count; i++)
                    {
                        cmdArgs[i - 1] = compiler.VisitCommandElement(kwAst.CommandElements[i]);
                    }

                    Expression cmdArgsArray = Expression.NewArrayInit(typeof(CommandParameterInternal), cmdArgs);

                    // Compile the keyword invocation itself
                    invocationCall = Expression.Call(KeywordProcessorCache.CachedInvokeCommandKeyword, contextVariable, keywordInstantiation, cmdArgsArray);
                    break;

                case DynamicKeywordBodyMode.Hashtable:
                    // Compile any arguments
                    var hashtableArgs = new Expression[kwAst.CommandElements.Count - 2];
                    for (int i = 1; i < kwAst.CommandElements.Count - 1; i++)
                    {
                        hashtableArgs[i - 1] = compiler.VisitCommandElement(kwAst.CommandElements[i]);
                    }

                    Expression hashtableArgsArray = Expression.NewArrayInit(typeof(CommandParameterInternal), hashtableArgs);

                    // Compile the hashtable body
                    Expression body = compiler.Compile(kwAst.BodyExpression);

                    // Compile the keyword invocation, passing in the arguments and hashtable body
                    invocationCall = Expression.Call(KeywordProcessorCache.CachedInvokeHashtableKeyword, contextVariable, keywordInstantiation, hashtableArgsArray, body);
                    break;

                case DynamicKeywordBodyMode.ScriptBlock:
                    // Compile the argument elements
                    var scriptBlockArgs = new Expression[kwAst.CommandElements.Count - 2];
                    for (int i = 1; i < kwAst.CommandElements.Count - 1; i++)
                    {
                        scriptBlockArgs[i - 1] = compiler.VisitCommandElement(kwAst.CommandElements[i]);
                    }

                    Expression scriptBlockArgsArray = Expression.NewArrayInit(typeof(CommandParameterInternal), scriptBlockArgs);

                    // Compile the child scriptblock by ripping out the statements and creating a false statement block
                    // TODO: This will tacitly ignore any other usual script block features, so we should warn about that.
                    //       Currently, those features would be unlikely to present anything useful, to my knowledge
                    ScriptBlockExpressionAst bodyScriptBlock = kwAst.BodyExpression as ScriptBlockExpressionAst;
                    if (bodyScriptBlock == null)
                    {
                        throw PSTraceSource.NewArgumentOutOfRangeException(nameof(kwAst.BodyExpression), kwAst.BodyExpression);
                    }

                    var statementBody = new StatementBlockAst(bodyScriptBlock.Extent, Ast.CopyElements<StatementAst>(bodyScriptBlock.ScriptBlock.EndBlock.Statements), null);

                    Expression childBlock = compiler.Compile(statementBody);

                    // Compile the keyword invocation itself, passing in the arguments and the child block
                    Expression topInvocation = Expression.Call(KeywordProcessorCache.CachedEnterScriptBlockKeywordScope, contextVariable, keywordInstantiation, scriptBlockArgsArray);

                    invocationCall = Expression.Block(typeof(void), topInvocation, childBlock);
                    break;

                default:
                    throw PSTraceSource.NewArgumentOutOfRangeException(nameof(kwAst.Keyword.BodyMode), kwAst.Keyword.BodyMode);
            }

            // Compile arguments and subexpressions of the keyword
            if (invocationCall == null)
            {
                return Expression.Empty();
            }

            // Compile the stack pop after the keyword and child invocations
            Expression stackPop = Expression.Call(KeywordProcessorCache.CachedLeaveKeywordScope, contextVariable);

            if (DynamicKeyword.ContainsKeyword(kwAst.Keyword.Keyword))
            {
                return compiler.CallAddCurrentPipe(Expression.Block(typeof(object), invocationCall, stackPop));
            }

            return Expression.Block(typeof(object), invocationCall, stackPop);
        }

        internal CommandMetadata GetCommandMetadata()
        {
            return new CommandMetadata(this);
        }
    }

    #endregion /* Keyword Command Instance */

    #region DSL definition attributes

    /// <summary>
    /// Specifies that a class denotes a DSL Keyword
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class KeywordAttribute : Attribute
    {
        /// <summary>
        /// Construct a KeywordAttribute with default options set
        /// </summary>
        public KeywordAttribute()
        {
        }

        /// <summary>
        /// Specifies the body syntax expected after a keyword
        /// </summary>
        public DynamicKeywordBodyMode Body { get; set; } = DynamicKeywordBodyMode.Command;

        /// <summary>
        /// Specifies the number of times a keyword may be used
        /// in a scope/block
        /// </summary>
        public DynamicKeywordUseMode Use { get; set; } = DynamicKeywordUseMode.OptionalMany;

        /// <summary>
        /// Specifies the name of the default parameter set in this keyword.
        /// If none is specified, this will default to <see cref="KeywordParameterAttribute.AllParameterSets"/> 
        /// </summary>
        public string DefaultParameterSetName { get; set; }
    }

    /// <summary>
    /// Specifies a field denoting a keyword argument
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class KeywordParameterAttribute : Internal.ParsingBaseAttribute
    {
        /// <summary>
        /// ParameterSetName referring to all ParameterSets
        /// </summary>
        public const string AllParameterSets = "__AllParameterSets";

        /// <summary>
        /// The value specifying that a parameter is not positional
        /// </summary>
        public const int NotPositional = Int32.MinValue;

        /// <summary>
        /// The default setting for mandatory parameters (is *not* mandatory)
        /// </summary>
        public const bool MandatoryDefault = false;

        private string _parameterSetName = KeywordParameterAttribute.AllParameterSets;

        /// <summary>
        /// Constructs a KeywordParamterAttribute with default options set
        /// </summary>
        public KeywordParameterAttribute()
        {
        }

        /// <summary>
        /// Specifies whether an argument must be given. If this is false
        /// and Name is null or empty, this should be an error.
        /// </summary>
        public bool Mandatory { get; set; } = MandatoryDefault;

        /// <summary>
        /// Specifies what position a parameter occurs at if not passed by name.
        /// </summary>
        public int Position { get; set; } = NotPositional;

        /// <summary>
        /// Delineates what parameter set the attributed parameter belongs to
        /// </summary>
        public string ParameterSetName
        {
            get
            {
                return _parameterSetName;
            }

            set
            {
                if (String.IsNullOrEmpty(value))
                {
                    _parameterSetName = AllParameterSets;
                }
                else
                {
                    _parameterSetName = value;
                }
            }
        }

        /// <summary>
        /// This object as a Cmdlet ParameterAttribute. Inheriting from ParameterAttribute
        /// would allow users to set options that don't make sense, but changing the code inside
        /// the attribute reading functionality could also break things
        /// </summary>
        public ParameterAttribute AsParameterAttribute
        {
            get
            {
                if (_parameterAttribute == null)
                {
                    _parameterAttribute = new ParameterAttribute();

                    _parameterAttribute.Mandatory = Mandatory;
                    _parameterAttribute.ParameterSetName = ParameterSetName;
                    _parameterAttribute.Position = Position;

                    _parameterAttribute.DontShow = false;
                    _parameterAttribute.HelpMessage = "<TODO>";
                    _parameterAttribute.ValueFromPipeline = false;
                    _parameterAttribute.ValueFromPipelineByPropertyName = false;
                    _parameterAttribute.ValueFromRemainingArguments = false;
                }
                return _parameterAttribute;
            }
        }
        private ParameterAttribute _parameterAttribute;
    }

    /// <summary>
    /// Denotes a property for a keyword specification. Currently this
    /// would be a key in a hashmap body
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class KeywordPropertyAttribute : System.Management.Automation.Internal.ParsingBaseAttribute
    {
        /// <summary>
        /// Constructs a KeywordPropertyAttribute with the default options set
        /// </summary>
        public KeywordPropertyAttribute()
        {
        }

        /// <summary>
        /// Specifies whether a property must be given for the keyword
        /// </summary>
        public bool Mandatory { get; set; } = false;
    }

    #endregion /* DSL definition attributes */


    #region Runtime State

    internal class DynamicKeywordParameterBinderController : ParameterBinderController
    {
        public DynamicKeywordParameterBinderController(Keyword keyword, ParameterBinderBase parameterBinder)
            : base(keyword.MyInvocation, keyword.Context, parameterBinder)
        {
            Keyword = keyword;
            CommandMetadata = keyword.KeywordInfo.CommandMetadata;

            _bindableParameters = keyword.KeywordInfo.CommandMetadata.StaticCommandParameterMetadata;
            UnboundParameters = new List<MergedCompiledCommandParameter>(_bindableParameters?.BindableParameters?.Values);
        }

        internal CommandMetadata CommandMetadata { get; }

        internal Keyword Keyword { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        internal override Collection<CommandParameterInternal> BindParameters(Collection<CommandParameterInternal> parameters)
        {
            return BindParameters(uint.MaxValue, parameters);
        }

        internal Collection<CommandParameterInternal> BindParameters(uint parameterSets, Collection<CommandParameterInternal> arguments)
        {
            Collection<CommandParameterInternal> result = new Collection<CommandParameterInternal>();

            foreach (CommandParameterInternal argument in arguments)
            {
                if (!argument.ParameterNameSpecified)
                {
                    result.Add(argument);
                    continue;
                }

                // We don't want to throw an exception yet because
                // the parameter might be a positional argument or it
                // might match up to a dynamic parameter
                MergedCompiledCommandParameter parameter =
                    BindableParameters.GetMatchingParameter(
                        argument.ParameterName,
                        false, true,
                        new InvocationInfo(this.InvocationInfo.MyCommand, argument.ParameterExtent));

                // If the parameter is not in the specified parameter set,
                // throw a binding exception
                if (parameter != null)
                {
                    // Now check to make sure it hasn't already been
                    // bound by looking in the boundParameters collection

                    if (BoundParameters.ContainsKey(parameter.Parameter.Name))
                    {
                        ParameterBindingException bindingException =
                            new ParameterBindingException(
                                ErrorCategory.InvalidArgument,
                                this.InvocationInfo,
                                GetParameterErrorExtent(argument),
                                argument.ParameterName,
                                null,
                                null,
                                ParameterBinderStrings.ParameterAlreadyBound,
                                "ParameterAlreadyBound");

                        // Multiple values assigned to the same parameter.
                        // Not caused by default parameter binding
                        throw bindingException;
                    }

                    if ((parameter.Parameter.ParameterSetFlags & parameterSets) == 0 &&
                        !parameter.Parameter.IsInAllSets)
                    {
                        string parameterSetName = BindableParameters.GetParameterSetName(parameterSets);

                        ParameterBindingException bindingException =
                            new ParameterBindingException(
                                ErrorCategory.InvalidArgument,
                                Keyword.MyInvocation,
                                null,
                                argument.ParameterName,
                                null,
                                null,
                                ParameterBinderStrings.ParameterNotInParameterSet,
                                "ParameterNotInParameterSet",
                                parameterSetName);

                        // Might be caused by default parameter binding
                        if (!DefaultParameterBindingInUse)
                        {
                            throw bindingException;
                        }
                        else
                        {
                            ThrowElaboratedBindingException(bindingException);
                        }
                    }

                    try
                    {
                        BindParameter(parameterSets, argument, parameter,
                            ParameterBindingFlags.ShouldCoerceType | ParameterBindingFlags.DelayBindScriptBlock);
                    }
                    catch (ParameterBindingException pbex)
                    {
                        if (!DefaultParameterBindingInUse)
                        {
                            throw;
                        }

                        ThrowElaboratedBindingException(pbex);
                    }
                }
                else
                {
                    result.Add(argument);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Defines a runtime which tracks the state of a DynamicKeyword execution block
    /// </summary>
    public class DynamicKeywordRuntimeContext
    {
        private Stack<Tuple<Keyword, object>> _keywordScopeStack;
        private Stack<List<object>> _keywordResultStack;

        /// <summary>
        /// Default constructor
        /// </summary>
        public DynamicKeywordRuntimeContext()
        {
            _keywordScopeStack = new Stack<Tuple<Keyword, object>>();
            _keywordResultStack = new Stack<List<object>>();
        }

        /// <summary>
        /// Enter the scope of a dynamic keyword invocation by running the scope entry call.
        /// The expectation is that the compiler will put the calls of children's scope entries after this one
        /// </summary>
        /// <param name="keyword">the dynamic keyword to enter the scope of</param>
        public object EnterScope(Keyword keyword)
        {
            object entryResult = null;
            if (keyword.KeywordInfo.HasEnterScopeCall)
            {
                entryResult = keyword.RuntimeEnterScope(_keywordScopeStack);
            }
            _keywordResultStack.Push(new List<object>());
            _keywordScopeStack.Push(new Tuple<Keyword, object>(keyword, entryResult));
            return entryResult;
        }

        /// <summary>
        /// Leave the scope of a runtime keyword, executing the scope exit call (which processes the results
        /// of child keywords). It is expected the compiler will call this after EnterScope(keyword) for any given keyword.
        /// </summary>
        /// <returns>the result of the scope exit call of the keyword we are leaving the scope of</returns>
        public object LeaveScope()
        {
            Keyword keyword = _keywordScopeStack.Pop().Item1;
            List<object> childResults = _keywordResultStack.Pop();
            object result = null;
            if (keyword.KeywordInfo.HasLeaveScopeCall)
            {
                result = keyword.RuntimeLeaveScope(_keywordScopeStack, childResults);
            }
            if (_keywordResultStack.Count > 0)
            {
                _keywordResultStack.Peek().Add(result);
            }
            return result;
        }
    }

    /// <summary>
    /// Static class to hold cached reflection-resolved functions for the compiler to call with DynamicKeywords
    /// </summary>
    internal static class KeywordProcessorCache
    {
        private const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic;

        public static readonly MethodInfo CachedInvokeCommandKeyword;
        public static readonly MethodInfo CachedInvokeHashtableKeyword;
        public static readonly MethodInfo CachedEnterScriptBlockKeywordScope;

        public static readonly MethodInfo CachedLeaveKeywordScope;

        static KeywordProcessorCache()
        {
            CachedInvokeCommandKeyword = typeof(KeywordProcessorCache).GetMethod(nameof(InvokeCommandKeyword), staticFlags);
            CachedInvokeHashtableKeyword = typeof(KeywordProcessorCache).GetMethod(nameof(InvokeHashtableKeyword), staticFlags);
            CachedEnterScriptBlockKeywordScope = typeof(KeywordProcessorCache).GetMethod(nameof(EnterScriptBlockKeywordScope), staticFlags);

            CachedLeaveKeywordScope = typeof(KeywordProcessorCache).GetMethod(nameof(LeaveKeywordScope), staticFlags);
        }

        /// <summary>
        /// Execute a command keyword by trying to instantiate the keyword object with the parameters given
        /// and executing the runtime call provided. Returns the result of the keyword execution.
        /// </summary>
        /// <param name="context">the execution context the keyword runs in, provided for keyword state passing</param>
        /// <param name="keyword">a fresh instantiation of the keyword</param>
        /// <param name="parameters">the parameters passed to the keyword</param>
        /// <returns></returns>
        private static object InvokeCommandKeyword(ExecutionContext context, Keyword keyword, CommandParameterInternal[] parameters)
        {
            keyword.Context = context;
            BindKeywordParameters(keyword, parameters);
            return keyword.EnterScope();
        }

        /// <summary>
        /// Execute a hashtable-bodied dynamic keyword with the parameters and body given and return the result
        /// </summary>
        /// <param name="context"></param>
        /// <param name="keyword"></param>
        /// <param name="parameters"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        private static object InvokeHashtableKeyword(ExecutionContext context, Keyword keyword, CommandParameterInternal[] parameters, Hashtable body)
        {
            keyword.Context = context;
            BindKeywordParameters(keyword, parameters);
            AssignHashtableProperties(keyword, body);
            return keyword.EnterScope();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="keyword"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private static object EnterScriptBlockKeywordScope(ExecutionContext context, Keyword keyword, CommandParameterInternal[] parameters)
        {
            keyword.Context = context;
            BindKeywordParameters(keyword, parameters);
            return keyword.EnterScope();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        private static object LeaveKeywordScope(ExecutionContext context)
        {
            return context.EngineSessionState.CurrentScope.DynamicKeywordRuntime.LeaveScope();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keyword"></param>
        /// <param name="parameters"></param>
        private static void BindKeywordParameters(Keyword keyword, CommandParameterInternal[] parameters)
        {
            // TODO: Cache all this at parse time and pass it in when compiled

            var parameterBinder = new ReflectionParameterBinder(keyword, keyword);
            var parameterBinderController = new DynamicKeywordParameterBinderController(keyword, parameterBinder);
            parameterBinderController.BindParameters(new Collection<CommandParameterInternal>(parameters));

            /*
            var parameterSets = keyword.KeywordInfo.KeywordData.DllKeywordParameters;

            // Set to whittle down the possible sets we're in
            var possibleParameterSets = new HashSet<string>(parameterSets.Keys);
            // Lookup table for positions, to know what parameter we have based on parameter set
            var possiblePositionalParameters = new Dictionary<int, Dictionary<string, string>>();
            // Lookup table to store what parameters occur in what sets
            var paramToSet = new Dictionary<string, HashSet<string>>();
            foreach (KeyValuePair<string, Dictionary<string, DynamicKeywordParameter>> parameterSet in parameterSets)
            {
                foreach (var param in parameterSet.Value.Values)
                {
                    if (param.Position != KeywordParameterAttribute.NotPositional)
                    {
                        if (possiblePositionalParameters.ContainsKey(param.Position))
                        {
                            possiblePositionalParameters[param.Position].Add(param.ParameterSet, param.Name);
                        }
                        else
                        {
                            possiblePositionalParameters.Add(param.Position, new Dictionary<string, string> { { param.ParameterSet, param.Name } });
                        }
                    }

                    if (paramToSet.ContainsKey(param.Name))
                    {
                        paramToSet[param.Name].Add(parameterSet.Key);
                    }
                    else
                    {
                        paramToSet.Add(param.Name, new HashSet<string> { parameterSet.Key });
                    }
                }
            }

            bool expectingArgument = false;
            string parameterName = null;
            object parameterValue = null;
            int position = 0;
            var seenPositions = new HashSet<int>();
            for (int i = 0; i < parameters.Length; i++)
            {
                // If we saw a positional parameter (by name or by position),
                // we pretend it was invoked by position when we get to it
                if (seenPositions.Contains(position))
                {
                    position++;
                }

                CommandParameterInternal currParam = parameters[i];

                // Deal with argument from last pass
                if (expectingArgument)
                {
                    expectingArgument = false;
                    if (currParam.ParameterNameSpecified)
                    {
                        throw new RuntimeException("Parameter given when argument expected: " + currParam.ParameterText);
                    }

                    if (currParam.ArgumentSpecified)
                    {
                        parameterValue = currParam.ArgumentValue;
                    }
                }

                if (currParam.ParameterNameSpecified)
                {
                    PropertyInfo parameter;
                    if (currParam.ArgumentSpecified)
                    {
                        parameterName = currParam.ParameterName;
                        parameterValue = currParam.ArgumentValue;

                        if (parameterSets.ContainsKey(parameterName))
                        {
                            // Treat named parameters as if they had been specified in the correct position
                            int pos = parameterSets[parameterName].GetCustomAttribute<KeywordParameterAttribute>().Position;
                            if (pos != KeywordParameterAttribute.NotPositional)
                            {
                                seenPositions.Add(pos);
                            }
                        }
                    }
                    // Test for switch parameters
                    else if (parameterSets.TryGetValue(currParam.ParameterName, out parameter) && parameter.PropertyType == typeof(SwitchParameter))
                    {
                        parameterName = currParam.ParameterName;
                        parameterValue = SwitchParameter.Present;
                    }
                    else
                    {
                        expectingArgument = true;
                        continue;
                    }
                }
                // Try to parse as a positional parameter
                else if (currParam.ArgumentSpecified)
                {
                    PropertyInfo parameter;
                    if (positionalParameters.TryGetValue(position, out parameter))
                    {
                        parameterName = parameter.Name;
                        parameterValue = currParam.ArgumentValue;
                        seenPositions.Add(position);
                    }
                    else
                    {
                        throw new RuntimeException("Bad unnamed parameter: " + currParam.ParameterText);
                    }
                }
                // The parameter name and value is now resolved

                // Ensure the parameter exists on the keyword
                if (!parameterSets.ContainsKey(parameterName))
                {
                    var msg = String.Format("Unknown parameter: '-{0}: {1}'", parameterName, parameterValue);
                    throw new RuntimeException(msg);
                }

                // Ensure the type is correct
                PropertyInfo parameterInfo = parameterSets[parameterName];
                if (!parameterInfo.PropertyType.IsAssignableFrom(parameterValue.GetType()))
                {
                    var msg = String.Format("Bad parameter type: '-{0}: {1}'", parameterName, parameterValue);
                    throw new RuntimeException(msg);
                }

                parameterInfo.SetValue(keyword, parameterValue);
            }
            */
        }

        /// <summary>
        /// Take the key value pairs in a PowerShell hashtable block and try to assign them as properties
        /// on the keyword
        /// </summary>
        /// <param name="keyword">the keyword instance to assign the properties to</param>
        /// <param name="hashtable">the hashtable specifying the key/value pairs to assign as properties</param>
        private static void AssignHashtableProperties(Keyword keyword, Hashtable hashtable)
        {
            TypeInfo keywordTypeInfo = keyword.GetType().GetTypeInfo();
            ImmutableDictionary<string, PropertyInfo> keywordProperties = keywordTypeInfo.DeclaredProperties
                .Where(p => p.GetCustomAttribute<KeywordPropertyAttribute>() != null)
                .ToImmutableDictionary(p => p.Name, p => p);

            foreach (KeyValuePair<object, object> hashtableEntry in hashtable)
            {
                string keywordPropertyName = hashtableEntry.Key as string;
                if (keywordPropertyName == null)
                {
                    throw new RuntimeException("Hashtable-bodied DynamicKeywords must have string-typed keys");
                }

                if (!keywordProperties.ContainsKey(keywordPropertyName))
                {
                    var msg = String.Format("Keyword '{0}' has no property '{1}'", keyword.GetType(), keywordPropertyName);
                    throw new RuntimeException(msg);
                }

                PropertyInfo keywordProperty = keywordProperties[keywordPropertyName];
                if (!keywordProperty.PropertyType.IsAssignableFrom(hashtableEntry.Value.GetType()))
                {
                    var msg = String.Format("The property '{0}' of the keyword '{1}' has type '{2}' and is not assignable by '{3}' (type '{4}')",
                        keywordPropertyName, keyword.GetType(), keywordProperty.PropertyType, hashtableEntry.Value, hashtableEntry.Value.GetType());
                }

                keywordProperty.SetValue(keyword, hashtableEntry.Value);
            }
        }
    }

    #endregion /* Runtime State */
}

namespace System.Management.Automation.Language
{
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
        public IDictionary<string, DllDefinedDynamicKeyword> ReadDslSpecification()
        {
            // TODO: Ensure the module is a DLL, else return null

            IDictionary<string, DllDefinedDynamicKeyword> globalKeywords;

            // Read the file metadata to load the parse-time keyword information
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

            // TODO: Move this elsewhere later
            // Load in the keyword type information
            (new DynamicKeywordLoader(globalKeywords.Values)).Load();

            return globalKeywords;
        }

        /// <summary>
        /// Provides a reader to render CIL type metadata into Type objects, provided those types are already loaded
        /// </summary>
        private TypingTypeProvider TypeLookupProvider
        {
            get
            {
                return s_typeLookupProvider ??
                    (s_typeLookupProvider = new TypingTypeProvider());
            }
        }
        private static TypingTypeProvider s_typeLookupProvider;

        /// <summary>
        /// Provides a reader to render CIL type metadata as strings
        /// </summary>
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
        private IDictionary<string, DllDefinedDynamicKeyword> ReadGlobalDynamicKeywords()
        {
            var globalKeywords = new Dictionary<string, DllDefinedDynamicKeyword>();

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
                    DllDefinedDynamicKeyword keyword = ReadKeywordSpecification(typeDef);
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
        /// Read in any enum definitions at the current type definition level -- this will provide completion at
        /// parse time without having to load the assembly
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
        /// <param name="isNested">whether the keyword is nested inside another keyword or not</param>
        /// <returns>the constructed DynamicKeyword from the parsed specification</returns>
        private DllDefinedDynamicKeyword ReadKeywordSpecification(TypeDefinition typeDef, bool isNested = false)
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
            string defaultParameterSetName = KeywordParameterAttribute.AllParameterSets;
            foreach (var keywordAttributeHandle in typeDef.GetCustomAttributes())
            {
                var keywordAttribute = _metadataReader.GetCustomAttribute(keywordAttributeHandle);
                if (IsKeywordAttribute(keywordAttribute))
                {
                    SetKeywordAttributeParameters(keywordAttribute, out bodyMode, out useMode, out defaultParameterSetName);
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
            var keywordParameters = new List<DllKeywordParameter>();
            var keywordProperties = new List<DynamicKeywordProperty>();
            var parameterPositions = new Dictionary<string, HashSet<int>> { { defaultParameterSetName, new HashSet<int>() } };
            foreach (var propertyHandle in typeDef.GetProperties())
            {
                var property = _metadataReader.GetPropertyDefinition(propertyHandle);
                var parameterSetSpecificProperties = new Dictionary<string, DynamicKeywordSetSpecificProperties>();
                foreach (var attributeHandle in property.GetCustomAttributes())
                {
                    var keywordMemberAttribute = _metadataReader.GetCustomAttribute(attributeHandle);
                    // Collect all parameter attributes
                    if (IsKeywordParameterAttribute(keywordMemberAttribute))
                    {
                        DynamicKeywordSetSpecificProperties paramSetProperties = ReadSetSpecificProperties(defaultParameterSetName, keywordMemberAttribute);
                        parameterSetSpecificProperties.Add(paramSetProperties.SetName, paramSetProperties);

                        if (paramSetProperties.Position != KeywordParameterAttribute.NotPositional)
                        {
                            if (parameterPositions[paramSetProperties.SetName].Contains(paramSetProperties.Position))
                            {
                                throw PSTraceSource.NewInvalidOperationException("Cannot give two parameters the same position");
                            }
                            else
                            {
                                parameterPositions[paramSetProperties.SetName].Add(paramSetProperties.Position);
                            }
                        }
                        break;
                    }
                    // Otherwise look for property attributes
                    else if (IsKeywordPropertyAttribute(keywordMemberAttribute))
                    {
                        keywordProperties.Add(ReadPropertySpecification(genericContext, property, keywordMemberAttribute));
                        break;
                    }
                }
                if (parameterSetSpecificProperties.Count > 0)
                {
                    keywordParameters.Add(ReadParameterSpecification(genericContext, parameterSetSpecificProperties.Values, property));
                }
            }

            // Read in all nested keywords below this one
            _keywordDefinitionStack.Push(new HashSet<string>());
            List<DllDefinedDynamicKeyword> innerKeywords = new List<DllDefinedDynamicKeyword>();
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
                    DllDefinedDynamicKeyword innerKeyword = ReadKeywordSpecification(innerTypeDef, isNested: true);
                    innerKeyword.IsNested = true;
                    innerKeywords.Add(innerKeyword);
                }
            }

            _keywordDefinitionStack.Pop();
            _enumDefStack.Pop();

            return new DllDefinedDynamicKeyword(keywordName, _moduleInfo, bodyMode, useMode, defaultParameterSetName, innerKeywords, keywordParameters, keywordProperties, isNested);
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
        /// <param name="setSpecificProperties">the attribute on the property declaring the parameter's set specific properties (position, mandatory)</param>
        /// <param name="genericContext">the generic type context in which the property is used</param>
        /// <returns></returns>
        private DllKeywordParameter ReadParameterSpecification(TypeNameGenericContext genericContext,
            IEnumerable<DynamicKeywordSetSpecificProperties> setSpecificProperties, PropertyDefinition property)
        {
            string parameterName = _metadataReader.GetString(property.Name);
            string parameterType = property.DecodeSignature(TypeNameProvider, genericContext).ReturnType;

            IEnumerable<string> typeValues = TryGetEnumValues(parameterType);

            var dkParameter = new DllKeywordParameter(parameterName, parameterType, typeValues ?? new string[0], new string[0], setSpecificProperties);
            return dkParameter;
        }

        private DynamicKeywordSetSpecificProperties ReadSetSpecificProperties(string defaultParameterSetName, CustomAttribute keywordParameterAttribute)
        {
            int position = KeywordParameterAttribute.NotPositional;
            bool mandatory = KeywordParameterAttribute.MandatoryDefault;
            string parameterSet = defaultParameterSetName;
            foreach (var attributeProperty in keywordParameterAttribute.DecodeValue(TypeLookupProvider).NamedArguments)
            {
                switch (attributeProperty.Name)
                {
                    case nameof(KeywordParameterAttribute.Position):
                        position = (int)attributeProperty.Value;
                        break;
                    case nameof(KeywordParameterAttribute.Mandatory):
                        mandatory = (bool)attributeProperty.Value;
                        break;
                    case nameof(KeywordParameterAttribute.ParameterSetName):
                        parameterSet = (string)attributeProperty.Value;
                        break;
                }
            }
            return new DynamicKeywordSetSpecificProperties(parameterSet, position, mandatory);
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

        private IEnumerable<string> TryGetEnumValues(string typeName)
        {
            foreach (var enumScope in _enumDefStack)
            {
                if (enumScope.ContainsKey(typeName))
                {
                    return enumScope[typeName];
                }
            }
            return null;
        }

        private bool TrySetMemberEnumType(DynamicKeywordProperty keywordProperty)
        {
            IEnumerable<string> values = TryGetEnumValues(keywordProperty.TypeConstraint);
            if (values == null)
            {
                return false;
            }
            keywordProperty.Values.AddRange(values);
            return true;
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

        private void SetKeywordAttributeParameters(CustomAttribute keywordAttribute, out DynamicKeywordBodyMode bodyMode, out DynamicKeywordUseMode useMode, out string defaultParamSet)
        {
            var keywordValue = keywordAttribute.DecodeValue(TypeLookupProvider);
            bodyMode = DynamicKeywordBodyMode.Command;
            useMode = DynamicKeywordUseMode.OptionalMany;
            defaultParamSet = KeywordParameterAttribute.AllParameterSets;

            foreach (var attributeArgument in keywordValue.NamedArguments)
            {
                switch (attributeArgument.Name)
                {
                    case nameof(KeywordAttribute.Body):
                        bodyMode = (DynamicKeywordBodyMode)attributeArgument.Value;
                        break;
                    case nameof(KeywordAttribute.Use):
                        useMode = (DynamicKeywordUseMode)attributeArgument.Value;
                        break;
                    case nameof(KeywordAttribute.DefaultParameterSetName):
                        defaultParamSet = (string)attributeArgument.Value;
                        break;
                }
            }
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

    #region Keyword Reflection Type Loading

    /// <summary>
    /// Takes the information stored in DynamicKeyword objects and loads the
    /// type information in accordingly
    /// </summary>
    internal class DynamicKeywordLoader
    {

        private static void AssignKeywordInfo(KeyValuePair<DllDefinedDynamicKeyword, KeywordInfo> keywordAssignPair)
        {
            keywordAssignPair.Key.KeywordInfo = keywordAssignPair.Value;
        }

        private readonly ImmutableList<DllDefinedDynamicKeyword> _topLevelKeywordsToLoad;

        private ImmutableDictionary<DllDefinedDynamicKeyword, Assembly> _keywordAssemblies;

        public DynamicKeywordLoader(IEnumerable<DllDefinedDynamicKeyword> keywordsToLoad)
        {
            _topLevelKeywordsToLoad = keywordsToLoad.ToImmutableList();
        }
        
        /// <summary>
        /// Perform the keyword loading operation
        /// </summary>
        public void Load()
        {
            if (_topLevelKeywordsToLoad.Count < 1)
            {
                return;
            }
            LoadModules();
            LoadKeywords();
        }

        /// <summary>
        /// Load the assemblies for the keywords to load
        /// </summary>
        private void LoadModules()
        {
            var loadedModules = new Dictionary<PSModuleInfo, Assembly>();
            var keywordAssemblies = new Dictionary<DllDefinedDynamicKeyword, Assembly>();
            foreach (var keyword in _topLevelKeywordsToLoad)
            {
                if (!loadedModules.ContainsKey(keyword.ImplementingModuleInfo))
                {
                    loadedModules.Add(keyword.ImplementingModuleInfo, ClrFacade.LoadFrom(keyword.ImplementingModuleInfo.Path));
                }

                keywordAssemblies.Add(keyword, loadedModules[keyword.ImplementingModuleInfo]);
            }
            _keywordAssemblies = keywordAssemblies.ToImmutableDictionary();
        }

        /// <summary>
        /// Load the keyword runtime logic in. This must be performed after executing LoadModules()
        /// </summary>
        private void LoadKeywords()
        {
            var keywordDefinitions = new List<Node<KeyValuePair<DllDefinedDynamicKeyword, KeywordInfo>>>();
            foreach(var keyword in _topLevelKeywordsToLoad)
            {
                Type definingType = _keywordAssemblies[keyword].GetType(keyword.Keyword);
                if (definingType == null)
                {
                    throw PSTraceSource.NewInvalidOperationException("Keyword '{0}' not defined in assembly loaded from '{1}", keyword, keyword.ImplementingModuleInfo.Path);
                }
                keywordDefinitions.Add(ReadKeywordInfo(keyword, definingType));
            }

            foreach (var keywordDefinition in keywordDefinitions)
            {
                keywordDefinition.Process(AssignKeywordInfo);
            }
        }

        /// <summary>
        /// Read the information on a single keyword (and its children, recursively). This returns an immutable data structure
        /// for later loading, so that errors will not lead to partial addition of runtime data
        /// </summary>
        /// <param name="keyword">the data holder for the dynamic keyword to load</param>
        /// <param name="definingType">the loaded type representing the dynamic keyword</param>
        /// <returns></returns>
        private Node<KeyValuePair<DllDefinedDynamicKeyword, KeywordInfo>> ReadKeywordInfo(DllDefinedDynamicKeyword keyword, Type definingType)
        {
            var children = new List<Node<KeyValuePair<DllDefinedDynamicKeyword, KeywordInfo>>>();
            foreach (var innerKeyword in keyword.InnerKeywords.Values)
            {
                Type nestedType = definingType.GetNestedType(innerKeyword.Keyword, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nestedType == null)
                {
                    throw PSTraceSource.NewInvalidOperationException("Inner keyword '{0}' is not defined within the type '{1}'", innerKeyword, definingType);
                }
                children.Add(ReadKeywordInfo((DllDefinedDynamicKeyword)innerKeyword, nestedType));
            }
            // TODO: Define what the definition string is for a given keyword
            KeywordInfo keywordInfo = new KeywordInfo(keyword, definingType, "<TODO>");

            return new Node<KeyValuePair<DllDefinedDynamicKeyword, KeywordInfo>>(new KeyValuePair<DllDefinedDynamicKeyword, KeywordInfo>(keyword, keywordInfo), children);
        }

        private class Node<T>
        {
            private ImmutableList<Node<T>> _children;
            private T _value;

            public Node(T value, IEnumerable<Node<T>> children)
            {
                _value = value;
                var acc = new List<Node<T>>();
                foreach (var child in children)
                {
                    acc.Add(child);
                }
                _children = acc.ToImmutableList();
            }

            public void Process(Action<T> p)
            {
                foreach (var child in _children)
                {
                    child.Process(p);
                }
                p(_value);
            }
        }
    }

    #endregion /* Keyword Reflection Type Loading */
}