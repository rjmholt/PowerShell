<#
Tests the creation of DynamicKeyword datastructures
#>

using module .\DslTestSupport.ps1

Describe "Basic DSL addition to runtime namespace" -Tags "CI" {
    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TESTDRIVE

        $dslName = "BasicDsl"
        $keywordName = "BasicKeyword"

        New-TestDllModule -TestDrive $TESTDRIVE -ModuleName $dslName

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

    Context "Keyword NameMode addition" {
        It "defaults the keyword NameMode to NoName when none is specified" {

        }

        It "adds the keyword NameMode to the DynamicKeyword" {

        }
    }

    Context "Keyword BodyMode addition" {
        It "defaults BodyMode to Command when none is specified" {

        }

        It "adds the keyword BodyMode to the DynamicKeyword" {

        }
    }
    
    Context "Keyword UseMode addition" {
        It "defaults the UseMode to Required when none is specified" {

        }

        It "adds the keyword UseMode to the DynamicKeyword" {

        }
    }

    Context "Mixed keyword mode attributes added" {
        It "adds the keyword NameMode to the DynamicKeyword" {

        }

        It "adds the keyword BodyMode to the DynamicKeyword" {

        }

        It "adds the keyword UseMode to the DynamicKeyword" {

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
