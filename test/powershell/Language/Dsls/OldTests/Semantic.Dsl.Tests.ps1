<#
Tests functionality of semantic features in DynamicKeywords
#>

Describe "Parsing of DSL semantic features" -Tags "CI" {
    It "rejects DSL that does not implement IPSKeyword" {

    }

    It "executes implemented PreParse actions" {

    }

    It "adds PostParse actions to AST" {

    }

    It "adds SemanticCheck actions to AST" {

    }

    It "rejects DSLs with both PreParse and PostParse delegates set to null" {

    }
}

Describe "Execution of semantic action" -Tags "CI" {
    Context "Executing pre-parse actions" {
        It "does nothing for null PreParse" {

        }

        It "successfully executes non-null PreParse delegate" {

        }
    }

    Context "Executing post-parse actions" {
        It "does nothing for null delegate" {

        }

        It "successfully executes non-null PostParse delegate" {

        }
    }

    Context "Executing semantic checks" {
        It "does nothing for null delegate" {

        }

        It "succesfully executes non-null SemanticCheck delegate" {

        }
    }
}
