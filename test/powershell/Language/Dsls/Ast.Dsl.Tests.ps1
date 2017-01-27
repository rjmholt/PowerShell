<#
Tests DynamicKeyword addition to AST structure
#>
Import-Module $PSScriptRoot\DslTestSupport.psm1

# Get the first instance of a DynamicKeyword of a given name within some AST
$GetKeywordByName = "Get-KeywordByName"
$GetKeywordByNameDef = @"
function $GetKeywordByName
{
    param(`$Ast, `$Name)
    `$Ast.Find({
        `$args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst] -and
        `$args[0].Keyword.Keyword -eq `$Name
    }, `$true)
}
"@

# Get nested keyword instances
$GetInnerKeyword = "Get-InnerKeyword"
$GetInnerKeywordDef = @"
$GetKeywordByNameDef

function $GetInnerKeyword
{
    param(`$Ast, `$NamePath)
    `$curr = `$Ast
    foreach (`$name in `$NamePath)
    {
        if (`$curr -eq `$null)
        {
            return `$null
        }
        `$curr = Get-KeywordByName -Ast `$curr -Name `$name
    }
    `$curr
}
"@

# Get a bundle of keywords to maximize use of costly process creation
$GetKeywordSet = "Get-KeywordSet"
$GetKeywordSetDef = @"
$GetInnerKeywordDef

function $GetKeywordSet
{
    param(`$Ast, `$NamePaths)
    `$acc = @{}
    foreach (`$namePath in `$NamePaths)
    {
        `$acc += @{`$namePath[-1] = Get-InnerKeyword -Ast `$Ast -NamePath `$namePath}
    }
    `$acc
}
"@

# Create a string to evaluate an expression for an AST in a new context
function Get-AstInvocationExpression
{
    param($TestDrive, $Modules, $Prelude, $Expression, $AstManipulator)

    # Put `using module $moduleName` on new lines
    $astGetter = @"
`$ast = [scriptblock]::Create('$Expression').Ast
"@

    $accumulator = "$AstManipulator -Ast `$ast"

    $expr = $astGetter,$accumulator -join "`n"

    Get-ExpressionFromModuleInNewContext -TestDrive $TestDrive -ModuleNames $Modules -Prelude $Prelude -Expression $expr
}

function Get-SerializedObjectChild
{
    param($SerializedObject, $PathToChild)

    $curr = $SerializedObject
    foreach($node in $PathToChild)
    {
        if ($curr -eq $null)
        {
            return $null
        }
        $curr = $curr.$node
    }
    $curr
}

Describe "Basic DSL syntax loading into AST" -Tags "CI" {
    BeforeAll {
        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        $moduleName = "BasicDsl"
        $astGetter = "$GetKeywordByName -Name $moduleName"
        $ast = Get-AstInvocationExpression -TestDrive $TestDrive -Modules $moduleName -Prelude $GetKeywordByNameDef -Expression "$moduleName" -AstManipulator $astGetter
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "imports a minimal C# defined DSL into the AST" {
        $ast.Keyword.Keyword | Should Be $moduleName
    }
}

Describe "More complex nested keyword structure loading into AST" -Tags "CI" {
    $testCases = @(
        @{ keywordToFind = "NestedKeyword1"; pathToKeyword = @() },
        @{ keywordToFind = "NestedKeyword2"; pathToKeyword = @() },
        @{ keywordToFind = "NestedKeyword1_1"; pathToKeyword = @("NestedKeyword1") },
        @{ keywordToFind = "NestedKeyword1_2"; pathToKeyword = @("NestedKeyword1") },
        @{ keywordToFind = "NestedKeyword2_1"; pathToKeyword = @("NestedKeyword2") },
        @{ keywordToFind = "NestedKeyword2_2"; pathToKeyword = @("NestedKeyword2") },
        @{ keywordToFind = "NestedKeyword1_1_1"; pathToKeyword = @("NestedKeyword1", "NestedKeyword1_1") },
        @{ keywordToFind = "NestedKeyword2_2_1"; pathToKeyword = @("NestedKeyword2", "NestedKeyword2_2") },
        @{ keywordToFind = "NestedKeyword2_2_1_1"; pathToKeyword = @("NestedKeyword2", "NestedKeyword2_2", "NestedKeyword2_2_1") }
    )

    BeforeAll {
        $dslName = "NestedDsl"

        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        $expr = @"
$dslName
{
    NestedKeyword1
    {
        NestedKeyword1_1
        {
            NestedKeyword1_1_1
        }

        NestedKeyword1_2
    }

    NestedKeyword2
    {
        NestedKeyword2_1

        NestedKeyword2_2
        {
            NestedKeyword2_2_1
            {
                NestedKeyword2_2_1_1
            }
        }
    }
}
"@

        # Rebuild a nested array expression from what we have
        $paths = "@(" + (($testCases | ForEach-Object { "@(" + (($_.pathToKeyword + $_.keywordToFind) -join ",") + ")" }) -join ",") + ")"

        $astGetter = "$GetKeywordSet -NamePaths $paths"

        $astDict = Get-AstInvocationExpression -TestDrive $TestDrive -Modules $dslName -Prelude $GetKeywordSetDef -Expression $expr -AstManipulator $astGetter
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "contains <keywordToFind> under the top level keyword" -TestCases $testCases {
        param($keywordToFind, $pathToKeyword)

        $astDict.$keywordToFind.Keyword.Keyword | Should Be $keywordToFind
    }
}

Describe "DSL Body most AST parsing" {
    $testCases = @(
        @{ keyword = "HashtableBodyKeyword"; script = "{0} {{ x = 1 }}"; bodyType = "Hashtable" },
        @{ keyword = "ScriptBlockBodyKeyword"; script = "{0} {{ foo }}"; bodyType = "ScriptBlockiExpression" }
    )

    BeforeAll {
        $dslName = "BodyModeDsl"

        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

<#
    It "has a body AST of type <bodyType> as a child AST" -TestCases $testCases {
        param($keyword, $bodyType)

        $ast = [scriptblock]::Create($script -f $keyword).Ast

        $kwStmt = Get-KeywordByName -Ast $ast -Name $keyword

        $kwStmt.BodyExpression | Should BeOfType $("[System.Management.Automation.Language.$($bodyType)Ast]")
    }

    It "does not parse scriptblock as body" {
        $ast = [scriptblock]::Create("CommandBodyKeyword { foo }").Ast

        $kwStmt = Get-KeywordByName -Ast $ast -Name "CommandBodyKeyword"

        $kwStmt.BodyExpression | Should Be $null
    }
    #>
}

Describe "Full DSL example loads into AST" -Tags "CI" {
    # TODO: Test AST of full DSL here
}
