Describe "Parsing of DSL semantic features" -Tags "Feature" {
    It "rejects DSL that does not implement IPSKeyword" {

    }

    It "adds PreParse actions to AST" {

    }

    It "adds PostParse actions to AST" {

    }

    It "adds SemanticCheck actions to AST" {

    }

    It "rejects DSLs with both PreParse and PostParse delegates set to null" {

    }
}

Describe "Execution of semantic action" -Tags "Feature" {
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
