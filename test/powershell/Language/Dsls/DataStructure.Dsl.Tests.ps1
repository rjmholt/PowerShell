<#
Tests the creation of DynamicKeyword datastructures
#>

using module .\DslTestSupport.psm1

function Get-TopLevelKeywordInContext
{
    param([powershell] $Context, [string] $KeywordName)

    $Context.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($KeywordName)").Invoke()
}

# Descends the keyword namespace tree to get the object representing the last DynamicKeyword in the list
function Get-InnerKeyword
{
    param([powershell] $Context, [string] $TopLevelKeywordName, [string[]] $NestedNames)

    $topKw = $Context.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($TopLevelKeywordName)").Invoke()

    $curr = $topKw
    foreach ($name in $NestedNames)
    {
        $curr = $curr.GetInnerKeyword($name)
    }

    $curr
}

Describe "Basic DSL addition to runtime namespace" -Tags "CI" {
    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        $dslName = "BasicDsl"
        $keywordName = "BasicKeyword"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName

        $basicContext = [powershell]::Create()
        $basicContext.AddScript("using module $dslName").Invoke()
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    It "imports the top level DSL keyword into the DynamicKeyword namespace" {
        $topLevelDslKeyword = Get-TopLevelKeywordInContext -Context $basicContext -KeywordName $dslName
        $topLevelDslKeyword.Keyword | Should Be $dslName
    }

    It "contains the inner keyword of the DSL in the imported DSL keyword" {
        $innerKeyword = Get-InnerKeyword -Context $basicContext -TopLevelKeywordName $dslName -NestedNames @($keywordName)
        $innerKeyword.Keyword | Should Be $keywordName
    }

    It "does not have the inner keyword available at the top level" {
        Get-TopLevelKeywordInContext -Context $basicContext -KeywordName $keywordName | Should Be $null
    }
}

Describe "Adding syntax modes to the DynamicKeyword datastructure" -Tags "CI" {
    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    Context "Default syntax mode tests" {
        BeforeAll {
            $defaultDslName = "BasicDsl"
            $defaultKeywordName = "BasicKeyword"
            New-TestDllModule -TestDrive $TestDrive -ModuleName $defaultDslName

            $defaultContext = [powershell]::Create()
            $defaultContext.AddScript("using module $defaultDslName").Invoke()

            $dkw = Get-InnerKeyword -Context $defaultContext -TopLevelKeywordName $defaultDslName -NestedNames @($defaultKeywordName)
        }

        It "has default NameMode as NoName" {
            $dkw.NameMode | Should Be [System.Management.Automation.Language.DynamicKeywordNameMode]::NoName
        }

        It "has default BodyMode as Comamnd" {
            $dkw.BodyMode | Should Be [System.Management.Automation.Language.DynamicKeywordBodyMode]::Command
        }

        It "has default UseMode as Required" {
            $dkw.UseMode | Should Be [System.Management.Automation.Language.DynamicKeywordUseMode]::Required
        }
    }

    Context "NameModeSyntax module testing" {
        BeforeAll {
            $nameModeDslName = "NameSyntaxModeDsl"
            $nameModeKeywordName = "NameSyntaxModeKeyword"

            New-TestDllModule -TestDrive $TestDrive -ModuleName $nameModeDslName

            $nameModeContext = [powershell]::Create()
            $nameModeContext.AddScript("using module $nameModeDslName").Invoke()
        }

        It "adds the keyword NameMode to the DynamicKeyword" {
            $kw = Get-InnerKeyword -Context $nameModeContext -TopLevelKeywordName $nameModeDslName -NestedNames @($nameModeKeywordName)
            $kw.NameMode | Should Be [System.Management.Automation.Language.DynamicKewordNameMode]::Required
        }
    }

    Context "BodyModeSyntax module testing" {
        BeforeAll {
            $bodyModeDslName = "BodySyntaxModeDsl"
            $bodyModeKeywordName = "BodySyntaxModeKeyword"

            New-TestDllModule -TestDrive $TestDrive -ModuleName $bodyModeDslName

            $bodyModeContext = [powershell]::Create()
            $bodyModeContext.AddScript("using module $bodyModeDslName").Invoke()

        }

        It "adds the keyword BodyMode to the DynamicKeyword" {
            $kw = Get-InnerKeyword -Context $bodyModeContext -TopLevelKeywordName $bodyModeDslName -NestedNames @($bodyModeKeywordName)
            $kw.BodyMode | Should Be [System.Management.Automation.Language.DynamicKewordBodyMode]::Hashtable
        }
    }
    
    Context "UseModeSyntax module testing" {
        BeforeAll {
            $useModeDslName = "UseSyntaxModeDsl"
            $useModeKeywordName = "UseSyntaxModeKeyword"

            New-TestDllModule -TestDrive $TestDrive -ModuleName $useModeDslName

            $useModeContext = [powershell]::Create()
            $useModeContext.AddScript("using module $useModeDslName").Invoke()
        }

        It "adds the keyword UseMode to the DynamicKeyword" {
            $kw = Get-InnerKeyword -Context $useModeContext -TopLevelKeywordName $useModeDslName -NestedNames @($useModeKeywordName)
            $kw.UseMode | Should Be [System.Management.Automation.Language.DynamicKewordUseMode]::Hashtable
        }
    }

    Context "Mixed keyword mode attributes added" {
        BeforeAll {
            $mixedModeDslName = "MixedSyntaxModeDsl"

            New-TestDllModule -TestDrive $TestDrive -ModuleName $mixedModeDslName

            $mixedModeContext = [powershell]::Create()
            $mixedModeContext.AddScript("using module $mixedModeDslName").Invoked()
        }

        It "sets MixedSyntaxModeNameOptionalKeyword NameMode to Optional" {
            $kwName = "MixedSyntaxModeNameOptionalKeyword"
            $kw = Get-InnerKeyword -Context $mixedModeContext -TopLevelKeywordName $mixedModeDslName -NestedNames @($kwName)
            $kw.NameMode | Should Be [System.Management.Automation.Language.DynamicKeywordNameMode]::Optional
        }

        It "sets MixedSyntaxModeBodyScriptBlockKeyword BodyMode to ScriptBlock" {
            $kwName = "MixedSyntaxModeBodyScriptBlockKeyword"
            $kw = Get-InnerKeyword -Context $mixedModeContext -TopLevelKeywordName $mixedModeDslName -NestedNames @($kwName)
            $kw.BodyMode | Should Be [System.Management.Automation.Language.DynamicKeywordBodyMode]::ScriptBlock
        }

        It "sets MixedSyntaxModeUseRequiredMany UseMode to RequiredMany" {
            $kwName = "MixedSyntaxModeUseRequiredMany"
            $kw = Get-InnerKeyword -Context $mixedModeContext -TopLevelKeywordName $mixedModeDslName -NestedNames @($kwName)
            $kw.UseMode | Should Be [System.Management.Automation.Language.DynamicKeywordUseMode]::RequiredMany
        }

        It "sets MixedSyntaxModeAllOptionsKeyword modes to correct configurations" {
            $kwName = "MixedSyntaxModeAllOptionsKeyword"
            $kw = Get-InnerKeyword -Context $mixedModeContext -TopLevelKeywordName $mixedModeDslName -NestedNames @($kwName)
            
            $kw.NameMode | Should Be [System.Management.Automation.Language.DynamicKeywordNameMode]::Optional
            $kw.BodyMode | Should Be [System.Management.Automation.Language.DynamicKeywordBodyMode]::Hashtable
            $kw.UseMode  | Should Be [System.Management.Automation.Language.DynamicKeywordUseMode]::OptionalMany
        }
    }
}

Describe "Adding properties to DynamicKeyword datastructures" -Tags "CI" {
    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        $dslName = "PropertyDsl"
        $keywordName = "PropertyKeyword"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName

        $parameterContext = [powershell]::Create()
        $parameterContext.AddScript("using module $dslName").Invoke()
    }

    It "adds a property to the DynamicKeyword" {
        $propertyName = "PropertyName"
        $kw = Get-InnerKeyword -Context $parameterContext -TopLevelKeywordName $dslName -NestedNames @($keywordName)
        $kw.Properties.$propertyName | Should Not Be $null
    }
}

Describe "Adding parameters to DynamicKeyword datastructures" -Tags "CI" {
    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        $dslName = "ParameterDsl"
        $keywordName = "ParameterKeyword"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName

        $parameterContext = [powershell]::Create()
        $parameterContext.AddScript("using module $dslName").Invoke()
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    It "adds a named keyword parameter to the DynamicKeyword" {
        $parameterName = "NamedParameter"
        $kw = Get-InnerKeyword -Context $parameterContext -TopLevelKeywordName $dslName -NestedNames @($keywordName)
        $kw.Parameters.$parameterName | Should Not Be $null
    }

    It "adds positional keyword parameters to the DynamicKeyword" -Pending {

    }
}

Describe "Adding nested keywords to a DynamicKeyword" -Tags "CI" {

    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        $dslName = "NestedDsl"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName

        $nestedContext = [powershell]::Create()
        $nestedContext.AddScript("using module $dslName").Invoke()
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    It "adds a nested keyword to the upper level DynamicKeyword" {
        $kw = Get-InnerKeyword -Context $nestedContext -TopLevelKeywordName $dslName -NestedNames @("NestedKeyword1", "NestedKeyword1_1", "NestedKeyword1_1_1")
        $kw | Should Not Be $null
    }

    It "adds all nested keywords to the upper level DynamicKeyword" {
        $keywordPaths = @(
            @("NestedKeyword1", "NestedKeyword1_1", "NestedKeyword1_1_1"),
            @("NestedKeyword1", "NestedKeyword1_2"),
            @("NestedKeyword2", "NestedKeyword2_1"),
            @("NestedKeyword2", "NestedKeyword2_2", "NestedKeyword2_2_1", "NestedKeyword2_2_1_1")
        )

        foreach ($path in $keywordPaths)
        {
            $kw = Get-InnerKeyword -Context $nestedContext -TopLevelKeywordName $dslName -NestedNames $path
            $kw | Should Not Be $null
        }
    }
}

Describe "Adding PreParse, PostParse and SemanticCheck to a DynamicKeyword datastructure" {

    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        $dslName = "SemanticDsl"
        $keywordName = "SemanticKeyword"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName

        $semanticContext = [powershell]::Create()
        $semanticContext.AddScript("using module $dslName").Invoke()

        $kw = Get-InnerKeyword -Context $semanticContext -TopLevelKeywordName $dslName -NestedNames @($keywordName)
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    It "adds the PreParse action to the DynamicKeyword" {
        $kw.PreParse | Should Not Be $null

        if ($kw.PreParse -ne $null)
        {
            # TODO: Figure out what action to put here to test the semantic action
        }
    }

    It "adds the PostParse action to the DynamicKeyword" {
        $kw.PostParse | Should Not Be $null

        if ($kw.PostParse -ne $null)
        {
            # TODO: Figure out what action to put here to test the semantic action
        }
    }

    It "adds the SemanticCheck action to the DynamicKeyword" {
        $kw.SemanticCheck | Should Not Be $null

        if ($kw.SemanticCheck -ne $null)
        {
            # TODO: Figure out what action to test the SemanticCheck with
        }
    }
}

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
