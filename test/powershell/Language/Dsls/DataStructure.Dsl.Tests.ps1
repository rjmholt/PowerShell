<#
Tests the creation of DynamicKeyword datastructures

TODO:
    * Work out how keyword scoping occurs and ensure keywords tested are exposed
#>

using module .\DslTestSupport.ps1

Describe "Basic DSL addition to runtime namespace" -Tags "CI" {
    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        $dslName = "BasicDsl"
        $keywordName = "BasicKeyword"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName

        $testContext = [powershell]::Create()
        $testContext.AddScript("using module $dslName").Invoke()
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    It "imports the top level DSL keyword into the DynamicKeyword namespace" {
        $topLevelDslKeyword = $testContext.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($dslName)").Invoke()
        $topLevelDslKeyword.Keyword | Should Be $dslName
    }

    It "contains the inner keyword of the DSL in the imported DSL keyword" -Pending {
        # TODO: Work out how keyword scoping occurs
    }

    It "does not have the inner keyword available at the top level" {
        $testContext.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($keywordName)").Invoke() | Should Be $null
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

            $dkw = $defaultContext.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($defaultKeywordName)").Invoke()
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
            $kw = $nameModeContext.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($nameModeKeywordName)").Invoke()
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
            $kw = $bodyModeContext.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($bodyModeKeywordName)").Invoke()
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
            $kw = $useModeContext.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($useModeKeywordName)").Invoke()
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
            $kw = $mixedModeContext.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($kwName)").Invoke()
            $kw.NameMode | Should Be [System.Management.Automation.Language.DynamicKeywordNameMode]::Optional
        }

        It "sets MixedSyntaxModeBodyScriptBlockKeyword BodyMode to ScriptBlock" {
            $kwName = "MixedSyntaxModeBodyScriptBlockKeyword"
            $kw = $mixedModeContext.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($kwName)").Invoke()
            $kw.BodyMode | Should Be [System.Management.Automation.Language.DynamicKeywordBodyMode]::ScriptBlock
        }

        It "sets MixedSyntaxModeUseRequiredMany UseMode to RequiredMany" {
            $kwName = "MixedSyntaxModeUseRequiredMany"
            $kw = $mixedModeContext.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($kwName)").Invoke()
            $kw.UseMode | Should Be [System.Management.Automation.Language.DynamicKeywordUseMode]::RequiredMany
        }

        It "sets MixedSyntaxModeAllOptionsKeyword modes to correct configurations" {
            $kwName = "MixedSyntaxModeAllOptionsKeyword"
            $kw = $mixedModeContext.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword($kwName)").Invoke()
            
            $kw.NameMode | Should Be [System.Management.Automation.Language.DynamicKeywordNameMode]::Optional
            $kw.BodyMode | Should Be [System.Management.Automation.Language.DynamicKeywordBodyMode]::Hashtable
            $kw.UseMode  | Should Be [System.Management.Automation.Language.DynamicKeywordUseMode]::OptionalMany
        }
    }
}

Describe "Adding properties and parameters to DynamicKeyword datastructures" -Tags "CI" {
    It "adds a keyword property to the DynamicKeyword" {

    }

    It "adds a named keyword parameter to the DynamicKeyword" {

    }

    It "adds a positional keyword parameter to the DynamicKeyword" {

    }
}

Describe "Adding nested keywords to a DynamicKeyword" -Tags "CI" {
    It "adds a nested keyword to the upper level DynamicKeyword" {

    }

    It "adds all nested keywords to the upper level DynamicKeyword" {

    }

}

Describe "Adding PreParse, PostParse and SemanticCheck to a DynamicKeyword datastructure" {
    It "adds the PreParse action to the DynamicKeyword" {

    }

    It "adds the PostParse action to the DynamicKeyword" {

    }

    It "adds the SemanticCheck action to the DynamicKeyword" {

    }
}

Describe "Full keyword definition datastructure addition" {
    It "adds the dsl name to the namespace" {

    }

    It "adds all nested keywords to the DynamicKeyword cache" {

    }

    It "adds all keyword syntax modes to their respective keywords" {

    }

    It "adds all keyword properties and arguments to their respective keywords" {

    }

    It "adds all semantic actions to their respective keywords" {

    }
}
