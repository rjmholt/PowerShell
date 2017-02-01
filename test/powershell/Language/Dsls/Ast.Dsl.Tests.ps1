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

        $keyword = Get-ScriptBlockResultInNewProcess -TestDrive $TestDrive -ModuleNames "BasicDsl" -Command {
            $null = [scriptblock]::Create("using module BasicDsl").Invoke()

            [scriptblock]::Create("BasicDsl").Ast.Find({
                $args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst]
            }, $true)
        }
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "imports a minimal C# defined DSL into the AST" {
        $keyword.Keyword.Keyword | Should Be "BasicDsl"
    }
}

Describe "More complex nested keyword structure loading into AST" -Tags "CI" {
    BeforeAll {
        $dslName = "NestedDsl"

        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        $testCases = @(
            @{ child = "NestedKeyword1"; parent = "NestedKeyword" },
            @{ child = "NestedKeyword2"; parent = "NestedKeyword" },
            @{ child = "NestedKeyword1_1"; parent = "NestedKeyword1" },
            @{ child = "NestedKeyword1_2"; parent = "NestedKeyword1" },
            @{ child = "NestedKeyword2_1"; parent = "NestedKeyword2" },
            @{ child = "NestedKeyword2_2"; parent = "NestedKeyword2" },
            @{ child = "NestedKeyword1_1_1"; parent = "NestedKeyword1_1" },
            @{ child = "NestedKeyword2_2_1"; parent = "NestedKeyword2_2" },
            @{ child = "NestedKeyword2_2_1_1"; parent = "NestedKeyword2_2_1" }
        )

        $expr = @"
NestedKeyword
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

        $sb = {
            $null = [scriptblock]::Create("using module NestedDsl").Invoke()
            # Dsl use goes here
            $ast = [scriptblock]::Create($args[0]).Ast

            $astFinder =  {
                param($keywordName)
                [scriptblock]::Create(@"
{
    `$bindingFlags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance
    `$keywordProperty = [System.Management.Automation.Language.DynamicKeywordStatementAst].GetProperty("Keyword", `$bindingFlags)

    `$args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst] -and
    `$keywordProperty.GetValue(`$args[0]).Keyword -eq '$keywordName'
}
"@)
}

            $parentChildPairs = $args[1] -split ";"
            $accumulator = @{}
            foreach ($parentChild in $parentChildPairs)
            {
                $pair = $parentChild -split ","

                $parent = $ast.Find((& $astFinder $pair[0]), $true)

                if ($parent -eq $null)
                {
                    continue
                }

                $child = $parent.Find((& $astFinder $pair[1]), $true)

                if ($child -ne $null)
                {
                    $accumulator += @{ $pair[1] = $pair[0] }
                }
            }

            $accumulator
        }

        $parentChildPairs = ($testCases | ForEach-Object { $_.parent + "," + $_.child }) -join ";"
        $astDict = Get-ScriptBlockResultInNewProcess -TestDrive $TestDrive -ModuleNames "NestedDsl" -Command $sb -Arguments $expr,$parentChildPairs
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "contains <child> under higher level keyword <parent>" -TestCases $testCases {
        param($parent, $child)

        $astDict.$child | Should Be $parent
    }
}

Describe "DSL Body most AST parsing" -Tags "CI" {
    BeforeAll {
        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "ScriptBlock keyword has ScriptBlock body" {
        $sb = {
            $null = [scriptblock]::Create("using module BodyModeDsl").Invoke()

            $bindingFlags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance
            $bodyProperty = [System.Management.Automation.Language.DynamicKeywordStatementAst].GetProperty("BodyExpression", $bindingFlags)

            $ast = [scriptblock]::Create("ScriptBlockBodyKeyword { }").Ast

            $kwStmt = $ast.Find({ $args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst]}, $true)

            $bodyProperty.GetValue($kwStmt).StaticType
        }

        Get-ScriptBlockResultInNewProcess -TestDrive $TestDrive -ModuleNames "BodyModeDsl" -Command $sb | Should Be "System.Management.Automation.ScriptBlock"
    }

    It "Hashtable keyword has Hashtable body" {
        $sb = {
            $null = [scriptblock]::Create("using module BodyModeDsl").Invoke()

            $bindingFlags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance
            $bodyProperty = [System.Management.Automation.Language.DynamicKeywordStatementAst].GetProperty("BodyExpression", $bindingFlags)

            $ast = [scriptblock]::Create("HashtableBodyKeyword { }").Ast

            $kwStmt = $ast.Find({ $args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst]}, $true)

            $bodyProperty.GetValue($kwStmt).StaticType
        }

        Get-ScriptBlockResultInNewProcess -TestDrive $TestDrive -ModuleNames "BodyModeDsl" -Command $sb | Should Be "System.Collections.Hashtable"
    }

    It "Command keyword has no body" {
        $sb = {
            $null = [scriptblock]::Create("using module BodyModeDsl").Invoke()

            $bindingFlags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance
            $bodyProperty = [System.Management.Automation.Language.DynamicKeywordStatementAst].GetProperty("BodyExpression", $bindingFlags)

            $ast = [scriptblock]::Create("CommandBodyKeyword { }").Ast

            $kwStmt = $ast.Find({ $args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst]}, $true)

            $bodyProperty.GetValue($kwStmt).StaticType
        }

        Get-ScriptBlockResultInNewProcess -TestDrive $TestDrive -ModuleNames "BodyModeDsl" -Command $sb | Should Be $null
    }
}

Describe "Full DSL example loads into AST" -Tags "CI" {
    # TODO: Test AST of full DSL here
}
