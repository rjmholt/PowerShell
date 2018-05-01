# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

Describe "Foreach loop with attributed iteration variable" -Tags "CI" {

    Context "Unattributed foreach tests" {
        It "Processes an unattributed foreach loop" {
            $num = 1
            foreach ($x in 1..10)
            {
                $x | Should -Be $num
                $num++
            }
        }

        It "Allows mixed types in the foreach variable" {
            $vals = 1,"2",[object]::new(),[type],9.3,"Hello"

            $i = 0
            foreach ($v in $vals)
            {
                $v | Should -Be $vals[$i]
                $i++
            }
        }
    }

    Context "Type-constrained foreach tests" {
        It "Performs coercion on the enumeration variable" {
            $vals = 1,"2",3,"4",'5'
            $result = 1
            foreach ([int]$v in $vals)
            {
                $v | Should -Be $result
                $result++
            }
        }

        It "Results in an error when the type constraint cannot be applied" {
            { foreach ([int]$x in "3","banana"){ $x } } | Should -Throw
        }

        It "Supports chained type constraints" {
            $results = [char]'0',[char]'A',[char]'?',[char]27
            $i = 0
            foreach ([char][int]$c in "48","65","63","27")
            {
                $c | Should -Be $results[$i]
                $i++
            }
        }

        It "Failed constraints should terminate the loop" {
            $results = @{
                0 = [char]'a'
                1 = [char]5
                3 = [char]'@'
            }
            $i = 0
            try
            {
                foreach ([char]$c in "a",5,"three",64,"64")
                {
                    if ($results.ContainsKey($i))
                    {
                        $c | Should -Be $results[$i]
                    }
                    $i++
                }
            }
            catch
            {
            }
            $i | Should -Be 2
        }
    }

    Context "Attributed variables in foreach tests" {
        It "Allows a validation attribute on a variable" {
            $i = 1
            foreach ([ValidateRange(1,5)]$x in 1..4)
            {
                $x | Should -Be $i
                $i++
            }
        }

        It "Allows a validation attribute in addition to a type constraint" {
            $i = [int][char]"a"
            foreach ([ValidateRange(97,122)][int][char]$c in "a".."z")
            {
                $c | Should -Be $i
                $i++
            }
        }

        It "Terminates the loop when a validation attribute fails" {
            $i = 0
            try
            {
                foreach ([ValidateRange(5,17)]$x in 5..20)
                {
                    $i++
                }
            }
            catch
            {
            }
            $i | Should -Be 13
        }

        It "Supports set validation" {
            $i = 0
            try
            {
                foreach ([ValidateSet("X", "Y")]$x in "X","X","Y","X","Z","Y")
                {
                    $i++
                }
            }
            catch
            {
            }
            $i | Should -Be 4
        }

        It "Supports script validation" {
            $i = 0
            try
            {
                foreach ([ValidateScript({ $_ -lt 10 })]$x in 4,2,9,6,1,4,11,5,2)
                {
                    $i++
                }
            }
            catch
            {
            }
            $i | Should -Be 6
        }
    }
}
