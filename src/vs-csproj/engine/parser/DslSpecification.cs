/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

namespace System.Management.Automation.Language
{
    /// <summary>
    /// An interface specifying the semantic properties/actions that a
    /// DSL keyword must provide to the PowerShell runtime. All keyword
    /// additions to the language must implement this.
    /// </summary>
    interface IPSDslKeyword
    {
        /// <summary>
        /// The pre-parse action of the keyword. Implemented
        /// as a Func property to allow nullability.
        /// </summary>
        Func<DynamicKeyword, ParseError[]> PreParse
        {
            get;
        }

        /// <summary>
        /// The post-parse action of the keyword. Implemented
        /// as a Func property to allow nullability.
        /// </summary>
        Func<DynamicKeywordStatementAst, ParseError[]> PostParse
        {
            get;
        }

        /// <summary>
        /// The semantic checking operation to make at parse time for
        /// a DSL keyword. Implemented as a Func property so as to be
        /// nullable.
        /// </summary>
        Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
        {
            get;
        }
    }

    #region DSL definition attributes

    /// <summary>
    /// Specifies a naming requirement for a keyword when used.
    /// The default is no name.
    /// </summary>
    public enum KeywordNameMode
    {
        NoName,   // No name should be given when calling this keyword
        Required, // A name must be given
        Optional, // A name may be given
    }

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
    /// Defaults to Required.
    /// </summary>
    public enum KeywordUseMode
    {
        Required,     // Must be used exactly once
        RequiredMany, // Must be used at least once
        Optional,     // May be used 0-1 times
        OptionalMany, // May be used any number of times
    }

    /// <summary>
    /// Defines a class as a definition block for a PowerShell DSL
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PSDslAttribute : System.Attribute {}

    /// <summary>
    /// Specifies that a class denotes a DSL Keyword
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PSKeywordAttribute : System.Attribute
    {
        private KeywordNameMode nameMode;
        private KeywordBodyMode bodyMode;
        private KeywordUseMode useMode;

        public PSKeywordAttribute()
        {
            this.nameMode = KeywordNameMode.NoName;
            this.bodyMode = KeywordBodyMode.Command;
            this.useMode = KeywordUseMode.Required;
        }

        public virtual KeywordNameMode Name
        {
            get { return nameMode; }
            set { nameMode = value; }
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
    [AttributeUsage(AttributeTargets.Field)]
    public class PSKeywordArgumentAttribute : System.Attribute
    {
        private string name;
        private bool mandatory;

        public PSKeywordArgumentAttribute()
        {
            this.name = null;
            this.mandatory = true;
        }

        /// <summary>
        /// If this is not null or empty, sets the parameter name for the argument,
        /// allowing it to be passed as a named argument.
        /// </summary>
        public string Name
        {
            get { return name; }
            set { name = value; }
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
    }

    #endregion /* DSL definition attributes */
}