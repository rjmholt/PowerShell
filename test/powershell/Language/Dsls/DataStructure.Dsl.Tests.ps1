<#
Tests the creation of DynamicKeyword datastructures
#>

Import-Module $PSScriptRoot\DslTestSupport.psm1

# Picks out a top level keyword by name, provided it is imported into the given context
function Get-TopLevelKeywordsStr
{
    param([string[]] $KeywordNames)

    $exp = "`$kws = @{}`n"

    foreach ($keywordName in $KeywordNames)
    {
        $exp += "`$kws.$keywordName = [System.Management.Automation.Language.DynamicKeyword]::GetKeyword('$keywordName')`n"
    }

    $exp += "`$kws"
    $exp
}

# Descends the keyword namespace tree to get the object representing the last DynamicKeyword in the list
function Get-InnerKeyword
{
    param($TopKw, [string[]] $NestedNames)

    $curr = $TopKw
    foreach ($name in $NestedNames)
    {
        if ($curr -eq $null)
        {
            return $null
        }
        $curr = $curr.InnerKeywords.$name
    }

    $curr
}

Describe "Basic DSL addition to runtime namespace" -Tags "CI" {
    BeforeAll {
        $moduleName = "BasicDsl"
        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "imports the top level DSL keyword into the DynamicKeyword namespace" {
        $expression = Get-TopLevelKeywordsStr -KeywordNames $moduleName
        $kwDict = Get-ExpressionFromModuleInNewContext -TestDrive $TestDrive -ModuleNames $moduleName -Expression $expression
        $kwDict.$moduleName.Keyword | Should Be $moduleName
    }
}

Describe "Adding syntax modes to the DynamicKeyword datastructure" -Tags "CI" {
    $testCases = @(
        # Defaults
        @{ mode = "BodyMode"; expected = "Command"; condition = "default"; dsl = "BasicDsl"; keyword = "BasicDsl" },
        @{ mode = "UseMode"; expected = "OptionalMany"; condition = "default"; dsl = "BasicDsl"; keyword = "BasicDsl" },

        # BodyMode settings
        @{ mode = "BodyMode"; expected = "Command"; condition = "DynamicKeywordBodyMode.Command"; dsl = "BodyModeDsl"; keyword = "CommandBodyKeyword" },
        @{ mode = "BodyMode"; expected = "ScriptBlock"; condition = "DynamicKeywordBodyMode.ScriptBlock"; dsl = "BodyModeDsl"; keyword = "ScriptBlockBodyKeyword" }
        @{ mode = "BodyMode"; expected = "Hashtable"; condition = "DynamicKeywordBodyMode.Hashtable"; dsl = "BodyModeDsl"; keyword = "HashtableBodyKeyword" },

        # UseMode settings
        @{ mode = "UseMode"; expected = "Optional"; condition = "DynamicKeywordUseMode.Optional"; dsl = "UseModeDsl"; keyword = "OptionalUseKeyword" },
        @{ mode = "UseMode"; expected = "OptionalMany"; condition = "DynamicKeywordUseMode.OptionalMany"; dsl = "UseModeDsl"; keyword = "OptionalManyUseKeyword" },
        @{ mode = "UseMode"; expected = "Required"; condition = "DynamicKeywordUseMode.Required"; dsl = "UseModeDsl"; keyword = "RequiredUseKeyword" },
        @{ mode = "UseMode"; expected = "RequiredMany"; condition = "DynamicKeywordUseMode.RequiredMany"; dsl = "UseModeDsl"; keyword = "RequiredManyUseKeyword" },

        # Mixed mode settings
        @{ mode = "BodyMode"; expected = "ScriptBlock"; condition = "DynamicKeywordBodyMode.ScriptBlock"; dsl = "MixedModeDsl"; keyword = "MixedModeKeyword" },
        @{ mode = "UseMode"; expected = "Required"; condition = "DynamicKeywordUseMode.Required"; dsl = "MixedModeDsl"; keyword = "MixedModeKeyword" }
    )

    BeforeAll {
        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        $moduleNames = @("BasicDsl", "BodyModeDsl", "UseModeDsl", "MixedModeDsl")
        $keywordNames = @(
                          "BasicDsl",
                          "CommandBodyKeyword", "ScriptBlockBodyKeyword", "HashtableBodyKeyword",
                          "OptionalUseKeyword", "OptionalManyUseKeyword", "RequiredUseKeyword", "RequiredManyUseKeyword",
                          "MixedModeKeyword"
                         )
        $expression = Get-TopLevelKeywordsStr -KeywordNames $keywordNames
        $kwDict = Get-ExpressionFromModuleInNewContext -TestDrive $TestDrive -ModuleNames $moduleNames -Expression $expression
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "sets <mode> to <expected> when specification is <condition>" -TestCases $testCases {
        param($mode, $expected, $condition, $dsl, $keyword)

        $kwDict.$keyword.$mode.Value | Should Be $expected
    }
}

Describe "Adding properties to DynamicKeyword datastructures" -Tags "CI" {
    $testCases = @(
        @{ name = "DefaultProperty"; type = "string"; mandatory = "optional" },
        @{ name = "MandatoryProperty"; type = "string"; mandatory = "mandatory" },
        @{ name = "IntProperty"; type = "int"; mandatory = "optional" },
        @{ name = "CustomTypeProperty"; type = "PropertyType"; mandatory = "optional" }
    )

    BeforeAll {
        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        $moduleName = "PropertyDsl"
        $keywordName = "PropertyKeyword"

        $expression = Get-TopLevelKeywordsStr -KeywordNames $keywordName
        $kwDict = Get-ExpressionFromModuleInNewContext -TestDrive $TestDrive -ModuleNames $moduleName -Prelude $serialization -Expression $expression
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "adds a property <name> to the keyword" -TestCases $testCases {
        param($name, $type, $mandatory)
        $kwDict.$keywordName.Properties.$name.Name | Should Be $name
    }

    It "adds a property <name> of type <type> to the keyword" -TestCases $testCases {
        param($name, $type, $mandatory)
        $kwDict.$keywordName.Properties.$name.TypeConstraint | Should Be $type
    }

    It "adds a property <name> which is <mandatory>" -TestCases $testCases {
        param($name, $type, $mandatory)
        $kwDict.$keywordName.Properties.$name.Mandatory | Should Be ($mandatory -eq "mandatory")
    }
}

Describe "Adding parameters to DynamicKeyword datastructures" -Tags "CI" {
    $testCases = @(
        @{ name = "NamedParameter"; type = "string"; position = -1; mandatory = $false },
        @{ name = "PositionalParameter"; type = "string"; position = 0; mandatory = $false },
        @{ name = "MandatoryNamedParameter"; type = "string"; position = -1; mandatory = $true },
        @{ name = "MandatoryPositionalParameter"; type = "string"; position = 1; mandatory = $true },
        @{ name = "IntParameter"; type = "int"; position = -1; mandatory = $false },
        @{ name = "CustomTypeParameter"; type = "KeywordParameterType"; position = -1; mandatory = $false }
    )

    BeforeAll {
        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        $moduleName = "ParameterDsl"
        $keywordName = "ParameterKeyword"

        $expression = Get-TopLevelKeywordsStr -KeywordNames $keywordName
        $kwDict = Get-ExpressionFromModuleInNewContext -TestDrive $TestDrive -ModuleNames $moduleNAme -Expression $expression
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "adds parameter <name> with type <type>" -TestCases $testCases {
        param($name, $type, $position, $mandatory)

        $kwDict.$keywordName.Parameters.$name.TypeConstraint | Should Be $type
    }

    It "adds parameter <name> with mandatory set to <mandatory>" -TestCases $testCases {
        param($name, $type, $position, $mandatory)
        $kwDict.$keywordName.Parameters.$name.Mandatory | Should Be $mandatory
    }

    It "adds parameter <name> with position set to <position>" -TestCases $testCases {
        param($name, $type, $position, $mandatory)
        $kwDict.$keywordName.Parameters.$name.Position | Should Be $position
    }
}

Describe "Adding nested keywords to a DynamicKeyword" -Tags "CI" {
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
        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        $moduleName = "NestedDsl"
        $topKeywordName = "NestedKeyword"

        $nesting = "Update-TypeData -SerializationDepth 7 -TypeName System.Management.Automation.Language.DynamicKeyword" 

        $expression = Get-TopLevelKeywordsStr -KeywordNames $topKeywordName
        $kw = (Get-ExpressionFromModuleInNewContext -TestDrive $TestDrive -ModuleNames $moduleName -Prelude $nesting -Expression $expression).$topKeywordName
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "finds the inner keyword <keywordToFind> under the according path" -TestCases $testCases {
        param($keywordToFind, $pathToKeyword)

        $innerKw = Get-InnerKeyword -TopKw $kw -NestedNames ($pathToKeyword + $keywordToFind)
        $innerKw.Keyword | Should Be $keywordToFind
    }
}

# TODO: Create a full DSL to test for this
Describe "Full keyword definition datastructure addition" {
    It "adds the dsl name to the namespace" -Pending {

    }

    It "adds all nested keywords to the DynamicKeyword cache" -Pending {

    }

    It "adds all keyword syntax modes to their respective keywords" -Pending {

    }

    It "adds all keyword properties and arguments to their respective keywords" -Pending {

    }

    It "adds all semantic actions to their respective keywords" -Pending {

    }
}
